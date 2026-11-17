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
}

/** O rulare păstrată în istoricul panoului, ca să poată fi comparată cu altele. */
export interface TwinRunEntry {
  id: number
  label: string
  color: string
  result: TwinRunResult
}
