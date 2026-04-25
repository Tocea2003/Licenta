import { describe, it, expect, beforeEach, vi } from 'vitest'

// Re-import the module fresh for each test to reset the module-level
// `isInitialized` flag and the `statistics` ref.
type StatisticsModule = typeof import('@/composables/useStatistics')

describe('useStatistics', () => {
  let useStatistics: StatisticsModule['useStatistics']

  beforeEach(async () => {
    vi.resetModules()
    localStorage.clear()
    const mod: StatisticsModule = await import('@/composables/useStatistics')
    useStatistics = mod.useStatistics
  })

  // ─── initial state ────────────────────────────────────────────────────────

  describe('initial state', () => {
    it('starts with all counters at zero and empty lists', () => {
      const { statistics } = useStatistics()
      expect(statistics.value.totalSearches).toBe(0)
      expect(statistics.value.totalTrips).toBe(0)
      expect(statistics.value.totalTimeUsed).toBe(0)
      expect(statistics.value.favoriteRoutes).toEqual([])
      expect(statistics.value.favoriteStations).toEqual([])
      expect(statistics.value.searchHistory).toEqual([])
    })
  })

  // ─── incrementSearches ────────────────────────────────────────────────────

  describe('incrementSearches', () => {
    it('increases totalSearches by one each call', () => {
      const { incrementSearches, statistics } = useStatistics()
      incrementSearches()
      expect(statistics.value.totalSearches).toBe(1)
      incrementSearches()
      expect(statistics.value.totalSearches).toBe(2)
    })

    it('adds a new entry for today in searchHistory', () => {
      const { incrementSearches, statistics } = useStatistics()
      const today = new Date().toISOString().split('T')[0]
      incrementSearches()
      expect(statistics.value.searchHistory).toHaveLength(1)
      expect(statistics.value.searchHistory[0]?.date).toBe(today)
      expect(statistics.value.searchHistory[0]?.count).toBe(1)
    })

    it('accumulates count in an existing today entry instead of duplicating', () => {
      const { incrementSearches, statistics } = useStatistics()
      incrementSearches()
      incrementSearches()
      incrementSearches()
      expect(statistics.value.searchHistory).toHaveLength(1)
      expect(statistics.value.searchHistory[0]?.count).toBe(3)
    })

    it('keeps searchHistory capped at 30 entries', () => {
      const { statistics, incrementSearches } = useStatistics()
      // Pre-fill 30 past days
      statistics.value.searchHistory = Array.from({ length: 30 }, (_, i) => ({
        date: `2025-01-${String(i + 1).padStart(2, '0')}`,
        count: 1
      }))
      // Today's search should unshift a new entry and trim to 30
      incrementSearches()
      expect(statistics.value.searchHistory.length).toBeLessThanOrEqual(30)
    })
  })

  // ─── incrementTrips ───────────────────────────────────────────────────────

  describe('incrementTrips', () => {
    it('increases totalTrips by one each call', () => {
      const { incrementTrips, statistics } = useStatistics()
      incrementTrips()
      expect(statistics.value.totalTrips).toBe(1)
      incrementTrips()
      expect(statistics.value.totalTrips).toBe(2)
    })
  })

  // ─── addTimeUsed ──────────────────────────────────────────────────────────

  describe('addTimeUsed', () => {
    it('accumulates minutes correctly', () => {
      const { addTimeUsed, statistics } = useStatistics()
      addTimeUsed(10)
      addTimeUsed(25)
      expect(statistics.value.totalTimeUsed).toBe(35)
    })
  })

  // ─── recordRouteUsage ─────────────────────────────────────────────────────

  describe('recordRouteUsage', () => {
    it('creates a new entry with count 1 on first use', () => {
      const { recordRouteUsage, statistics } = useStatistics()
      recordRouteUsage(5)
      expect(statistics.value.favoriteRoutes).toHaveLength(1)
      expect(statistics.value.favoriteRoutes[0]).toEqual({ routeId: 5, count: 1 })
    })

    it('increments count on subsequent uses of the same route', () => {
      const { recordRouteUsage, statistics } = useStatistics()
      recordRouteUsage(5)
      recordRouteUsage(5)
      recordRouteUsage(5)
      expect(statistics.value.favoriteRoutes[0]?.count).toBe(3)
    })

    it('sorts entries by count descending', () => {
      const { recordRouteUsage, statistics } = useStatistics()
      recordRouteUsage(1)                     // count 1
      recordRouteUsage(2); recordRouteUsage(2) // count 2
      recordRouteUsage(3); recordRouteUsage(3); recordRouteUsage(3) // count 3

      expect(statistics.value.favoriteRoutes[0]?.routeId).toBe(3)
      expect(statistics.value.favoriteRoutes[1]?.routeId).toBe(2)
      expect(statistics.value.favoriteRoutes[2]?.routeId).toBe(1)
    })

    it('caps the list at 10 routes', () => {
      const { recordRouteUsage, statistics } = useStatistics()
      for (let i = 1; i <= 12; i++) recordRouteUsage(i)
      expect(statistics.value.favoriteRoutes).toHaveLength(10)
    })
  })

  // ─── recordStationUsage ───────────────────────────────────────────────────

  describe('recordStationUsage', () => {
    it('creates a new entry with count 1 on first use', () => {
      const { recordStationUsage, statistics } = useStatistics()
      recordStationUsage(8)
      expect(statistics.value.favoriteStations).toHaveLength(1)
      expect(statistics.value.favoriteStations[0]).toEqual({ stationId: 8, count: 1 })
    })

    it('increments count on subsequent uses of the same station', () => {
      const { recordStationUsage, statistics } = useStatistics()
      recordStationUsage(8)
      recordStationUsage(8)
      expect(statistics.value.favoriteStations[0]?.count).toBe(2)
    })

    it('sorts entries by count descending', () => {
      const { recordStationUsage, statistics } = useStatistics()
      recordStationUsage(1)
      recordStationUsage(8); recordStationUsage(8)

      expect(statistics.value.favoriteStations[0]?.stationId).toBe(8)
      expect(statistics.value.favoriteStations[1]?.stationId).toBe(1)
    })

    it('caps the list at 10 stations', () => {
      const { recordStationUsage, statistics } = useStatistics()
      for (let i = 1; i <= 12; i++) recordStationUsage(i)
      expect(statistics.value.favoriteStations).toHaveLength(10)
    })
  })

  // ─── resetStatistics ──────────────────────────────────────────────────────

  describe('resetStatistics', () => {
    it('clears all counters, lists, and history', () => {
      const {
        incrementSearches, incrementTrips, addTimeUsed,
        recordRouteUsage, recordStationUsage, resetStatistics, statistics
      } = useStatistics()

      incrementSearches()
      incrementTrips()
      addTimeUsed(30)
      recordRouteUsage(1)
      recordStationUsage(8)

      resetStatistics()

      expect(statistics.value.totalSearches).toBe(0)
      expect(statistics.value.totalTrips).toBe(0)
      expect(statistics.value.totalTimeUsed).toBe(0)
      expect(statistics.value.favoriteRoutes).toEqual([])
      expect(statistics.value.favoriteStations).toEqual([])
      expect(statistics.value.searchHistory).toEqual([])
    })
  })

  // ─── computed properties ──────────────────────────────────────────────────

  describe('averageSearchesPerDay', () => {
    it('returns 0 when search history is empty', () => {
      const { averageSearchesPerDay } = useStatistics()
      expect(averageSearchesPerDay.value).toBe(0)
    })

    it('returns the rounded average across history entries', () => {
      const { statistics, averageSearchesPerDay } = useStatistics()
      statistics.value.searchHistory = [
        { date: '2026-01-01', count: 3 },
        { date: '2026-01-02', count: 4 }
      ]
      // Math.round((3+4)/2) = Math.round(3.5) = 4
      expect(averageSearchesPerDay.value).toBe(4)
    })
  })

  describe('mostUsedRoute', () => {
    it('returns null when no routes have been used', () => {
      const { mostUsedRoute } = useStatistics()
      expect(mostUsedRoute.value).toBeNull()
    })

    it('returns the route with the highest count', () => {
      const { recordRouteUsage, mostUsedRoute } = useStatistics()
      recordRouteUsage(5)
      recordRouteUsage(5)
      recordRouteUsage(1)
      expect(mostUsedRoute.value?.routeId).toBe(5)
    })
  })

  describe('mostUsedStation', () => {
    it('returns null when no stations have been used', () => {
      const { mostUsedStation } = useStatistics()
      expect(mostUsedStation.value).toBeNull()
    })

    it('returns the station with the highest count', () => {
      const { recordStationUsage, mostUsedStation } = useStatistics()
      recordStationUsage(8)
      recordStationUsage(8)
      recordStationUsage(12)
      expect(mostUsedStation.value?.stationId).toBe(8)
    })
  })

  // ─── persistence ──────────────────────────────────────────────────────────

  describe('localStorage persistence', () => {
    it('saves statistics to localStorage after each change', () => {
      const { incrementSearches } = useStatistics()
      incrementSearches()
      const stored = JSON.parse(localStorage.getItem('tursib_user_statistics')!)
      expect(stored.totalSearches).toBe(1)
    })

    it('loads previously persisted statistics on initialization', async () => {
      localStorage.setItem('tursib_user_statistics', JSON.stringify({
        totalSearches: 42,
        totalTrips: 7,
        favoriteRoutes: [{ routeId: 1, count: 5 }],
        favoriteStations: [],
        totalTimeUsed: 120,
        searchHistory: [],
        lastUpdated: Date.now()
      }))

      vi.resetModules()
      const { useStatistics: freshUse } = await import('@/composables/useStatistics')
      const { statistics } = freshUse()

      expect(statistics.value.totalSearches).toBe(42)
      expect(statistics.value.totalTrips).toBe(7)
      expect(statistics.value.totalTimeUsed).toBe(120)
    })

    it('handles corrupted JSON in localStorage without throwing', async () => {
      localStorage.setItem('tursib_user_statistics', 'not-valid-json{{{')
      vi.resetModules()
      const { useStatistics: freshUse } = await import('@/composables/useStatistics')
      expect(() => freshUse()).not.toThrow()
      const { statistics } = freshUse()
      expect(statistics.value.totalSearches).toBe(0)
    })
  })
})
