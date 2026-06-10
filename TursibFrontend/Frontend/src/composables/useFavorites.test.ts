import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { useFavorites } from '@/composables/useFavorites'

// Expose mock functions via vi.hoisted so they are available inside vi.mock factories
// and can be reconfigured per-test with mockRejectedValueOnce etc.
const mockGet = vi.hoisted(() =>
  vi.fn(() => Promise.resolve({ data: [] }))
)
const mockPost = vi.hoisted(() =>
  vi.fn((_url: string, data: any) =>
    Promise.resolve({ data: { id: Math.random().toString(), ...data, createdAt: Date.now() } })
  )
)
const mockPut = vi.hoisted(() => vi.fn(() => Promise.resolve({ data: {} })))
const mockDelete = vi.hoisted(() => vi.fn(() => Promise.resolve({ data: {} })))

vi.mock('axios', () => {
  const mockAxiosInstance = {
    get: mockGet,
    post: mockPost,
    put: mockPut,
    delete: mockDelete,
    interceptors: {
      request: { use: vi.fn((fn: any) => fn) },
      response: { use: vi.fn((fn: any) => fn) }
    }
  }
  return {
    default: {
      create: vi.fn(() => mockAxiosInstance)
    }
  }
})

describe('useFavorites', () => {
  beforeEach(async () => {
    localStorage.clear()
    const mockToken = createMockJWT()
    localStorage.setItem('token', mockToken)

    // Reset mock implementations to defaults before each test
    mockGet.mockImplementation(() => Promise.resolve({ data: [] }))
    mockPost.mockImplementation((_url: string, data: any) =>
      Promise.resolve({ data: { id: Math.random().toString(), ...data, createdAt: Date.now() } })
    )
    mockPut.mockImplementation(() => Promise.resolve({ data: {} }))
    mockDelete.mockImplementation(() => Promise.resolve({ data: {} }))

    const { clearFavorites } = useFavorites()
    await clearFavorites()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  // ─── basic CRUD (original tests) ──────────────────────────────────────────

  it('should initialize with empty favorites', () => {
    const { favorites } = useFavorites()
    expect(favorites.value).toEqual([])
  })

  it('should add a home location', async () => {
    const { addFavorite, favorites } = useFavorites()

    const homeLocation = {
      name: 'Acasă', address: 'Strada Principală 123',
      type: 'home' as const, icon: '🏠', lat: 45.7983, lon: 24.1256
    }

    await addFavorite(homeLocation)

    expect(favorites.value.length).toBeGreaterThanOrEqual(1)
    const added = favorites.value.find(f => f.name === 'Acasă')
    expect(added).toBeDefined()
    expect(added?.type).toBe('home')
  })

  it('should add a work location', async () => {
    const { addFavorite, favorites, clearFavorites } = useFavorites()
    await clearFavorites()

    const workLocation = {
      name: 'Serviciu', address: 'Bulevardul Muncii 45',
      type: 'work' as const, icon: '💼', lat: 45.8020, lon: 24.1350
    }

    await addFavorite(workLocation)

    expect(favorites.value.length).toBeGreaterThanOrEqual(1)
    const added = favorites.value.find(f => f.name === 'Serviciu')
    expect(added).toBeDefined()
    expect(added?.type).toBe('work')
  })

  it('should add custom locations', async () => {
    const { addFavorite, favorites, clearFavorites } = useFavorites()
    await clearFavorites()

    await addFavorite({
      name: 'Sala de Sport', address: 'Strada Fitness 10',
      type: 'custom' as const, icon: '🏋️', lat: 45.8000, lon: 24.1300
    })
    await addFavorite({
      name: 'Cafenea', address: 'Piața Centrală 5',
      type: 'custom' as const, icon: '☕', lat: 45.8010, lon: 24.1320
    })

    expect(favorites.value).toHaveLength(2)
  })

  it('should remove a favorite by id', async () => {
    const { addFavorite, removeFavorite, favorites, clearFavorites } = useFavorites()
    await clearFavorites()

    const location = {
      name: 'Test', address: 'Test Address',
      type: 'custom' as const, icon: '📍', lat: 45.7983, lon: 24.1256
    }

    const added = await addFavorite(location)
    const initialLength = favorites.value.length
    expect(initialLength).toBeGreaterThanOrEqual(1)

    if (added) {
      await removeFavorite(added.id)
      expect(favorites.value.length).toBe(initialLength - 1)
    }
  })

  it('should update an existing favorite', async () => {
    const { addFavorite, updateFavorite, favorites } = useFavorites()

    const location = {
      name: 'Original Name', address: 'Original Address',
      type: 'custom' as const, icon: '📍', lat: 45.7983, lon: 24.1256
    }

    await addFavorite(location)

    await updateFavorite('1', { name: 'Updated Name', address: 'Updated Address', icon: '🏠' })

    expect(favorites.value[0]?.name).toBe('Updated Name')
    expect(favorites.value[0]?.address).toBe('Updated Address')
    expect(favorites.value[0]?.icon).toBe('🏠')
  })

  it('should persist favorites to localStorage', async () => {
    const { addFavorite, clearFavorites } = useFavorites()
    await clearFavorites()

    await addFavorite({
      name: 'Test', address: 'Test Address',
      type: 'custom' as const, icon: '📍', lat: 45.7983, lon: 24.1256
    })

    const { favorites: newFavorites } = useFavorites()
    const found = newFavorites.value.find(f => f.name === 'Test')
    expect(found).toBeDefined()
  })

  it('should get favorite by id', async () => {
    const { addFavorite, getFavorite } = useFavorites()

    const location = {
      name: 'Test Location', address: 'Test Address',
      type: 'custom' as const, icon: '📍', lat: 45.7983, lon: 24.1256
    }

    await addFavorite(location)

    const retrieved = getFavorite('test-id')
    expect(retrieved).toMatchObject(location)
  })

  it('should return null for non-existent favorite id', () => {
    const { getFavorite } = useFavorites()
    expect(getFavorite('non-existent-id')).toBeNull()
  })

  it('should only allow one home location', async () => {
    const { addFavorite, favorites } = useFavorites()

    await addFavorite({
      name: 'Acasă 1', address: 'Address 1',
      type: 'home' as const, icon: '🏠', lat: 45.7983, lon: 24.1256
    })
    await addFavorite({
      name: 'Acasă 2', address: 'Address 2',
      type: 'home' as const, icon: '🏠', lat: 45.8000, lon: 24.1300
    })

    const homeLocations = favorites.value.filter(f => f.type === 'home')
    expect(homeLocations).toHaveLength(1)
    expect(homeLocations[0]?.name).toBe('Acasă 2')
  })

  it('should only allow one work location', async () => {
    const { addFavorite, favorites } = useFavorites()

    await addFavorite({
      name: 'Serviciu 1', address: 'Address 1',
      type: 'work' as const, icon: '💼', lat: 45.7983, lon: 24.1256
    })
    await addFavorite({
      name: 'Serviciu 2', address: 'Address 2',
      type: 'work' as const, icon: '💼', lat: 45.8000, lon: 24.1300
    })

    const workLocations = favorites.value.filter(f => f.type === 'work')
    expect(workLocations).toHaveLength(1)
    expect(workLocations[0]?.name).toBe('Serviciu 2')
  })

  // ─── isFavorite ───────────────────────────────────────────────────────────

  describe('isFavorite', () => {
    it('returns true for coordinates matching a saved favorite within threshold', async () => {
      const { addFavorite, isFavorite, clearFavorites } = useFavorites()
      await clearFavorites()

      await addFavorite({
        name: 'Piața Unirii', address: 'Piața Unirii',
        type: 'custom' as const, icon: '📍', lat: 45.7983, lon: 24.1256
      })

      expect(isFavorite(45.7983, 24.1256)).toBe(true)
    })

    it('returns false for coordinates far from any saved favorite', async () => {
      const { addFavorite, isFavorite, clearFavorites } = useFavorites()
      await clearFavorites()

      await addFavorite({
        name: 'Piața Unirii', address: '',
        type: 'custom' as const, icon: '📍', lat: 45.7983, lon: 24.1256
      })

      expect(isFavorite(46.0, 25.0)).toBe(false)
    })

    it('returns false when favorites list is empty', () => {
      const { isFavorite } = useFavorites()
      expect(isFavorite(45.7983, 24.1256)).toBe(false)
    })
  })

  // ─── getNearestFavorite ───────────────────────────────────────────────────

  describe('getNearestFavorite', () => {
    it('returns null when there are no favorites', () => {
      const { getNearestFavorite } = useFavorites()
      expect(getNearestFavorite(45.79, 24.14)).toBeNull()
    })

    it('returns the favorite nearest to the given coordinates', async () => {
      const { addFavorite, getNearestFavorite, clearFavorites } = useFavorites()
      await clearFavorites()

      await addFavorite({
        name: 'Close', address: '',
        type: 'custom' as const, icon: '📍', lat: 45.7910, lon: 24.1490
      })
      await addFavorite({
        name: 'Far', address: '',
        type: 'custom' as const, icon: '📍', lat: 46.0, lon: 25.0
      })

      const nearest = getNearestFavorite(45.7909, 24.1485) // very close to 'Close'
      expect(nearest?.name).toBe('Close')
    })
  })

  // ─── error paths ──────────────────────────────────────────────────────────

  describe('error paths', () => {
    it('addFavorite returns null and does not corrupt state when the API rejects', async () => {
      const { addFavorite, favorites, clearFavorites } = useFavorites()
      await clearFavorites()
      mockPost.mockRejectedValueOnce(new Error('Network error'))

      const result = await addFavorite({
        name: 'Test', address: 'Test',
        type: 'custom' as const, icon: '📍', lat: 45.79, lon: 24.14
      })

      expect(result).toBeNull()
      expect(favorites.value).toHaveLength(0)
    })

    it('removeFavorite returns false when the API rejects', async () => {
      const { addFavorite, removeFavorite, favorites } = useFavorites()
      const added = await addFavorite({
        name: 'Test', address: 'Test',
        type: 'custom' as const, icon: '📍', lat: 45.79, lon: 24.14
      })

      mockDelete.mockRejectedValueOnce(new Error('Network error'))
      const result = await removeFavorite(added!.id)

      expect(result).toBe(false)
      // Favorite should still be in local state because removal failed
      expect(favorites.value.some(f => f.id === added!.id)).toBe(true)
    })

    it('addFavorite returns null when user is not authenticated', async () => {
      localStorage.removeItem('token')
      const { addFavorite } = useFavorites()

      const result = await addFavorite({
        name: 'Test', address: 'Test',
        type: 'custom' as const, icon: '📍', lat: 45.79, lon: 24.14
      })

      expect(result).toBeNull()
    })

    it('removeFavorite returns false when user is not authenticated', async () => {
      localStorage.removeItem('token')
      const { removeFavorite } = useFavorites()

      const result = await removeFavorite('some-id')

      expect(result).toBe(false)
    })

    it('handles an expired JWT token by treating the user as unauthenticated', async () => {
      // Token with exp in the past (year 2000)
      const expiredToken = buildExpiredJWT()
      localStorage.setItem('token', expiredToken)

      const { addFavorite } = useFavorites()
      const result = await addFavorite({
        name: 'Test', address: 'Test',
        type: 'custom' as const, icon: '📍', lat: 45.79, lon: 24.14
      })

      expect(result).toBeNull()
      // Expired token should have been cleared from storage
      expect(localStorage.getItem('token')).toBeNull()
    })

    it('handles a malformed token (wrong format) gracefully', async () => {
      localStorage.setItem('token', 'not.a.valid.jwt.string.extra')
      const { addFavorite } = useFavorites()

      // Should not throw - just treat as unauthenticated
      const result = await addFavorite({
        name: 'Test', address: 'Test',
        type: 'custom' as const, icon: '📍', lat: 45.79, lon: 24.14
      })

      expect(result).toBeNull()
    })
  })
})

// ─── JWT helpers ──────────────────────────────────────────────────────────────

function createMockJWT(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({
    sub: 'test-user',
    email: 'test@example.com',
    role: 'User',
    exp: 2524608000 // Expires year 2050
  }))
  const signature = btoa('mock-signature')
  return `${header}.${payload}.${signature}`
}

function buildExpiredJWT(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({
    sub: 'test-user',
    email: 'test@example.com',
    role: 'User',
    exp: Math.floor(new Date('2000-01-01').getTime() / 1000) // expired in 2000
  }))
  const signature = btoa('mock-signature')
  return `${header}.${payload}.${signature}`
}
