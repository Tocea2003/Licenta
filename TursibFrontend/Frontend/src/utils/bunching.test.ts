import { describe, it, expect } from 'vitest'
import type { BunchingAlert } from '@/types/bus'
import { alertDurationSeconds, formatDuration, formatGap, parseBunchingList, sortAlerts } from './bunching'

const alert = (extra: Partial<BunchingAlert> = {}): BunchingAlert => ({
  key: '1:f', routeId: 1, leaderId: 'l', followerId: 'f', gapMeters: 100, idealGapMeters: 1000,
  ratio: 0.1, severity: 'warning', startedAt: 1000, ...extra
})

describe('parseBunchingList', () => {
  it('maps backend alerts and treats null as missing', () => {
    const [a] = parseBunchingList([
      { key: '9:route_9_bus_0', routeId: 9, routeNumber: '9', leaderId: 'route_9_bus_1', followerId: 'route_9_bus_0',
        gapMeters: 100, idealGapMeters: 3333, ratio: 0.03, severity: 'critical', startedAt: 5, endedAt: null, headwaySeconds: 14 }
    ])
    expect(a).toMatchObject({ routeId: 9, severity: 'critical', headwaySeconds: 14 })
    expect(a!.endedAt).toBeUndefined()
  })

  it('ignores malformed entries and non-arrays', () => {
    expect(parseBunchingList([null, 3, { routeId: 1 }])).toEqual([])
    expect(parseBunchingList(undefined)).toEqual([])
  })
})

describe('sortAlerts', () => {
  it('puts critical first, then the smallest ratio', () => {
    const sorted = sortAlerts([
      alert({ key: 'a', ratio: 0.1 }),
      alert({ key: 'b', ratio: 0.3 }),
      alert({ key: 'c', ratio: 0.2, severity: 'critical' })
    ])
    expect(sorted.map(a => a.key)).toEqual(['c', 'a', 'b'])
  })
})

describe('formatting', () => {
  it('formats gaps', () => {
    expect(formatGap(85.4)).toBe('85 m')
    expect(formatGap(1240)).toBe('1,2 km')
  })

  it('formats durations', () => {
    expect(formatDuration(45)).toBe('45 s')
    expect(formatDuration(120)).toBe('2 min')
    expect(formatDuration(150)).toBe('2 min 30 s')
  })

  it('computes alert duration for active and ended alerts', () => {
    expect(alertDurationSeconds(alert(), 1090)).toBe(90)
    expect(alertDurationSeconds(alert({ endedAt: 1030 }), 9999)).toBe(30)
  })
})
