import { describe, it, expect, beforeEach, vi } from 'vitest'

const mockGet = vi.hoisted(() => vi.fn())
const mockPost = vi.hoisted(() => vi.fn())

// Capture interceptor callbacks so we can test them directly
const capturedReqInterceptor = vi.hoisted(() => ({ fn: null as ((config: any) => any) | null }))
const capturedResErrorInterceptor = vi.hoisted(() => ({ fn: null as ((error: any) => any) | null }))

vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => ({
      get: mockGet,
      post: mockPost,
      interceptors: {
        request: {
          use: vi.fn((fn: (config: any) => any) => {
            capturedReqInterceptor.fn = fn
            return 0
          })
        },
        response: {
          use: vi.fn((_successFn: any, errorFn: (error: any) => any) => {
            capturedResErrorInterceptor.fn = errorFn
            return 0
          })
        }
      }
    }))
  }
}))

import ticketsService from '@/services/ticketsService'

describe('ticketsService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    Object.defineProperty(window, 'location', {
      value: { pathname: '/', href: '' },
      writable: true,
      configurable: true
    })
  })

  describe('Request interceptor – Authorization header', () => {
    it('adds Bearer token from the "token" key in localStorage', () => {
      localStorage.setItem('token', 'user-jwt-token')
      const config = { headers: {} as Record<string, string> }

      const result = capturedReqInterceptor.fn!(config)

      expect(result.headers.Authorization).toBe('Bearer user-jwt-token')
    })

    it('falls back to admin_token when "token" is absent', () => {
      localStorage.setItem('admin_token', 'admin-jwt-token')
      const config = { headers: {} as Record<string, string> }

      const result = capturedReqInterceptor.fn!(config)

      expect(result.headers.Authorization).toBe('Bearer admin-jwt-token')
    })

    it('prefers "token" over "admin_token" when both are present', () => {
      localStorage.setItem('token', 'user-token')
      localStorage.setItem('admin_token', 'admin-token')
      const config = { headers: {} as Record<string, string> }

      const result = capturedReqInterceptor.fn!(config)

      expect(result.headers.Authorization).toBe('Bearer user-token')
    })

    it('does not add Authorization header when no token exists', () => {
      const config = { headers: {} as Record<string, string> }

      const result = capturedReqInterceptor.fn!(config)

      expect(result.headers['Authorization']).toBeUndefined()
    })
  })

  describe('Response error interceptor – 401 handling', () => {
    it('removes token and user from localStorage on 401', async () => {
      localStorage.setItem('token', 'expired-token')
      localStorage.setItem('user', JSON.stringify({ id: 1 }))
      Object.defineProperty(window, 'location', {
        value: { pathname: '/home', href: '' },
        writable: true,
        configurable: true
      })

      const error = { response: { status: 401 } }
      await expect(capturedResErrorInterceptor.fn!(error)).rejects.toEqual(error)

      expect(localStorage.getItem('token')).toBeNull()
      expect(localStorage.getItem('user')).toBeNull()
    })

    it('redirects to /login on 401 when not already on login page', async () => {
      Object.defineProperty(window, 'location', {
        value: { pathname: '/favorites', href: '' },
        writable: true,
        configurable: true
      })

      const error = { response: { status: 401 } }
      await expect(capturedResErrorInterceptor.fn!(error)).rejects.toEqual(error)

      expect(window.location.href).toBe('/login')
    })

    it('does not redirect when already on /login page for a 401', async () => {
      Object.defineProperty(window, 'location', {
        value: { pathname: '/login', href: '' },
        writable: true,
        configurable: true
      })

      const error = { response: { status: 401 } }
      await expect(capturedResErrorInterceptor.fn!(error)).rejects.toEqual(error)

      expect(window.location.href).toBe('')
    })

    it('re-throws non-401 errors without clearing storage', async () => {
      localStorage.setItem('token', 'valid-token')
      const error = { response: { status: 500 } }

      await expect(capturedResErrorInterceptor.fn!(error)).rejects.toEqual(error)

      expect(localStorage.getItem('token')).toBe('valid-token')
    })
  })

  describe('purchase', () => {
    it('calls POST /tickets/purchase with the full request payload', async () => {
      const mockTicket = {
        id: 1, ticketType: 'single', priceRon: 3.0, status: 'valid',
        purchasedAt: '2026-04-25T10:00:00', validFrom: '2026-04-25T10:00:00',
        validUntil: '2026-04-25T11:00:00', qrToken: 'abc123'
      }
      mockPost.mockResolvedValueOnce({ data: mockTicket })

      const req = {
        ticketType: 'single',
        cardNumber: '4111111111111111',
        expiryMonth: '12',
        expiryYear: '2028',
        cvv: '123',
        cardholderName: 'Ion Popescu'
      }

      const result = await ticketsService.purchase(req)

      expect(mockPost).toHaveBeenCalledWith('/tickets/purchase', req)
      expect(result).toEqual(mockTicket)
    })

    it('propagates network errors from purchase', async () => {
      mockPost.mockRejectedValueOnce(new Error('Network error'))

      await expect(ticketsService.purchase({
        ticketType: 'single', cardNumber: '4111', expiryMonth: '12',
        expiryYear: '2028', cvv: '123', cardholderName: 'Test'
      })).rejects.toThrow('Network error')
    })
  })

  describe('getMyTickets', () => {
    it('calls GET /tickets and returns the ticket list', async () => {
      const mockTickets = [
        { id: 1, ticketType: 'single', priceRon: 3.0, status: 'valid',
          purchasedAt: '', validFrom: '', validUntil: '', qrToken: 'abc' },
        { id: 2, ticketType: 'day', priceRon: 10.0, status: 'used',
          purchasedAt: '', validFrom: '', validUntil: '', qrToken: 'xyz' }
      ]
      mockGet.mockResolvedValueOnce({ data: mockTickets })

      const result = await ticketsService.getMyTickets()

      expect(mockGet).toHaveBeenCalledWith('/tickets')
      expect(result).toEqual(mockTickets)
    })

    it('returns empty array when user has no tickets', async () => {
      mockGet.mockResolvedValueOnce({ data: [] })

      const result = await ticketsService.getMyTickets()

      expect(result).toEqual([])
    })
  })

  describe('getTicket', () => {
    it('calls GET /tickets/:id and returns the single ticket', async () => {
      const mockTicket = {
        id: 42, ticketType: 'single', priceRon: 3.0, status: 'used',
        purchasedAt: '', validFrom: '', validUntil: '', qrToken: 'xyz'
      }
      mockGet.mockResolvedValueOnce({ data: mockTicket })

      const result = await ticketsService.getTicket(42)

      expect(mockGet).toHaveBeenCalledWith('/tickets/42')
      expect(result).toEqual(mockTicket)
    })
  })
})
