import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' }
})

// Cache in-memory cu TTL pentru răspunsuri statice (routes, stations, shapes)
const CACHE_TTL_MS = 5 * 60 * 1000 // 5 minute
const _cache = new Map<string, { data: unknown; expires: number }>()

function getCached<T>(key: string): T | null {
  const entry = _cache.get(key)
  if (entry && Date.now() < entry.expires) return entry.data as T
  _cache.delete(key)
  return null
}

function setCached<T>(key: string, data: T, ttl = CACHE_TTL_MS): T {
  _cache.set(key, { data, expires: Date.now() + ttl })
  return data
}

/** Șterge cache-ul (util după operații de admin) */
export function clearApiCache(prefix?: string) {
  if (!prefix) { _cache.clear(); return }
  for (const key of _cache.keys()) {
    if (key.startsWith(prefix)) _cache.delete(key)
  }
}

// Interfețe TypeScript pentru datele din API
export interface Route {
  id: number
  routeNumber: string
  name: string
  color?: string
}

export interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

export interface StationScheduleEntry {
  routeId: number
  routeNumber: string
  routeName: string
  routeColor?: string
  direction?: string
  directionId: number
  arrivalTime: string
  departureTime: string
}

export interface Bus {
  id: number
  licensePlate: string
  internalName: string
  currentRouteId?: number
  currentRoute?: Route
}

export interface ShapePoint {
  latitude: number
  longitude: number
  sequence: number
}

export interface RouteShape {
  routeId: number
  shapeId: string
  directionId: number
  points: ShapePoint[]
}

export default {
  // ========== ROUTES ==========

  async getRoutes(): Promise<Route[]> {
    const cached = getCached<Route[]>('routes')
    if (cached) return cached
    const { data } = await apiClient.get<Route[]>('/routes')
    return setCached('routes', data)
  },

  async getRoute(id: number): Promise<Route> {
    const key = `route_${id}`
    const cached = getCached<Route>(key)
    if (cached) return cached
    const { data } = await apiClient.get<Route>(`/routes/${id}`)
    return setCached(key, data)
  },

  async getRouteStations(routeId: number): Promise<Station[]> {
    const key = `route_stations_${routeId}`
    const cached = getCached<Station[]>(key)
    if (cached) return cached
    const { data } = await apiClient.get<Station[]>(`/routes/${routeId}/stations`)
    return setCached(key, data)
  },

  // ========== STATIONS ==========

  async getStations(): Promise<Station[]> {
    const cached = getCached<Station[]>('stations')
    if (cached) return cached
    const { data } = await apiClient.get<Station[]>('/stations')
    return setCached('stations', data)
  },

  async getStation(id: number): Promise<Station> {
    const key = `station_${id}`
    const cached = getCached<Station>(key)
    if (cached) return cached
    const { data } = await apiClient.get<Station>(`/stations/${id}`)
    return setCached(key, data)
  },

  async getStationRoutes(stationId: number): Promise<Route[]> {
    const key = `station_routes_${stationId}`
    const cached = getCached<Route[]>(key)
    if (cached) return cached
    const { data } = await apiClient.get<Route[]>(`/stations/${stationId}/routes`)
    return setCached(key, data)
  },

  async getStationSchedule(stationId: number): Promise<StationScheduleEntry[]> {
    const key = `station_schedule_${stationId}`
    const cached = getCached<StationScheduleEntry[]>(key)
    if (cached) return cached
    const { data } = await apiClient.get<StationScheduleEntry[]>(`/stations/${stationId}/schedule`)
    return setCached(key, data, 2 * 60 * 1000) // 2 min (mai dinamic)
  },

  // ========== BUSES ==========

  async getBuses(): Promise<Bus[]> {
    const { data } = await apiClient.get<Bus[]>('/buses')
    return data // nu se cachează - date live
  },

  async getBus(id: number): Promise<Bus> {
    const { data } = await apiClient.get<Bus>(`/buses/${id}`)
    return data
  },

  // ========== SHAPES (GTFS) ==========

  async getRouteShape(routeId: number): Promise<RouteShape> {
    const key = `shape_${routeId}`
    const cached = getCached<RouteShape>(key)
    if (cached) return cached
    const { data } = await apiClient.get<RouteShape>(`/shapes/route/${routeId}`)
    return setCached(key, data, 30 * 60 * 1000) // 30 min - shapes nu se schimbă
  },

  async getRouteSegment(routeId: number, fromStationId: number, toStationId: number): Promise<RouteShape> {
    const key = `segment_${routeId}_${fromStationId}_${toStationId}`
    const cached = getCached<RouteShape>(key)
    if (cached) return cached
    const { data } = await apiClient.get<RouteShape>(
      `/shapes/route/${routeId}/segment?fromStationId=${fromStationId}&toStationId=${toStationId}`
    )
    return setCached(key, data, 30 * 60 * 1000)
  },

  // ========== ROUTING ==========

  async getRouteAlternatives(startStationId: number, endStationId: number, departureTime?: string): Promise<any[]> {
    const { data } = await apiClient.post<any[]>('/routing/alternatives', {
      startStationId,
      endStationId,
      departureTime: departureTime ?? null
    })
    return data // nu se cachează - depinde de timp
  }
}