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
    // Try admin token first, then fall back to regular token
    const adminToken = localStorage.getItem('admin_token')
    const regularToken = localStorage.getItem('token')
    const token = adminToken || regularToken
    
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    console.log('🔑 Admin API Request:', config.method?.toUpperCase(), config.url, 'Token:', token ? 'Present' : 'Missing')
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
    console.error('❌ Admin API Error:', error.response?.status, error.response?.data)
    if (error.response?.status === 401) {
      // Token invalid sau expirat - logout
      console.warn('⚠️ Unauthorized - redirecting to login')
      localStorage.removeItem('admin_token')
      localStorage.removeItem('admin_user')
      localStorage.removeItem('token')
      localStorage.removeItem('user')
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

export interface Bus {
  id: number
  licensePlate: string
  internalName: string
  currentRouteId: number | null
  currentRouteName?: string | null
  currentRouteNumber?: string | null
}

export interface AdminTicket {
  id: number
  ticketType: string
  priceRon: number
  status: string
  purchasedAt: string
  validFrom: string
  validUntil: string
  qrToken: string
  username: string | null
  userId: number
  payment: {
    id: number
    amount: number
    cardLast4: string
    cardBrand: string
    status: string
  } | null
}

export interface AdminUser {
  id: number
  username: string
  role: string
  createdAt: string
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
    // Clear both admin and regular user tokens
    localStorage.removeItem('admin_token')
    localStorage.removeItem('admin_user')
    localStorage.removeItem('token')
    localStorage.removeItem('user')
  },

  isAuthenticated(): boolean {
    // Check for both admin and regular user tokens
    return !!(localStorage.getItem('admin_token') || localStorage.getItem('token'))
  },

  getUser(): { username: string; role: string } | null {
    // Try admin user first, then regular user
    const adminUserStr = localStorage.getItem('admin_user')
    if (adminUserStr) {
      return JSON.parse(adminUserStr)
    }
    
    const userStr = localStorage.getItem('user')
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

const adminUsersService = {
  async getUsers(): Promise<AdminUser[]> {
    const response = await adminApi.get('/admin/users')
    return response.data
  },

  async updateRole(id: number, role: string): Promise<void> {
    await adminApi.patch(`/admin/users/${id}/role`, { role })
  },

  async deleteUser(id: number): Promise<void> {
    await adminApi.delete(`/admin/users/${id}`)
  }
}

const adminBusesService = {
  async getBuses(): Promise<Bus[]> {
    const response = await adminApi.get('/admin/buses')
    return response.data
  },

  async createBus(bus: Omit<Bus, 'id' | 'currentRouteName' | 'currentRouteNumber'>): Promise<Bus> {
    const response = await adminApi.post('/admin/buses', bus)
    return response.data
  },

  async updateBus(id: number, bus: Omit<Bus, 'currentRouteName' | 'currentRouteNumber'>): Promise<void> {
    await adminApi.put(`/admin/buses/${id}`, bus)
  },

  async deleteBus(id: number): Promise<void> {
    await adminApi.delete(`/admin/buses/${id}`)
  }
}

const adminTicketsService = {
  async getTickets(): Promise<AdminTicket[]> {
    const response = await adminApi.get('/admin/tickets')
    return response.data
  }
}

export { authService, adminRoutesService, adminStationsService, adminUsersService, adminBusesService, adminTicketsService }
