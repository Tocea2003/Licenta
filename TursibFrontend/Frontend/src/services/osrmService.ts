import type { LatLngTuple } from '@/utils/geo'

// Client pentru serverul public OSRM (rute pe străzi), folosit pentru mers pe jos
// și ca fallback pentru segmentele de autobuz fără geometrie GTFS.

const OSRM_BASE_URL = 'https://router.project-osrm.org/route/v1'

export type OsrmProfile = 'foot' | 'driving'

export interface OsrmRoute {
  path: LatLngTuple[]
  /** metri */
  distance: number
  /** secunde */
  duration: number
}

interface LonLat {
  lat: number
  lon: number
}

/** Ruta OSRM printre punctele date, sau null dacă serviciul nu găsește rută / e indisponibil. */
export async function fetchOsrmRoute(profile: OsrmProfile, points: readonly LonLat[]): Promise<OsrmRoute | null> {
  if (points.length < 2) return null
  const coordinates = points.map(p => `${p.lon},${p.lat}`).join(';')
  try {
    const response = await fetch(`${OSRM_BASE_URL}/${profile}/${coordinates}?overview=full&geometries=geojson`)
    const data = await response.json()
    const route = data?.code === 'Ok' ? data.routes?.[0] : null
    if (!route?.geometry?.coordinates) return null
    return {
      path: route.geometry.coordinates.map((c: number[]) => [c[1], c[0]] as LatLngTuple),
      distance: route.distance,
      duration: route.duration
    }
  } catch {
    return null
  }
}
