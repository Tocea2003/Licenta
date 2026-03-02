import { describe, it, expect, beforeEach } from 'vitest'
import { useRecentSearches } from '@/composables/useRecentSearches'

describe('useRecentSearches', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('should initialize with empty searches', () => {
    const { recentSearches } = useRecentSearches()
    expect(recentSearches.value).toEqual([])
  })

  it('should add a recent search', () => {
    const { addRecentSearch, recentSearches } = useRecentSearches()

    const search = {
      query: 'Piața Unirii',
      type: 'station' as const,
      result: {
        name: 'Piața Unirii',
        lat: 45.7983,
        lon: 24.1256,
        stationId: 123
      }
    }

    addRecentSearch(search)

    expect(recentSearches.value).toHaveLength(1)
    expect(recentSearches.value[0]?.result.name).toBe('Piața Unirii')
  })

  it('should limit to 10 searches', () => {
    const { addRecentSearch, recentSearches } = useRecentSearches()

    // Add 12 searches
    for (let i = 0; i < 12; i++) {
      addRecentSearch({
        query: `Search ${i}`,
        type: 'station' as const,
        result: {
          name: `Station ${i}`,
          lat: 45.7983,
          lon: 24.1256,
          stationId: i
        }
      })
    }

    expect(recentSearches.value).toHaveLength(10)
    expect(recentSearches.value[0]?.result.name).toBe('Station 11')
    expect(recentSearches.value[9]?.result.name).toBe('Station 2')
  })

  it('should clear all searches', () => {
    const { addRecentSearch, clearRecentSearches, recentSearches } = useRecentSearches()

    addRecentSearch({
      query: 'Test',
      type: 'station' as const,
      result: {
        name: 'Test Station',
        lat: 45.7983,
        lon: 24.1256,
        stationId: 1
      }
    })

    clearRecentSearches()
    expect(recentSearches.value).toHaveLength(0)
  })

  it('should remove a search by id', () => {
    const { addRecentSearch, removeRecentSearch, recentSearches } = useRecentSearches()

    const added = addRecentSearch({
      query: 'Test',
      type: 'station' as const,
      result: {
        name: 'Test Station',
        lat: 45.7983,
        lon: 24.1256,
        stationId: 1
      }
    })

    removeRecentSearch(added.id)
    expect(recentSearches.value).toHaveLength(0)
  })

  it('should persist searches to localStorage', () => {
    const { addRecentSearch } = useRecentSearches()

    addRecentSearch({
      query: 'Strada Principală',
      type: 'address' as const,
      result: {
        name: 'Strada Principală 123',
        lat: 45.7983,
        lon: 24.1256
      }
    })

    // Create new instance to test persistence
    const { recentSearches: newSearches } = useRecentSearches()

    expect(newSearches.value).toHaveLength(1)
    expect(newSearches.value[0]?.result.name).toBe('Strada Principală 123')
  })

  it('should deduplicate searches', () => {
    const { addRecentSearch, recentSearches, clearRecentSearches } = useRecentSearches()
    
    // Start fresh
    clearRecentSearches()

    const search = {
      query: 'Piața Unirii',
      type: 'station' as const,
      result: {
        name: 'Piața Unirii',
        lat: 45.7983,
        lon: 24.1256,
        stationId: 123
      }
    }

    addRecentSearch(search)
    addRecentSearch(search)

    // The implementation removes duplicate and re-adds at top
    // So we should have only 1 search
    expect(recentSearches.value).toHaveLength(1)
  })

  it('should support different search types', () => {
    const { addRecentSearch, recentSearches } = useRecentSearches()

    addRecentSearch({
      query: 'Piața Unirii',
      type: 'station' as const,
      result: {
        name: 'Piața Unirii',
        lat: 45.7983,
        lon: 24.1256,
        stationId: 123
      }
    })

    addRecentSearch({
      query: 'Strada Principală',
      type: 'address' as const,
      result: {
        name: 'Strada Principală 123',
        lat: 45.8000,
        lon: 24.1300
      }
    })

    addRecentSearch({
      query: 'Linia 5',
      type: 'route' as const,
      result: {
        name: 'Linia 5',
        routeId: 5
      }
    })

    expect(recentSearches.value).toHaveLength(3)
    expect(recentSearches.value.map(s => s.type)).toEqual(['route', 'address', 'station'])
  })
})
