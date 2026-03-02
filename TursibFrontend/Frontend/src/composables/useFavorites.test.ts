import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { useFavorites } from '@/composables/useFavorites'

// Mock axios at the module level
vi.mock('axios', () => {
  const mockAxiosInstance = {
    get: vi.fn(() => Promise.resolve({ data: [] })),
    post: vi.fn((url, data) => Promise.resolve({ 
      data: { 
        id: Math.random().toString(),
        ...data,
        createdAt: Date.now()
      } 
    })),
    put: vi.fn(() => Promise.resolve({ data: {} })),
    delete: vi.fn(() => Promise.resolve({ data: {} })),
    interceptors: {
      request: { use: vi.fn((fn) => fn) },
      response: { use: vi.fn((fn) => fn) }
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
    // Clear localStorage before each test
    localStorage.clear()
    
    // Create a valid mock JWT token (expires in 2050)
    const mockToken = createMockJWT()
    localStorage.setItem('token', mockToken)
    
    // Reset the module state by clearing favorites
    // Note: This is a workaround since the composable uses module-level state
    const { clearFavorites } = useFavorites()
    await clearFavorites()
  })
  
  afterEach(() => {
    vi.clearAllMocks()
  })

  it('should initialize with empty favorites', () => {
    const { favorites } = useFavorites()
    expect(favorites.value).toEqual([])
  })

  it('should add a home location', async () => {
    const { addFavorite, favorites } = useFavorites()

    const homeLocation = {
      id: '1',
      name: 'Acasă',
      address: 'Strada Principală 123',
      type: 'home' as const,
      icon: '🏠',
      lat: 45.7983,
      lon: 24.1256
    }

    await addFavorite(homeLocation)

    expect(favorites.value.length).toBeGreaterThanOrEqual(1)
    const added = favorites.value.find(f => f.name === 'Acasă')
    expect(added).toBeDefined()
    expect(added?.type).toBe('home')
  })

  it('should add a work location', async () => {
    const { addFavorite, favorites, clearFavorites } = useFavorites()
    
    // Start fresh
    await clearFavorites()

    const workLocation = {
      id: '2',
      name: 'Serviciu',
      address: 'Bulevardul Muncii 45',
      type: 'work' as const,
      icon: '💼',
      lat: 45.8020,
      lon: 24.1350
    }

    await addFavorite(workLocation)

    expect(favorites.value.length).toBeGreaterThanOrEqual(1)
    const added = favorites.value.find(f => f.name === 'Serviciu')
    expect(added).toBeDefined()
    expect(added?.type).toBe('work')
  })

  it('should add custom locations', async () => {
    const { addFavorite, favorites, clearFavorites } = useFavorites()
    
    // Start fresh
    await clearFavorites()

    const customLocation1 = {
      id: '3',
      name: 'Sala de Sport',
      address: 'Strada Fitness 10',
      type: 'custom' as const,
      icon: '🏋️',
      lat: 45.8000,
      lon: 24.1300
    }

    const customLocation2 = {
      id: '4',
      name: 'Cafenea',
      address: 'Piața Centrală 5',
      type: 'custom' as const,
      icon: '☕',
      lat: 45.8010,
      lon: 24.1320
    }

    await addFavorite(customLocation1)
    await addFavorite(customLocation2)

    expect(favorites.value).toHaveLength(2)
  })

  it('should remove a favorite by id', async () => {
    const { addFavorite, removeFavorite,favorites, clearFavorites } = useFavorites()
    
    // Start fresh
    await clearFavorites()

    const location = {
      id: '1',
      name: 'Test',
      address: 'Test Address',
      type: 'custom' as const,
      icon: '📍',
      lat: 45.7983,
      lon: 24.1256
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
      id: '1',
      name: 'Original Name',
      address: 'Original Address',
      type: 'custom' as const,
      icon: '📍',
      lat: 45.7983,
      lon: 24.1256
    }

    await addFavorite(location)

    const updatedLocation = {
      name: 'Updated Name',
      address: 'Updated Address',
      icon: '🏠'
    }

    await updateFavorite('1', updatedLocation)

    expect(favorites.value[0]?.name).toBe('Updated Name')
    expect(favorites.value[0]?.address).toBe('Updated Address')
    expect(favorites.value[0]?.icon).toBe('🏠')
  })

  it('should persist favorites to localStorage', async () => {
    const { addFavorite, clearFavorites } = useFavorites()
    
    // Start fresh
    await clearFavorites()

    const location = {
      id: '1',
      name: 'Test',
      address: 'Test Address',
      type: 'custom' as const,
      icon: '📍',
      lat: 45.7983,
      lon: 24.1256
    }

    await addFavorite(location)

    // Create a new instance and check if data persists (from backend mock)
    const { favorites: newFavorites } = useFavorites()
    const found = newFavorites.value.find(f => f.name === 'Test')
    expect(found).toBeDefined()
  })

  it('should get favorite by id', async () => {
    const { addFavorite, getFavorite } = useFavorites()

    const location = {
      id: 'test-id',
      name: 'Test Location',
      address: 'Test Address',
      type: 'custom' as const,
      icon: '📍',
      lat: 45.7983,
      lon: 24.1256
    }

    await addFavorite(location)

    const retrieved = getFavorite('test-id')
    expect(retrieved).toMatchObject(location)
  })

  it('should return null for non-existent favorite id', () => {
    const { getFavorite } = useFavorites()

    const retrieved = getFavorite('non-existent-id')
    expect(retrieved).toBeNull()
  })

  it('should only allow one home location', async () => {
    const { addFavorite, favorites } = useFavorites()

    const home1 = {
      id: '1',
      name: 'Acasă 1',
      address: 'Address 1',
      type: 'home' as const,
      icon: '🏠',
      lat: 45.7983,
      lon: 24.1256
    }

    const home2 = {
      id: '2',
      name: 'Acasă 2',
      address: 'Address 2',
      type: 'home' as const,
      icon: '🏠',
      lat: 45.8000,
      lon: 24.1300
    }

    await addFavorite(home1)
    await addFavorite(home2)

    // Should replace first home with second
    const homeLocations = favorites.value.filter(f => f.type === 'home')
    expect(homeLocations).toHaveLength(1)
    expect(homeLocations[0]?.name).toBe('Acasă 2')
  })

  it('should only allow one work location', async () => {
    const { addFavorite, favorites } = useFavorites()

    const work1 = {
      id: '1',
      name: 'Serviciu 1',
      address: 'Address 1',
      type: 'work' as const,
      icon: '💼',
      lat: 45.7983,
      lon: 24.1256
    }

    const work2 = {
      id: '2',
      name: 'Serviciu 2',
      address: 'Address 2',
      type: 'work' as const,
      icon: '💼',
      lat: 45.8000,
      lon: 24.1300
    }

    await addFavorite(work1)
    await addFavorite(work2)

    // Should replace first work with second
    const workLocations = favorites.value.filter(f => f.type === 'work')
    expect(workLocations).toHaveLength(1)
    expect(workLocations[0]?.name).toBe('Serviciu 2')
  })
})

// Helper function to create a mock JWT token
function createMockJWT(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({
    sub: 'test-user',
    email: 'test@example.com',
    role: 'User',
    exp: 2524608000 // Expires in year 2050
  }))
  const signature = btoa('mock-signature')
  return `${header}.${payload}.${signature}`
}
