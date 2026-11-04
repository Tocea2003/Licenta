// Walking directions from the public Valhalla server (FOSSGIS).
// Uses the pedestrian profile, so one-way streets do not apply and
// footpaths are allowed; instructions come back already localized.
import { haversineKm, type LatLngTuple } from '@/utils/geo'

const VALHALLA_URL = 'https://valhalla1.openstreetmap.de/route'
const WALKING_SPEED_KMH = 5

export type ManeuverKind =
  | 'depart'
  | 'arrive'
  | 'straight'
  | 'slight-left'
  | 'left'
  | 'sharp-left'
  | 'slight-right'
  | 'right'
  | 'sharp-right'
  | 'uturn'
  | 'roundabout'

export interface WalkingStep {
  kind: ManeuverKind
  /** Localized text from Valhalla; empty for the straight-line fallback. */
  instruction: string
  distanceM: number
  durationS: number
}

export interface WalkingRoute {
  distanceM: number
  durationS: number
  geometry: LatLngTuple[]
  steps: WalkingStep[]
  /** True when Valhalla failed and this is a straight line at walking speed. */
  approximate: boolean
}

export interface LatLon {
  lat: number
  lon: number
}

const VALHALLA_LANGUAGE: Record<string, string> = {
  ro: 'ro-RO',
  en: 'en-US',
  de: 'de-DE',
}

/** Valhalla maneuver type → icon kind. See the turn-by-turn API reference. */
export function maneuverKind(type: number): ManeuverKind {
  switch (type) {
    case 1: case 2: case 3: return 'depart'
    case 4: case 5: case 6: return 'arrive'
    case 9: case 18: case 24: return 'slight-right'
    case 10: case 20: return 'right'
    case 11: return 'sharp-right'
    case 12: case 13: return 'uturn'
    case 14: return 'sharp-left'
    case 15: case 21: return 'left'
    case 16: case 19: case 25: return 'slight-left'
    case 26: case 27: return 'roundabout'
    default: return 'straight'
  }
}

/** Decodes a Valhalla encoded polyline (precision 6) into [lat, lon] pairs. */
export function decodePolyline(encoded: string, precision = 6): LatLngTuple[] {
  const factor = Math.pow(10, precision)
  const points: LatLngTuple[] = []
  let index = 0
  let lat = 0
  let lon = 0

  const next = (): number => {
    let shift = 0
    let result = 0
    let byte: number
    do {
      byte = encoded.charCodeAt(index++) - 63
      result |= (byte & 0x1f) << shift
      shift += 5
    } while (byte >= 0x20)
    return result & 1 ? ~(result >> 1) : result >> 1
  }

  while (index < encoded.length) {
    lat += next()
    lon += next()
    points.push([lat / factor, lon / factor])
  }
  return points
}

/** Straight line at walking speed, used when the routing server is unavailable. */
export function straightLineRoute(start: LatLon, end: LatLon): WalkingRoute {
  const distanceM = haversineKm(start.lat, start.lon, end.lat, end.lon) * 1000
  const durationS = (distanceM / 1000 / WALKING_SPEED_KMH) * 3600
  return {
    distanceM,
    durationS,
    geometry: [[start.lat, start.lon], [end.lat, end.lon]],
    steps: [
      { kind: 'depart', instruction: '', distanceM, durationS },
      { kind: 'arrive', instruction: '', distanceM: 0, durationS: 0 },
    ],
    approximate: true,
  }
}

interface ValhallaManeuver {
  type: number
  instruction?: string
  length: number
  time: number
}

interface ValhallaResponse {
  trip?: {
    legs?: { shape: string; maneuvers: ValhallaManeuver[]; summary: { length: number; time: number } }[]
  }
}

export function parseValhallaResponse(data: ValhallaResponse): WalkingRoute | null {
  const leg = data.trip?.legs?.[0]
  if (!leg) return null
  return {
    distanceM: leg.summary.length * 1000,
    durationS: leg.summary.time,
    geometry: decodePolyline(leg.shape),
    steps: leg.maneuvers.map((m) => ({
      kind: maneuverKind(m.type),
      instruction: m.instruction ?? '',
      distanceM: m.length * 1000,
      durationS: m.time,
    })),
    approximate: false,
  }
}

/** Walking route between two points; falls back to a straight line, never throws. */
export async function fetchWalkingRoute(
  start: LatLon,
  end: LatLon,
  language = 'ro',
  signal?: AbortSignal,
): Promise<WalkingRoute> {
  try {
    const response = await fetch(VALHALLA_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        locations: [start, end],
        costing: 'pedestrian',
        units: 'kilometers',
        directions_options: { language: VALHALLA_LANGUAGE[language] ?? 'ro-RO' },
      }),
      signal,
    })
    if (response.ok) {
      const route = parseValhallaResponse(await response.json())
      if (route) return route
    }
  } catch (error) {
    if (signal?.aborted) throw error
  }
  return straightLineRoute(start, end)
}
