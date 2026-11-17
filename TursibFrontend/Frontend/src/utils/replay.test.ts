import { describe, it, expect } from 'vitest'
import {
  advancePlayhead, buildTracks, formatClock, formatElapsed, frameForTrack, framesAt, interpolateHeading,
  parseHistory, timeBounds, trailFor, windowRange, type ReplayPoint
} from './replay'

const point = (id: string, timestamp: number, latitude: number, extra: Partial<ReplayPoint> = {}): ReplayPoint => ({
  id, routeId: 5, latitude, longitude: 24.1, heading: 0, speed: 0, timestamp, ...extra
})

describe('parseHistory', () => {
  it('keeps valid rows, fills defaults and drops malformed ones', () => {
    const rows = parseHistory([
      { id: 'a', routeId: 5, latitude: 45.7, longitude: 24.1, timestamp: 100, speed: 20, heading: 90, occupancy: null, status: 'in_transit' },
      { id: 'b', routeId: 5, latitude: 45.7, longitude: 24.1, timestamp: 100 },
      { id: 'c', latitude: 45.7 },
      null
    ])
    expect(rows).toHaveLength(2)
    expect(rows[0]).toMatchObject({ id: 'a', speed: 20, heading: 90, status: 'in_transit' })
    expect(rows[0]!.occupancy).toBeUndefined()
    expect(rows[1]).toMatchObject({ speed: 0, heading: 0 })
    expect(parseHistory({})).toEqual([])
  })
})

describe('buildTracks / timeBounds', () => {
  const tracks = buildTracks([point('b', 30, 1), point('a', 20, 1), point('a', 10, 1), point('b', 50, 1)])

  it('groups per vehicle and sorts chronologically', () => {
    expect(tracks.map(t => t.id)).toEqual(['a', 'b'])
    expect(tracks[0]!.points.map(p => p.timestamp)).toEqual([10, 20])
  })

  it('finds the overall time range', () => {
    expect(timeBounds(tracks)).toEqual({ start: 10, end: 50 })
    expect(timeBounds([])).toBeNull()
  })
})

describe('interpolateHeading', () => {
  it('takes the shortest way around the circle', () => {
    expect(interpolateHeading(350, 10, 0.5)).toBeCloseTo(0, 5)
    expect(interpolateHeading(10, 350, 0.5)).toBeCloseTo(0, 5)
    expect(interpolateHeading(0, 90, 0.5)).toBeCloseTo(45, 5)
  })
})

describe('frameForTrack', () => {
  const track = buildTracks([point('a', 100, 45.0, { heading: 0, speed: 10 }), point('a', 110, 45.1, { heading: 90, speed: 30 })])[0]!

  it('interpolates linearly between the two surrounding samples', () => {
    const frame = frameForTrack(track, 105)!
    expect(frame.latitude).toBeCloseTo(45.05, 6)
    expect(frame.heading).toBeCloseTo(45, 6)
    expect(frame.speed).toBeCloseTo(20, 6)
  })

  it('returns the exact sample at its timestamp, and null outside the track', () => {
    expect(frameForTrack(track, 100)!.latitude).toBe(45.0)
    expect(frameForTrack(track, 110)!.latitude).toBe(45.1)
    expect(frameForTrack(track, 99)).toBeNull()
    expect(frameForTrack(track, 111)).toBeNull()
  })

  it('does not invent positions across a long reporting gap', () => {
    const gappy = buildTracks([point('a', 100, 45.0), point('a', 400, 45.5)])[0]!
    expect(frameForTrack(gappy, 110)!.latitude).toBe(45.0)     // încă aproape de ultimul eșantion
    expect(frameForTrack(gappy, 390)!.latitude).toBe(45.5)     // deja aproape de următorul
    expect(frameForTrack(gappy, 250)).toBeNull()               // în mijlocul golului: ascuns
  })
})

describe('framesAt / trailFor', () => {
  const tracks = buildTracks([
    point('a', 0, 45.0), point('a', 10, 45.1), point('a', 20, 45.2),
    point('b', 100, 46.0), point('b', 110, 46.1)
  ])

  it('lists only the vehicles active at that time', () => {
    expect(framesAt(tracks, 15).map(f => f.id)).toEqual(['a'])
    expect(framesAt(tracks, 105).map(f => f.id)).toEqual(['b'])
    expect(framesAt(tracks, 50)).toEqual([])
  })

  it('builds a trail of the recent past ending at the current position', () => {
    const trail = trailFor(tracks[0]!, 15, 12)
    expect(trail).toHaveLength(2)
    expect(trail[0]![0]).toBeCloseTo(45.1, 9)
    expect(trail[1]![0]).toBeCloseTo(45.15, 9)
    expect(trailFor(tracks[0]!, 500, 12)).toEqual([])
  })
})

describe('playback helpers', () => {
  it('advances by real time multiplied by the speed and stops at the end', () => {
    expect(advancePlayhead(100, 1000, 15, 500)).toEqual({ t: 115, ended: false })
    expect(advancePlayhead(495, 1000, 15, 500)).toEqual({ t: 500, ended: true })
  })

  it('formats clock and elapsed time', () => {
    expect(formatElapsed(125)).toBe('2:05')
    expect(formatElapsed(3725)).toBe('1:02:05')
    expect(formatElapsed(-3)).toBe('0:00')
    expect(formatClock(0)).toMatch(/^\d{2}:\d{2}:\d{2}$/)
  })

  it('computes the window to load', () => {
    expect(windowRange('15', 10_000)).toEqual({ from: 9_100, to: 10_000 })
    expect(windowRange('180', 100_000)).toEqual({ from: 89_200, to: 100_000 })
    expect(windowRange('latest', 0, { first: 1_000, last: 9_000 })).toEqual({ from: 5_400, to: 9_001 })
    expect(windowRange('latest', 0, { first: 8_000, last: 9_000 })).toEqual({ from: 8_000, to: 9_001 })
    expect(windowRange('latest', 0, null)).toBeNull()
  })
})
