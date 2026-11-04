// Funcții geografice pure (fără Vue/Leaflet), folosite de hartă și planificator.

export type LatLngTuple = [number, number]

export interface GeoPoint {
  latitude: number
  longitude: number
}

const EARTH_RADIUS_KM = 6371
const toRad = (deg: number) => (deg * Math.PI) / 180

/** Distanța pe sferă (formula Haversine), în kilometri. */
export function haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const dLat = toRad(lat2 - lat1)
  const dLon = toRad(lon2 - lon1)
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2
  return EARTH_RADIUS_KM * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}

/** Azimutul (0-360°, 0 = nord) de la punctul A spre punctul B. */
export function bearingDeg(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const dLon = toRad(lon2 - lon1)
  const lat1R = toRad(lat1)
  const lat2R = toRad(lat2)
  const y = Math.sin(dLon) * Math.cos(lat2R)
  const x = Math.cos(lat1R) * Math.sin(lat2R) - Math.sin(lat1R) * Math.cos(lat2R) * Math.cos(dLon)
  return ((Math.atan2(y, x) * 180) / Math.PI + 360) % 360
}

/** Cel mai mic unghi absolut dintre două direcții (0-180°). */
export function angleDiff(a: number, b: number): number {
  return Math.abs(((a - b + 540) % 360) - 180)
}

/** Stația cea mai apropiată de un punct, sau null dacă lista e goală. */
export function findNearestStation<T extends GeoPoint>(lat: number, lon: number, stations: readonly T[]): T | null {
  let nearest: T | null = null
  let minDistance = Infinity
  for (const station of stations) {
    const distance = haversineKm(lat, lon, station.latitude, station.longitude)
    if (distance < minDistance) {
      minDistance = distance
      nearest = station
    }
  }
  return nearest
}

/**
 * Taie geometria unei rute (lista completă de puncte GTFS) la porțiunea dintre două stații:
 * găsește punctele cele mai apropiate de fiecare stație și păstrează ce e între ele.
 */
export function trimShapeToStations(points: readonly GeoPoint[], from: GeoPoint, to: GeoPoint): LatLngTuple[] {
  if (!points || points.length === 0) return []
  let fromIdx = 0
  let fromDist = Infinity
  let toIdx = points.length - 1
  let toDist = Infinity
  points.forEach((point, i) => {
    const dFrom = Math.hypot(point.latitude - from.latitude, point.longitude - from.longitude)
    const dTo = Math.hypot(point.latitude - to.latitude, point.longitude - to.longitude)
    if (dFrom < fromDist) { fromDist = dFrom; fromIdx = i }
    if (dTo < toDist) { toDist = dTo; toIdx = i }
  })
  const start = Math.min(fromIdx, toIdx)
  const end = Math.max(fromIdx, toIdx)
  return points.slice(start, end + 1).map(p => [p.latitude, p.longitude] as LatLngTuple)
}
