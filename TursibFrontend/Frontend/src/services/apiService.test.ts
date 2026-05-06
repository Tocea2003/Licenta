import { describe, it, expect, beforeEach, vi } from 'vitest'

const mockGet = vi.hoisted(() => vi.fn())
const mockPost = vi.hoisted(() => vi.fn())

vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => ({
      get: mockGet,
      post: mockPost,
      interceptors: {
        request: { use: vi.fn() },
        response: { use: vi.fn() }
      }
    }))
  }
}))

import apiService from '@/services/apiService'

describe('apiService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('Routes', () => {
    it('getRoutes calls GET /routes and returns data', async () => {
      const mockRoutes = [{ id: 1, routeNumber: '1', name: 'Cimitir - Hornbach', color: '#ff0000' }]
      mockGet.mockResolvedValueOnce({ data: mockRoutes })

      const result = await apiService.getRoutes()

      expect(mockGet).toHaveBeenCalledWith('/routes')
      expect(result).toEqual(mockRoutes)
    })

    it('getRoute calls GET /routes/:id and returns single route', async () => {
      const mockRoute = { id: 5, routeNumber: '5', name: 'Route 5' }
      mockGet.mockResolvedValueOnce({ data: mockRoute })

      const result = await apiService.getRoute(5)

      expect(mockGet).toHaveBeenCalledWith('/routes/5')
      expect(result).toEqual(mockRoute)
    })

    it('getRouteStations calls GET /routes/:id/stations', async () => {
      const mockStations = [
        { id: 1, name: 'Cimitir', latitude: 45.7679, longitude: 24.1385 },
        { id: 2, name: 'Calea Dumbrăvii', latitude: 45.7704, longitude: 24.1384 }
      ]
      mockGet.mockResolvedValueOnce({ data: mockStations })

      const result = await apiService.getRouteStations(1)

      expect(mockGet).toHaveBeenCalledWith('/routes/1/stations')
      expect(result).toEqual(mockStations)
    })
  })

  describe('Stations', () => {
    it('getStations calls GET /stations and returns all stations', async () => {
      const mockStations = [{ id: 8, name: 'Piața Unirii', latitude: 45.79, longitude: 24.14 }]
      mockGet.mockResolvedValueOnce({ data: mockStations })

      const result = await apiService.getStations()

      expect(mockGet).toHaveBeenCalledWith('/stations')
      expect(result).toEqual(mockStations)
    })

    it('getStation calls GET /stations/:id', async () => {
      const mockStation = { id: 8, name: 'Piața Unirii', latitude: 45.79, longitude: 24.14 }
      mockGet.mockResolvedValueOnce({ data: mockStation })

      const result = await apiService.getStation(8)

      expect(mockGet).toHaveBeenCalledWith('/stations/8')
      expect(result).toEqual(mockStation)
    })

    it('getStationRoutes calls GET /stations/:id/routes', async () => {
      const mockRoutes = [{ id: 1, routeNumber: '1', name: 'Cimitir - Hornbach' }]
      mockGet.mockResolvedValueOnce({ data: mockRoutes })

      const result = await apiService.getStationRoutes(8)

      expect(mockGet).toHaveBeenCalledWith('/stations/8/routes')
      expect(result).toEqual(mockRoutes)
    })

    it('getStationSchedule calls GET /stations/:id/schedule', async () => {
      const mockSchedule = [{
        routeId: 1, routeNumber: '1', routeName: 'Cimitir - Hornbach',
        directionId: 0, arrivalTime: '08:00', departureTime: '08:01'
      }]
      mockGet.mockResolvedValueOnce({ data: mockSchedule })

      const result = await apiService.getStationSchedule(8)

      expect(mockGet).toHaveBeenCalledWith('/stations/8/schedule')
      expect(result).toEqual(mockSchedule)
    })
  })

  describe('Buses', () => {
    it('getBuses calls GET /buses and returns all buses', async () => {
      const mockBuses = [{ id: 1, licensePlate: 'SB-01-ABC', internalName: 'Bus 101', currentRouteId: 1 }]
      mockGet.mockResolvedValueOnce({ data: mockBuses })

      const result = await apiService.getBuses()

      expect(mockGet).toHaveBeenCalledWith('/buses')
      expect(result).toEqual(mockBuses)
    })

    it('getBus calls GET /buses/:id', async () => {
      const mockBus = { id: 2, licensePlate: 'SB-11-XYZ', internalName: 'Bus 211', currentRouteId: 2 }
      mockGet.mockResolvedValueOnce({ data: mockBus })

      const result = await apiService.getBus(2)

      expect(mockGet).toHaveBeenCalledWith('/buses/2')
      expect(result).toEqual(mockBus)
    })
  })

  describe('Shapes', () => {
    it('getRouteShape calls GET /shapes/route/:id', async () => {
      const mockShape = { routeId: 1, shapeId: 'shape-1', directionId: 0, points: [] }
      mockGet.mockResolvedValueOnce({ data: mockShape })

      const result = await apiService.getRouteShape(1)

      expect(mockGet).toHaveBeenCalledWith('/shapes/route/1')
      expect(result).toEqual(mockShape)
    })

    it('getRouteSegment calls GET /shapes/route/:id/segment with correct query params', async () => {
      const mockShape = { routeId: 1, shapeId: 'shape-1', directionId: 0, points: [] }
      mockGet.mockResolvedValueOnce({ data: mockShape })

      const result = await apiService.getRouteSegment(1, 3, 7)

      expect(mockGet).toHaveBeenCalledWith(
        '/shapes/route/1/segment?fromStationId=3&toStationId=7'
      )
      expect(result).toEqual(mockShape)
    })
  })

  describe('Routing', () => {
    it('calculateRouteAlternatives without options sends only station ids', async () => {
      mockPost.mockResolvedValueOnce({ data: [] })

      await apiService.calculateRouteAlternatives(1, 17)

      expect(mockPost).toHaveBeenCalledWith('/routing/alternatives', {
        startStationId: 1,
        endStationId: 17
      })
    })

    it('calculateRouteAlternatives with departAt sends departureTime', async () => {
      mockPost.mockResolvedValueOnce({ data: [] })

      await apiService.calculateRouteAlternatives(1, 17, {
        mode: 'departAt',
        dateTime: '2026-04-25T08:00:00'
      })

      expect(mockPost).toHaveBeenCalledWith('/routing/alternatives', {
        startStationId: 1,
        endStationId: 17,
        departureTime: '2026-04-25T08:00:00'
      })
    })

    it('calculateRouteAlternatives with arriveBy sends arrivalTime', async () => {
      mockPost.mockResolvedValueOnce({ data: [] })

      await apiService.calculateRouteAlternatives(1, 17, {
        mode: 'arriveBy',
        dateTime: '2026-04-25T09:00:00'
      })

      expect(mockPost).toHaveBeenCalledWith('/routing/alternatives', {
        startStationId: 1,
        endStationId: 17,
        arrivalTime: '2026-04-25T09:00:00'
      })
    })

    it('calculateRouteAlternatives with legacy string option sends departureTime', async () => {
      mockPost.mockResolvedValueOnce({ data: [] })

      await apiService.calculateRouteAlternatives(1, 17, '2026-04-25T08:00:00')

      expect(mockPost).toHaveBeenCalledWith('/routing/alternatives', {
        startStationId: 1,
        endStationId: 17,
        departureTime: '2026-04-25T08:00:00'
      })
    })

    it('calculateRouteAlternatives returns the routes array from the response', async () => {
      const mockRoutes = [
        { routeType: 'direct', totalDuration: 30, segments: [], routeRank: 1, routeCategory: 'Cea mai rapidă' }
      ]
      mockPost.mockResolvedValueOnce({ data: mockRoutes })

      const result = await apiService.calculateRouteAlternatives(1, 17)

      expect(result).toEqual(mockRoutes)
    })
  })
})
