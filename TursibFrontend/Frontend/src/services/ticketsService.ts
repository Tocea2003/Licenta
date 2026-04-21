import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

const ticketsApi = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' }
})

ticketsApi.interceptors.request.use((config) => {
  const token = localStorage.getItem('token') || localStorage.getItem('admin_token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

ticketsApi.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      // Redirect to regular login (not admin login)
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

export interface PaymentInfo {
  id: number
  amount: number
  currency: string
  cardLast4: string
  cardBrand: string
  status: string
}

export interface Ticket {
  id: number
  ticketType: string
  priceRon: number
  status: string
  purchasedAt: string
  validFrom: string
  validUntil: string
  qrToken: string
  payment?: PaymentInfo | null
}

export interface PurchaseRequest {
  ticketType: string
  cardNumber: string
  expiryMonth: string
  expiryYear: string
  cvv: string
  cardholderName: string
}

const ticketsService = {
  async purchase(req: PurchaseRequest): Promise<Ticket> {
    const response = await ticketsApi.post<Ticket>('/tickets/purchase', req)
    return response.data
  },

  async getMyTickets(): Promise<Ticket[]> {
    const response = await ticketsApi.get<Ticket[]>('/tickets')
    return response.data
  },

  async getTicket(id: number): Promise<Ticket> {
    const response = await ticketsApi.get<Ticket>(`/tickets/${id}`)
    return response.data
  }
}

export default ticketsService
