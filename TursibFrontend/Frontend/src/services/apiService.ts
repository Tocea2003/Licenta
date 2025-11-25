import axios from 'axios'

// URL-ul backend-ului .NET (portul pe care rulează API-ul)
// Folosește VITE_API_URL din .env sau localhost ca fallback
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

// Configurarea instanței axios
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

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

// Serviciu API cu toate metodele pentru comunicare cu backend-ul
export default {
  // ========== ROUTES ==========
  
  // GET /api/routes - Returnează toate traseele
  async getRoutes(): Promise<Route[]> {
    const response = await apiClient.get<Route[]>('/routes')
    return response.data
  },

  // GET /api/routes/{id} - Returnează un traseu specific
  async getRoute(id: number): Promise<Route> {
    const response = await apiClient.get<Route>(`/routes/${id}`)
    return response.data
  },

  // GET /api/routes/{id}/stations - Returnează stațiile unui traseu (ORDONATE)
  async getRouteStations(routeId: number): Promise<Station[]> {
    const response = await apiClient.get<Station[]>(`/routes/${routeId}/stations`)
    return response.data
  },

  // ========== STATIONS ==========
  
  // GET /api/stations - Returnează toate stațiile
  async getStations(): Promise<Station[]> {
    const response = await apiClient.get<Station[]>('/stations')
    return response.data
  },

  // GET /api/stations/{id} - Returnează o stație specifică
  async getStation(id: number): Promise<Station> {
    const response = await apiClient.get<Station>(`/stations/${id}`)
    return response.data
  },

  // ========== BUSES ==========
  
  // GET /api/buses - Returnează toate autobuzele
  async getBuses(): Promise<Bus[]> {
    const response = await apiClient.get<Bus[]>('/buses')
    return response.data
  },

  // GET /api/buses/{id} - Returnează un autobuz specific
  async getBus(id: number): Promise<Bus> {
    const response = await apiClient.get<Bus>(`/buses/${id}`)
    return response.data
  },

  // ========== SHAPES (GTFS) ==========
  
  // GET /api/shapes/route/{routeId} - Returnează shape-ul (traseul exact pe străzi) pentru un traseu
  async getRouteShape(routeId: number): Promise<RouteShape> {
    const response = await apiClient.get<RouteShape>(`/shapes/route/${routeId}`)
    return response.data
  },

  // GET /api/shapes/route/{routeId}/segment - Returnează segmentul de traseu între două stații
  async getRouteSegment(routeId: number, fromStationId: number, toStationId: number): Promise<RouteShape> {
    const response = await apiClient.get<RouteShape>(
      `/shapes/route/${routeId}/segment?fromStationId=${fromStationId}&toStationId=${toStationId}`
    )
    return response.data
  }
}