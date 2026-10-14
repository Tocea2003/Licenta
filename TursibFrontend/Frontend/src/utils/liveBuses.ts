import type { BusLocation, StationEta } from '@/types/bus'
import { angleDiff, bearingDeg, haversineKm, type GeoPoint } from '@/utils/geo'

// Logica pură pentru autobuzele live: parsare, ce afișăm pe hartă, ETA spre o stație.

/** Transformă nodul Firebase `bus_locations` într-o listă (doar intrările route_X_bus_Y cu poziție). */
export function parseBusLocations(raw: Record<string, unknown> | null | undefined): BusLocation[] {
  if (!raw) return []
  const buses: BusLocation[] = []
  for (const [id, value] of Object.entries(raw)) {
    if (!id.startsWith('route_')) continue
    const data = value as Partial<BusLocation> | null
    if (!data?.latitude || !data?.longitude) continue
    buses.push({
      id,
      latitude: data.latitude,
      longitude: data.longitude,
      routeId: data.routeId as number,
      routeNumber: data.routeNumber,
      routeName: data.routeName,
      timestamp: data.timestamp as number,
      speed: data.speed as number,
      heading: data.heading as number,
      occupancy: data.occupancy,
      status: data.status,
      directionId: data.directionId,
      nextStationId: data.nextStationId,
      nextStationName: data.nextStationName,
      nextStationEta: data.nextStationEta
    })
  }
  return buses
}

export interface VisibleBusOptions {
  userLocation?: { lat: number; lon: number } | null
  selectedRouteId?: number | null
  showAll?: boolean
  /** câte autobuze afișăm în afara rutei selectate */
  limit?: number
  /** raza în jurul utilizatorului (km) */
  maxDistanceKm?: number
}

/**
 * Ce autobuze afișăm pe hartă:
 * - cu locația utilizatorului: doar cele din rază, sortate după distanță;
 * - cele de pe ruta selectată apar mereu, restul sunt limitate la `limit` (dacă nu e `showAll`).
 */
export function selectVisibleBuses(buses: BusLocation[], options: VisibleBusOptions = {}): BusLocation[] {
  const { userLocation, selectedRouteId = null, showAll = false, limit = 30, maxDistanceKm = 30 } = options

  let candidates = buses
  if (userLocation?.lat && userLocation?.lon) {
    candidates = buses
      .map(bus => ({ bus, distance: haversineKm(userLocation.lat, userLocation.lon, bus.latitude, bus.longitude) }))
      .filter(x => x.distance <= maxDistanceKm)
      .sort((a, b) => a.distance - b.distance)
      .map(x => x.bus)
  }

  if (showAll) return candidates
  if (selectedRouteId != null) {
    const onRoute = candidates.filter(b => b.routeId === selectedRouteId)
    const others = candidates.filter(b => b.routeId !== selectedRouteId).slice(0, limit)
    return [...onRoute, ...others]
  }
  return candidates.slice(0, limit)
}

export interface StationEtaOptions {
  routeColors: Record<number, string>
  /** eticheta afișată pentru linie, ex. (id) => `Linia ${id}` */
  routeLabel: (routeId: number) => string
  maxResults?: number
}

/**
 * Autobuzele care vin spre o stație, cu ETA estimat din distanță și viteză.
 * Pentru fiecare linie păstrăm doar cel mai apropiat autobuz care se îndreaptă spre stație.
 */
export function computeStationEtas(station: GeoPoint, buses: BusLocation[], options: StationEtaOptions): StationEta[] {
  const { routeColors, routeLabel, maxResults = 8 } = options
  const closestPerRoute = new Map<number, { busId: string; distKm: number; speed: number }>()

  for (const bus of buses) {
    if (typeof bus.latitude !== 'number' || typeof bus.longitude !== 'number') continue

    const distKm = haversineKm(bus.latitude, bus.longitude, station.latitude, station.longitude)
    // Ignorăm autobuzele la peste 4 km sau deja în stație (< 30 m)
    if (distKm > 4 || distKm < 0.03) continue

    // Ignorăm autobuzele care se îndepărtează (direcție la peste 90° de stație)
    if (typeof bus.heading === 'number') {
      const bearing = bearingDeg(bus.latitude, bus.longitude, station.latitude, station.longitude)
      if (angleDiff(bus.heading, bearing) > 90) continue
    }

    const existing = closestPerRoute.get(bus.routeId)
    if (!existing || distKm < existing.distKm) {
      closestPerRoute.set(bus.routeId, { busId: bus.id, distKm, speed: bus.speed ?? 0 })
    }
  }

  const etas: Array<StationEta & { minutes: number }> = []
  for (const [routeId, { busId, distKm, speed }] of closestPerRoute) {
    // Viteza reală dacă e relevantă, altfel media urbană de 25 km/h
    const avgSpeed = speed > 5 ? Math.min(speed, 50) : 25
    const minutes = Math.round((distKm / avgSpeed) * 60)
    if (minutes > 30) continue
    etas.push({
      busId,
      routeId,
      routeNumber: routeLabel(routeId),
      eta: minutes <= 1 ? '<1 min' : `${minutes} min`,
      color: routeColors[routeId] || '#2563eb',
      minutes: minutes <= 1 ? 0 : minutes
    })
  }

  return etas
    .sort((a, b) => a.minutes - b.minutes)
    .slice(0, maxResults)
    .map(({ minutes: _minutes, ...eta }) => eta)
}
