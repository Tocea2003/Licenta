import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  decodePolyline,
  fetchWalkingRoute,
  maneuverKind,
  parseValhallaResponse,
  straightLineRoute,
} from './valhallaService'

// Encodes [lat, lon] pairs the same way Valhalla does (precision 6).
function encode(points: [number, number][]): string {
  let out = ''
  let prevLat = 0
  let prevLon = 0
  const put = (value: number) => {
    let v = value < 0 ? ~(value << 1) : value << 1
    while (v >= 0x20) {
      out += String.fromCharCode((0x20 | (v & 0x1f)) + 63)
      v >>= 5
    }
    out += String.fromCharCode(v + 63)
  }
  for (const [lat, lon] of points) {
    const la = Math.round(lat * 1e6)
    const lo = Math.round(lon * 1e6)
    put(la - prevLat)
    put(lo - prevLon)
    prevLat = la
    prevLon = lo
  }
  return out
}

const SIBIU: [number, number][] = [
  [45.797, 24.152],
  [45.7975, 24.1531],
  [45.7962, 24.1544],
]

describe('decodePolyline', () => {
  it('round-trips precision-6 coordinates', () => {
    const decoded = decodePolyline(encode(SIBIU))
    expect(decoded).toHaveLength(3)
    decoded.forEach(([lat, lon], i) => {
      expect(lat).toBeCloseTo(SIBIU[i]![0], 6)
      expect(lon).toBeCloseTo(SIBIU[i]![1], 6)
    })
  })

  it('returns an empty list for an empty string', () => {
    expect(decodePolyline('')).toEqual([])
  })
})

describe('maneuverKind', () => {
  it('maps Valhalla maneuver types to turn directions', () => {
    expect(maneuverKind(1)).toBe('depart')
    expect(maneuverKind(4)).toBe('arrive')
    expect(maneuverKind(10)).toBe('right')
    expect(maneuverKind(15)).toBe('left')
    expect(maneuverKind(9)).toBe('slight-right')
    expect(maneuverKind(16)).toBe('slight-left')
    expect(maneuverKind(26)).toBe('roundabout')
    expect(maneuverKind(8)).toBe('straight')
    expect(maneuverKind(999)).toBe('straight')
  })
})

describe('parseValhallaResponse', () => {
  it('converts km to metres and keeps localized instructions', () => {
    const route = parseValhallaResponse({
      trip: {
        legs: [
          {
            shape: encode(SIBIU),
            summary: { length: 0.42, time: 300 },
            maneuvers: [
              { type: 2, instruction: 'Mergeți spre est.', length: 0.3, time: 216 },
              { type: 15, instruction: 'Virați la stânga.', length: 0.12, time: 84 },
              { type: 4, instruction: 'Ați ajuns.', length: 0, time: 0 },
            ],
          },
        ],
      },
    })
    expect(route).not.toBeNull()
    expect(route!.distanceM).toBeCloseTo(420)
    expect(route!.approximate).toBe(false)
    expect(route!.geometry).toHaveLength(3)
    expect(route!.steps.map((s) => s.kind)).toEqual(['depart', 'left', 'arrive'])
    expect(route!.steps[1]!.instruction).toBe('Virați la stânga.')
    expect(route!.steps[0]!.distanceM).toBeCloseTo(300)
  })

  it('returns null when there is no leg', () => {
    expect(parseValhallaResponse({})).toBeNull()
  })
})

describe('fetchWalkingRoute', () => {
  afterEach(() => vi.unstubAllGlobals())

  const start = { lat: 45.797, lon: 24.152 }
  const end = { lat: 45.7962, lon: 24.1544 }

  it('asks for a pedestrian route in the UI language', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        trip: { legs: [{ shape: encode(SIBIU), summary: { length: 0.2, time: 150 }, maneuvers: [] }] },
      }),
    })
    vi.stubGlobal('fetch', fetchMock)

    const route = await fetchWalkingRoute(start, end, 'de')
    const body = JSON.parse(fetchMock.mock.calls[0]![1].body)
    expect(body.costing).toBe('pedestrian')
    expect(body.directions_options.language).toBe('de-DE')
    expect(route.approximate).toBe(false)
  })

  it('falls back to a straight line when the server fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')))
    const route = await fetchWalkingRoute(start, end)
    expect(route.approximate).toBe(true)
    expect(route.geometry).toEqual([[start.lat, start.lon], [end.lat, end.lon]])
  })
})

describe('straightLineRoute', () => {
  it('estimates duration at 5 km/h', () => {
    const route = straightLineRoute({ lat: 45.79, lon: 24.15 }, { lat: 45.799, lon: 24.15 })
    // ~1 km north → ~12 minutes
    expect(route.distanceM).toBeGreaterThan(990)
    expect(route.distanceM).toBeLessThan(1010)
    expect(route.durationS / 60).toBeCloseTo(12, 0)
  })
})
