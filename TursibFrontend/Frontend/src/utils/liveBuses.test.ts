import { describe, it, expect } from 'vitest'
import type { BusLocation } from '@/types/bus'
import { parseBusLocations, selectVisibleBuses, computeStationEtas } from './liveBuses'

const bus = (id: string, routeId: number, latitude: number, extra: Partial<BusLocation> = {}): BusLocation => ({
  id, routeId, latitude, longitude: 24.15, timestamp: 0, speed: 30, heading: 0, ...extra
})

describe('parseBusLocations', () => {
  it('keeps only route_* entries that have coordinates', () => {
    const result = parseBusLocations({
      route_1_bus_0: { latitude: 45.79, longitude: 24.15, routeId: 1, speed: 20, heading: 90, timestamp: 1 },
      route_1_bus_1: { latitude: 0, longitude: 0, routeId: 1 },
      legacy_bus_7: { latitude: 45.8, longitude: 24.1, routeId: 2 }
    })
    expect(result.map(b => b.id)).toEqual(['route_1_bus_0'])
    expect(result[0]).toMatchObject({ routeId: 1, speed: 20, heading: 90 })
  })

  it('returns [] for empty data', () => {
    expect(parseBusLocations(null)).toEqual([])
  })
})

describe('selectVisibleBuses', () => {
  const buses = Array.from({ length: 40 }, (_, i) => bus(`b${i}`, i < 5 ? 7 : 1, 45.79 + i * 0.001))

  it('limits the number of buses by default', () => {
    expect(selectVisibleBuses(buses)).toHaveLength(30)
  })

  it('shows everything when showAll is set', () => {
    expect(selectVisibleBuses(buses, { showAll: true })).toHaveLength(40)
  })

  it('always includes buses on the selected route, plus up to `limit` others', () => {
    const result = selectVisibleBuses(buses, { selectedRouteId: 7, limit: 3 })
    expect(result.filter(b => b.routeId === 7)).toHaveLength(5)
    expect(result).toHaveLength(8)
  })

  it('sorts by distance and drops buses outside the radius when the user location is known', () => {
    const far = bus('far', 1, 47.5) // ~190 km north
    const near = bus('near', 1, 45.791)
    const nearer = bus('nearer', 1, 45.7901)
    const result = selectVisibleBuses([far, near, nearer], { userLocation: { lat: 45.79, lon: 24.15 } })
    expect(result.map(b => b.id)).toEqual(['nearer', 'near'])
  })
})

describe('computeStationEtas', () => {
  const station = { latitude: 45.80, longitude: 24.15 }
  const options = { routeColors: { 1: '#ff0000' }, routeLabel: (id: number) => `Linia ${id}` }

  it('estimates the ETA for a bus heading toward the station', () => {
    // ~1.1 km south of the station, heading north at 30 km/h → ~2 min
    const etas = computeStationEtas(station, [bus('b1', 1, 45.79, { heading: 0, speed: 30 })], options)
    expect(etas).toEqual([{ busId: 'b1', routeId: 1, routeNumber: 'Linia 1', eta: '2 min', color: '#ff0000' }])
  })

  it('ignores buses driving away from the station', () => {
    const etas = computeStationEtas(station, [bus('b1', 1, 45.79, { heading: 180 })], options)
    expect(etas).toEqual([])
  })

  it('keeps only the closest bus per route and sorts by ETA', () => {
    const etas = computeStationEtas(station, [
      bus('far1', 1, 45.78, { heading: 0 }),
      bus('near1', 1, 45.795, { heading: 0 }),
      bus('route2', 2, 45.785, { heading: 0 })
    ], options)
    expect(etas.map(e => e.busId)).toEqual(['near1', 'route2'])
  })

  it('ignores buses already at the stop or more than 4 km away', () => {
    const etas = computeStationEtas(station, [
      bus('atStop', 1, 45.8001, { heading: 0 }),
      bus('tooFar', 2, 45.75, { heading: 0 })
    ], options)
    expect(etas).toEqual([])
  })
})
