/** Poziția live a unui autobuz, așa cum o publică simulatorul (Firebase: bus_locations/route_X_bus_Y). */
export interface BusLocation {
  id: string
  latitude: number
  longitude: number
  routeId: number
  routeNumber?: string
  routeName?: string
  /** secunde Unix */
  timestamp: number
  /** km/h */
  speed: number
  /** grade, 0 = nord */
  heading: number
  /** procent 0-100 */
  occupancy?: number
  status?: 'in_transit' | 'approaching' | 'at_station' | 'departing' | string
  directionId?: number
  nextStationId?: number
  nextStationName?: string
  /** secunde */
  nextStationEta?: number
}

/** Un autobuz care se apropie de o stație, cu timpul estimat de sosire. */
export interface StationEta {
  busId: string
  routeId: number
  routeNumber: string
  eta: string
  color: string
}

/** Alertă de bunching: două autobuze ale aceleiași linii circulă prea aproape (detectată de backend). */
export interface BunchingAlert {
  key: string
  routeId: number
  routeNumber?: string
  /** autobuzul din față */
  leaderId: string
  /** autobuzul care îl urmează prea aproape */
  followerId: string
  /** distanța dintre ele pe traseu (m) */
  gapMeters: number
  /** distanța ideală dacă ar fi echidistante (m) */
  idealGapMeters: number
  /** gapMeters / idealGapMeters */
  ratio: number
  severity: 'warning' | 'critical'
  /** secunde Unix */
  startedAt: number
  /** secunde Unix; lipsește cât timp alerta e activă */
  endedAt?: number
  /** timpul estimat dintre cele două autobuze (s) */
  headwaySeconds?: number
  /** cât ar trebui reținut autobuzul din spate la următoarea stație ca să redea golul (s); doar la alertele active */
  recommendedHoldSeconds?: number
}
