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
