import type { TwinCompareRequest, TwinRunResult, TwinSample, TwinScenarioRequest, TwinStrategy, TwinStrategySummary } from '@/types/twin'

// Logica pură a panoului digital twin: construirea scenariului și geometria graficului.

export interface TwinForm {
  routeId: number | null
  busCount: number
  durationMinutes: number
  useLive: boolean
  disturbance: boolean
  /** minutul la care se produce perturbarea */
  disturbanceAtMinute: number
  /** cât stă autobuzul #1 pe loc (minute) */
  disturbanceHoldMinutes: number
  seed: number
  /** cum se stabilește flota: număr fix de autobuze sau frecvența dorită */
  fleetMode?: 'buses' | 'headway'
  headwayMinutes?: number
  /** what-if: autobuze adăugate în timpul simulării */
  extraBuses?: boolean
  extraBusesCount?: number
  extraBusesAtMinute?: number
  /** what-if: o stradă închisă (km pe sensul dus) cu ocol */
  closure?: boolean
  closureFromKm?: number
  closureToKm?: number
  closureDetourKm?: number
  closureFromMinute?: number
  /** 0 sau gol = până la final */
  closureToMinute?: number
}

export const MIN_BUSES = 2
export const MAX_BUSES = 12
/** pragul sub care o pereche e considerată „lipită” (același ca în backend: BunchingOptions.EnterRatio) */
export const BUNCHING_THRESHOLD = 0.35

/** Cererea pentru backend, sau null dacă formularul nu e valid (fără linie, durată, perturbare în afara rulării). */
export function buildScenario(form: TwinForm): TwinScenarioRequest | null {
  if (form.routeId == null) return null
  const durationSeconds = Math.round(form.durationMinutes * 60)
  if (durationSeconds < 60) return null

  const disturbances = []
  if (form.disturbance) {
    const atSecond = Math.round(form.disturbanceAtMinute * 60)
    const holdSeconds = Math.round(form.disturbanceHoldMinutes * 60)
    if (atSecond < 0 || atSecond > durationSeconds || holdSeconds < 1) return null
    disturbances.push({ atSecond, busIndex: 0, holdSeconds })
  }

  const byHeadway = !form.useLive && form.fleetMode === 'headway'
  if (byHeadway && !(form.headwayMinutes != null && form.headwayMinutes >= 2 && form.headwayMinutes <= 120)) return null

  const extraBuses = []
  if (form.extraBuses) {
    const atSecond = Math.round((form.extraBusesAtMinute ?? 0) * 60)
    const count = Math.round(form.extraBusesCount ?? 0)
    if (atSecond < 0 || atSecond > durationSeconds || count < 1 || count > 10) return null
    extraBuses.push({ atSecond, count })
  }

  const closures = []
  if (form.closure) {
    const fromMeters = Math.round((form.closureFromKm ?? 0) * 1000)
    const toMeters = Math.round((form.closureToKm ?? 0) * 1000)
    const detourMeters = Math.round((form.closureDetourKm ?? 0) * 1000)
    const fromSecond = Math.round((form.closureFromMinute ?? 0) * 60)
    const toSecond = form.closureToMinute ? Math.round(form.closureToMinute * 60) : undefined
    if (fromMeters < 0 || toMeters <= fromMeters || detourMeters < 0 || detourMeters > 20_000) return null
    if (toSecond != null && toSecond <= fromSecond) return null
    closures.push({ fromMeters, toMeters, detourMeters, fromSecond, ...(toSecond != null ? { toSecond } : {}) })
  }

  return {
    routeId: form.routeId,
    busCount: form.useLive || byHeadway ? undefined : Math.min(MAX_BUSES, Math.max(MIN_BUSES, Math.round(form.busCount))),
    ...(byHeadway ? { headwayMinutes: form.headwayMinutes } : {}),
    durationSeconds,
    seed: form.seed,
    useLiveState: form.useLive,
    disturbances,
    extraBuses,
    closures
  }
}

/** Punctele unei polilinii SVG ("x,y x,y ...") pentru o serie, scalate în `width` × `height` (y crește în jos). */
export function linePoints(
  series: TwinSample[],
  durationSeconds: number,
  width: number,
  height: number,
  yMax: number,
  value: (s: TwinSample) => number = s => s.minRatio
): string {
  if (series.length === 0 || durationSeconds <= 0 || yMax <= 0) return ''
  return series
    .map(s => {
      const x = (s.t / durationSeconds) * width
      const y = height - Math.min(Math.max(value(s), 0), yMax) / yMax * height
      return `${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')
}

/** Cel mai mare minRatio din serii, rotunjit în sus la 0,1 (minim 1), ca să încapă toate seriile. */
export function chartYMax(seriesList: TwinSample[][]): number {
  const max = Math.max(1, ...seriesList.flatMap(series => series.map(s => s.minRatio)))
  return Math.ceil(max * 10) / 10
}

/** Eticheta unei rulări pentru istoric, ex. „4 min la min 10 · 3 autobuze”. */
export function runLabel(form: TwinForm, result: TwinRunResult): string {
  const parts = [form.disturbance ? `${form.disturbanceHoldMinutes} min la min ${form.disturbanceAtMinute}` : 'fără perturbare']
  parts.push(form.fleetMode === 'headway' && !form.useLive
    ? `la ${form.headwayMinutes} min (${result.busCount} autobuze)`
    : `${result.busCount} autobuze`)
  if (form.extraBuses) parts.push(`+${form.extraBusesCount} la min ${form.extraBusesAtMinute}`)
  if (form.closure) parts.push(`stradă închisă km ${form.closureFromKm}–${form.closureToKm}`)
  if (result.initialisedFrom === 'live') parts.push('live')
  return parts.join(' · ')
}

export const STRATEGIES: TwinStrategy[] = ['none', 'headway', 'equalize', 'rl']
export const DEFAULT_REPLICATIONS = 5

/** Cererea de comparare a strategiilor (același scenariu ca „Rulează”), sau null dacă formularul nu e valid. */
export function buildCompareRequest(form: TwinForm, replications = DEFAULT_REPLICATIONS): TwinCompareRequest | null {
  const scenario = buildScenario(form)
  return scenario ? { ...scenario, strategies: STRATEGIES, replications } : null
}

/** Cu cât scade `value` față de `base`, în procente întregi (0 dacă baza e 0; negativ dacă a crescut). */
export function reductionPercent(base: number, value: number): number {
  if (base <= 0) return 0
  return Math.round(((base - value) / base) * 100)
}

/**
 * Strategia cu cea mai mică întârziere totală pe pasager (așteptare + timp pierdut în autobuz din holding);
 * la egalitate, cea cu headway mai regulat. null dacă lista e goală.
 */
export function bestStrategy(results: TwinStrategySummary[]): TwinStrategySummary | null {
  if (results.length === 0) return null
  return [...results].sort((a, b) =>
    a.totalDelayPerPassengerSeconds - b.totalDelayPerPassengerSeconds || a.headwayCv - b.headwayCv)[0]!
}
