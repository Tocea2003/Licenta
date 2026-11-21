import { describe, it, expect } from 'vitest'
import type { TwinRunResult, TwinSample, TwinStrategySummary } from '@/types/twin'
import { bestStrategy, buildCompareRequest, buildScenario, chartYMax, linePoints, reductionPercent, runLabel, type TwinForm } from './twin'

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

describe('buildScenario what-if', () => {
  it('uses the frequency instead of the bus count', () => {
    const s = buildScenario(form({ fleetMode: 'headway', headwayMinutes: 8 }))!
    expect(s.busCount).toBeUndefined()
    expect(s.headwayMinutes).toBe(8)
    expect(buildScenario(form({ fleetMode: 'headway', headwayMinutes: 1 }))).toBeNull()
  })

  it('adds extra buses and a closed street in backend units', () => {
    const s = buildScenario(form({
      extraBuses: true, extraBusesCount: 2, extraBusesAtMinute: 30,
      closure: true, closureFromKm: 1.2, closureToKm: 1.8, closureDetourKm: 0.9, closureFromMinute: 10, closureToMinute: 70
    }))!
    expect(s.extraBuses).toEqual([{ atSecond: 1800, count: 2 }])
    expect(s.closures).toEqual([{ fromMeters: 1200, toMeters: 1800, detourMeters: 900, fromSecond: 600, toSecond: 4200 }])
  })

  it('keeps a closure open-ended when no end minute is given, and rejects inverted ranges', () => {
    expect(buildScenario(form({ closure: true, closureFromKm: 1, closureToKm: 2, closureDetourKm: 0.5 }))!.closures![0])
      .not.toHaveProperty('toSecond')
    expect(buildScenario(form({ closure: true, closureFromKm: 2, closureToKm: 1, closureDetourKm: 0.5 }))).toBeNull()
    expect(buildScenario(form({ extraBuses: true, extraBusesCount: 0, extraBusesAtMinute: 5 }))).toBeNull()
  })

  it('describes the what-if in the run label', () => {
    expect(runLabel(form({ fleetMode: 'headway', headwayMinutes: 8, closure: true, closureFromKm: 1, closureToKm: 2 }), result([])))
      .toBe('fără perturbare · la 8 min (3 autobuze) · stradă închisă km 1–2')
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
    expect(chartYMax([[sample(0, 0.5)]])).toBe(1)
    expect(chartYMax([[sample(0, 1.23)]])).toBe(1.3)
  })
})

describe('runLabel', () => {
  it('describes the scenario', () => {
    expect(runLabel(form(), result([]))).toBe('fără perturbare · 3 autobuze')
    expect(runLabel(form({ disturbance: true }), result([], { initialisedFrom: 'live' }))).toBe('4 min la min 10 · 3 autobuze · live')
  })
})

describe('buildCompareRequest', () => {
  it('reuses the scenario and asks for all strategies with several replications', () => {
    expect(buildCompareRequest(form({ disturbance: true }))).toMatchObject({
      routeId: 9, durationSeconds: 7200, strategies: ['none', 'headway', 'equalize', 'rl'], replications: 5,
      disturbances: [{ atSecond: 600, busIndex: 0, holdSeconds: 240 }]
    })
  })

  it('is null when the form is invalid', () => {
    expect(buildCompareRequest(form({ routeId: null }))).toBeNull()
  })
})

describe('reductionPercent', () => {
  it('computes the drop against the baseline', () => {
    expect(reductionPercent(80, 20)).toBe(75)
    expect(reductionPercent(0.4, 0.4)).toBe(0)
    expect(reductionPercent(50, 75)).toBe(-50)
    expect(reductionPercent(0, 10)).toBe(0)
  })
})

describe('bestStrategy', () => {
  const summary = (strategy: TwinStrategySummary['strategy'], delay: number, headwayCv = 0.1): TwinStrategySummary => ({
    strategy, bunchedPercent: 0, meanCv: 0, minRatio: 1, bunchingEpisodes: 0, holdSecondsPerBusHour: 0, averageSpeedKmh: 18, series: [],
    meanWaitSeconds: delay, excessWaitSeconds: 0, headwayCv, totalDelayPerPassengerSeconds: delay, totalDelayStdSeconds: 0
  })

  it('picks the lowest total delay per passenger, then the most regular headway', () => {
    expect(bestStrategy([summary('none', 387), summary('headway', 317), summary('equalize', 320)])!.strategy).toBe('headway')
    expect(bestStrategy([summary('headway', 300, 0.2), summary('equalize', 300, 0.1)])!.strategy).toBe('equalize')
    expect(bestStrategy([])).toBeNull()
  })
})
