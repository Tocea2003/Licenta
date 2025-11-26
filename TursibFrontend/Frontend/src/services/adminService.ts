import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

// Axios instance pentru admin API cu JWT
const adminApi = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Interceptor pentru a adăuga JWT token la fiecare cerere
adminApi.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('admin_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Interceptor pentru a gestiona erorile de autentificare
adminApi.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token invalid sau expirat - logout
      localStorage.removeItem('admin_token')
      localStorage.removeItem('admin_user')
      window.location.href = '/loginadmin'
    }
    return Promise.reject(error)
  }
)

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  token: string
  username: string
  role: string
  expiresAt: string
}

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

export interface RouteStation {
  routeId: number
  stationId: number
  order: number
  station?: Station
}

const authService = {
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    const response = await axios.post(`${API_BASE_URL}/Auth/login`, credentials)
    return response.data
  },

  async register(credentials: LoginRequest): Promise<any> {
    const response = await axios.post(`${API_BASE_URL}/Auth/register`, credentials)
    return response.data
  },

  logout() {
    localStorage.removeItem('admin_token')
    localStorage.removeItem('admin_user')
  },

  isAuthenticated(): boolean {
    return !!localStorage.getItem('admin_token')
  },

  getUser(): { username: string; role: string } | null {
    const userStr = localStorage.getItem('admin_user')
    return userStr ? JSON.parse(userStr) : null
  }
}

const adminRoutesService = {
  async getRoutes(): Promise<Route[]> {
    const response = await adminApi.get('/admin/routes')
    console.log('🎨 Routes from API:', response.data)
    return response.data
  },

  async getRoute(id: number): Promise<Route> {
    const response = await adminApi.get(`/Routes/${id}`)
    return response.data
  },

  async updateRoute(id: number, route: Partial<Route>): Promise<void> {
    await adminApi.put(`/admin/routes/${id}`, route)
  },

  async createRoute(route: Omit<Route, 'id'>): Promise<Route> {
    const response = await adminApi.post('/admin/routes', route)
    return response.data
  },

  async deleteRoute(id: number): Promise<void> {
    await adminApi.delete(`/admin/routes/${id}`)
  },

  async updateRouteColor(id: number, color: string): Promise<void> {
    await adminApi.patch(`/admin/routes/${id}/color`, { color })
  },

  async getRouteStations(id: number): Promise<RouteStation[]> {
    const response = await adminApi.get(`/Routes/${id}/stations`)
    return response.data
  },

  async addStationToRoute(routeId: number, stationId: number, order: number): Promise<void> {
    await adminApi.post(`/admin/routes/${routeId}/stations`, { stationId, order })
  },

  async removeStationFromRoute(routeId: number, stationId: number): Promise<void> {
    await adminApi.delete(`/admin/routes/${routeId}/stations/${stationId}`)
  },

  async reorderStations(routeId: number, stationOrders: { stationId: number; order: number }[]): Promise<void> {
    await adminApi.put(`/admin/routes/${routeId}/stations/reorder`, stationOrders)
  }
}

const adminStationsService = {
  async getStations(): Promise<Station[]> {
    const response = await adminApi.get('/Stations')
    return response.data
  },

  async createStation(station: Omit<Station, 'id'>): Promise<Station> {
    const response = await adminApi.post('/admin/stations', station)
    return response.data
  },

  async updateStation(id: number, station: Partial<Station>): Promise<void> {
    await adminApi.put(`/admin/stations/${id}`, station)
  },

  async deleteStation(id: number): Promise<void> {
    await adminApi.delete(`/admin/stations/${id}`)
  }
}

export { authService, adminRoutesService, adminStationsService }
