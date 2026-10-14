import { describe, it, expect } from 'vitest'
import { haversineKm, bearingDeg, angleDiff, findNearestStation, trimShapeToStations } from './geo'

describe('haversineKm', () => {
  it('returns 0 for the same point', () => {
    expect(haversineKm(45.79, 24.15, 45.79, 24.15)).toBe(0)
  })

  it('measures one degree of latitude as ≈ 111.19 km', () => {
    expect(haversineKm(45, 24, 46, 24)).toBeCloseTo(111.19, 1)
  })

  it('is symmetric', () => {
    const ab = haversineKm(45.76, 24.13, 45.8, 24.17)
    const ba = haversineKm(45.8, 24.17, 45.76, 24.13)
    expect(ab).toBeCloseTo(ba, 10)
  })
})

describe('bearingDeg', () => {
  it('is 0° when heading north and 90° when heading east', () => {
    expect(bearingDeg(45, 24, 46, 24)).toBeCloseTo(0, 5)
    expect(bearingDeg(0, 24, 0, 25)).toBeCloseTo(90, 5)
  })

  it('is 180° when heading south', () => {
    expect(bearingDeg(46, 24, 45, 24)).toBeCloseTo(180, 5)
  })
})

describe('angleDiff', () => {
  it('wraps around 360°', () => {
    expect(angleDiff(350, 10)).toBe(20)
    expect(angleDiff(10, 350)).toBe(20)
  })

  it('is at most 180°', () => {
    expect(angleDiff(0, 180)).toBe(180)
    expect(angleDiff(90, 270)).toBe(180)
  })
})

describe('findNearestStation', () => {
  const stations = [
    { id: 1, latitude: 45.79, longitude: 24.15 },
    { id: 2, latitude: 45.80, longitude: 24.15 },
    { id: 3, latitude: 45.81, longitude: 24.15 }
  ]

  it('returns the closest station', () => {
    expect(findNearestStation(45.801, 24.15, stations)?.id).toBe(2)
  })

  it('returns null for an empty list', () => {
    expect(findNearestStation(45.8, 24.15, [])).toBeNull()
  })
})

describe('trimShapeToStations', () => {
  const shape = [0, 1, 2, 3, 4, 5].map(i => ({ latitude: 45 + i * 0.01, longitude: 24 }))

  it('keeps only the points between the two stations', () => {
    const result = trimShapeToStations(shape, { latitude: 45.01, longitude: 24 }, { latitude: 45.03, longitude: 24 })
    expect(result).toEqual([[45.01, 24], [45.02, 24], [45.03, 24]])
  })

  it('works when the stations are given in reverse order', () => {
    const result = trimShapeToStations(shape, { latitude: 45.03, longitude: 24 }, { latitude: 45.01, longitude: 24 })
    expect(result).toHaveLength(3)
  })

  it('returns [] for an empty shape', () => {
    expect(trimShapeToStations([], { latitude: 45, longitude: 24 }, { latitude: 46, longitude: 24 })).toEqual([])
  })
})
