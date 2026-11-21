/** Perturbare programată în digital twin: autobuzul stă pe loc un timp. */
export interface TwinDisturbance {
  atSecond: number
  busIndex: number
  holdSeconds: number
}

/** Corpul cererii POST /api/twin/run */
export interface TwinScenarioRequest {
  routeId: number
  busCount?: number
  durationSeconds: number
  seed: number
  useLiveState: boolean
  disturbances: TwinDisturbance[]
  /** strategia de control (implicit `none`) */
  strategy?: TwinStrategy
  /** what-if: frecvența dorită (un autobuz la N minute), în loc de busCount */
  headwayMinutes?: number
  /** what-if: autobuze adăugate în timpul simulării */
  extraBuses?: Array<{ atSecond: number; count: number }>
  /** what-if: străzi închise (pe sensul dus, aplicate ambelor sensuri), cu ocol */
  closures?: Array<{ fromMeters: number; toMeters: number; detourMeters: number; fromSecond?: number; toSecond?: number }>
}

/** Strategii de control a bunching-ului (holding la stație). */
export type TwinStrategy = 'none' | 'headway' | 'equalize' | 'rl'

/** Corpul cererii POST /api/twin/compare: același scenariu, mai multe strategii, mai multe seed-uri. */
export interface TwinCompareRequest extends TwinScenarioRequest {
  strategies?: TwinStrategy[]
  replications: number
}

/** Rezultatul unei strategii, mediat pe mai multe rulări. */
export interface TwinStrategySummary {
  strategy: TwinStrategy
  bunchedPercent: number
  meanCv: number
  minRatio: number
  bunchingEpisodes: number
  /** secunde de holding pe autobuz și pe oră: costul pentru pasagerii din autobuz */
  holdSecondsPerBusHour: number
  averageSpeedKmh: number
  series: TwinSample[]
  meanWaitSeconds: number
  excessWaitSeconds: number
  headwayCv: number
  /** metrica principală: așteptare în stație + întârziere în autobuz din holding, pe pasager (s) */
  totalDelayPerPassengerSeconds: number
  /** abaterea standard între replicări (s) */
  totalDelayStdSeconds: number
}

export interface TwinComparison {
  routeId: number
  initialisedFrom: 'live' | 'even'
  busCount: number
  durationSeconds: number
  replications: number
  results: TwinStrategySummary[]
}

export interface TwinSample {
  /** secunde de la începutul simulării */
  t: number
  /** cel mai mic raport gol/ideal dintre perechile consecutive */
  minRatio: number
  /** coeficientul de variație al golurilor */
  cv: number
  bunched: boolean
}

export interface TwinRunResult {
  routeId: number
  /** `live` = pornit de la pozițiile reale, `even` = echidistant */
  initialisedFrom: 'live' | 'even'
  busCount: number
  durationSeconds: number
  routeLengthMeters: number
  bunchedPercent: number
  meanCv: number
  maxCv: number
  minRatio: number
  bunchingEpisodes: number
  series: TwinSample[]
  finalLoopPositions: number[]
  strategy?: TwinStrategy
  holdSecondsPerBusHour?: number
  averageSpeedKmh?: number
  /** așteptarea medie în stație pe pasager (s) */
  meanWaitSeconds?: number
  /** așteptarea în plus față de autobuze perfect regulate (s) */
  excessWaitSeconds?: number
  /** coeficientul de variație al headway-ului în timp */
  headwayCv?: number
  meanHeadwaySeconds?: number
  /** așteptare + întârziere în autobuz din holding, pe pasager (s) */
  totalDelayPerPassengerSeconds?: number
  finalBusCount?: number
  skippedStopVisits?: number
}

/** O rulare păstrată în istoricul panoului, ca să poată fi comparată cu altele. */
export interface TwinRunEntry {
  id: number
  label: string
  color: string
  result: TwinRunResult
}
