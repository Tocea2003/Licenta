import { describe, it, expect } from 'vitest'
import type { TwinRunResult, TwinSample } from '@/types/twin'
import { buildScenario, chartYMax, linePoints, runLabel, type TwinForm } from './twin'

const form = (extra: Partial<TwinForm> = {}): TwinForm => ({
  routeId: 9, busCount: 3, durationMinutes: 120, useLive: false,
  disturbance: false, disturbanceAtMinute: 10, disturbanceHoldMinutes: 4, seed: 1, ...extra
})

const sample = (t: number, minRatio: number): TwinSample => ({ t, minRatio, cv: 0, bunched: minRatio < 0.35 })
const result = (series: TwinSample[], extra: Partial<TwinRunResult> = {}): TwinRunResult => ({
  routeId: 9, initialisedFrom: 'even', busCount: 3, durationSeconds: 7200, routeLengthMeters: 5000,
  bunchedPercent: 0, meanCv: 0, maxCv: 0, minRatio: 1, bunchingEpisodes: 0, series, finalLoopPositions: [], ...extra
})

describe('buildScenario', () => {
  it('requires a route', () => {
    expect(buildScenario(form({ routeId: null }))).toBeNull()
  })

  it('converts minutes to seconds and omits busCount for live starts', () => {
    expect(buildScenario(form())).toMatchObject({ routeId: 9, busCount: 3, durationSeconds: 7200, useLiveState: false, disturbances: [] })
    expect(buildScenario(form({ useLive: true }))!.busCount).toBeUndefined()
  })

  it('adds the disturbance on bus 0 and rejects one outside the run', () => {
    expect(buildScenario(form({ disturbance: true }))!.disturbances).toEqual([{ atSecond: 600, busIndex: 0, holdSeconds: 240 }])
    expect(buildScenario(form({ disturbance: true, disturbanceAtMinute: 500 }))).toBeNull()
  })

  it('clamps the bus count', () => {
    expect(buildScenario(form({ busCount: 99 }))!.busCount).toBe(12)
    expect(buildScenario(form({ busCount: 0 }))!.busCount).toBe(2)
  })
})

describe('linePoints', () => {
  it('scales time to x and ratio to y (inverted)', () => {
    expect(linePoints([sample(0, 1), sample(3600, 0.5), sample(7200, 0)], 7200, 600, 100, 1))
      .toBe('0.0,0.0 300.0,50.0 600.0,100.0')
  })

  it('clamps values above the maximum and returns "" for empty input', () => {
    expect(linePoints([sample(0, 5)], 100, 100, 100, 1)).toBe('0.0,0.0')
    expect(linePoints([], 100, 100, 100, 1)).toBe('')
  })
})

describe('chartYMax', () => {
  it('is at least 1 and rounds up to a tenth', () => {
    expect(chartYMax([result([sample(0, 0.5)])])).toBe(1)
    expect(chartYMax([result([sample(0, 1.23)])])).toBe(1.3)
  })
})

describe('runLabel', () => {
  it('describes the scenario', () => {
    expect(runLabel(form(), result([]))).toBe('fără perturbare · 3 autobuze')
    expect(runLabel(form({ disturbance: true }), result([], { initialisedFrom: 'live' }))).toBe('4 min la min 10 · 3 autobuze · live')
  })
})
