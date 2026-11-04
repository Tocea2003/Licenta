import apiService from '@/services/apiService'
import { fetchOsrmRoute } from '@/services/osrmService'
import { trimShapeToStations, type GeoPoint, type LatLngTuple } from '@/utils/geo'

// Geometria segmentelor unei călătorii (ce se desenează pe hartă).

interface StationRef extends GeoPoint {
  id: number
}

const toLonLat = (p: GeoPoint) => ({ lat: p.latitude, lon: p.longitude })

/**
 * Traseul autobuzului între două stații: geometria GTFS a liniei, tăiată între stații;
 * dacă lipsește (sau are sub 2 puncte), ruta OSRM pe străzi între cele două stații.
 */
export async function buildBusSegmentPath(routeId: number, from: StationRef, to: StationRef): Promise<LatLngTuple[]> {
  let path: LatLngTuple[] = []
  try {
    const segment = await apiService.getRouteSegment(routeId, from.id, to.id)
    if (segment?.points?.length) {
      path = trimShapeToStations(segment.points, from, to)
    }
  } catch {
    // fallback OSRM mai jos
  }

  if (path.length < 2) {
    path = (await fetchOsrmRoute('driving', [toLonLat(from), toLonLat(to)]))?.path ?? []
  }
  return path
}

/** Traseul pietonal OSRM între două puncte ({lat, lon}), sau [] dacă nu se poate calcula. */
export async function buildWalkingPath(from: { lat: number; lon: number }, to: { lat: number; lon: number }): Promise<LatLngTuple[]> {
  return (await fetchOsrmRoute('foot', [from, to]))?.path ?? []
}
