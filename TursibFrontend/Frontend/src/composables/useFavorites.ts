import { ref, computed } from 'vue'
import axios from 'axios'

export interface FavoriteLocation {
  id: string
  name: string
  address: string
  lat: number
  lon: number
  type: 'home' | 'work' | 'custom'
  icon: string
  createdAt: number
}

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

// Axios instance pentru favorites API cu JWT
const favoritesApi = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Interceptor pentru a adăuga JWT token la fiecare cerere
favoritesApi.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// State global pentru favorites
const favorites = ref<FavoriteLocation[]>([])
let isInitialized = false

// Verifică dacă utilizatorul este autentificat
const isAuthenticated = () => {
  const token = localStorage.getItem('token')
  if (!token) {
    console.log('🔍 No token found in localStorage')
    return false
  }
  
  console.log('🔍 Token found, validating...')
  
  // Verifică dacă token-ul este valid (nu expirat)
  try {
    const tokenParts = token.split('.')
    if (tokenParts.length !== 3) {
      console.warn('⚠️ Invalid token format, clearing')
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      return false
    }
    
    const tokenPayload = tokenParts[1]
    if (!tokenPayload) {
      console.warn('⚠️ Invalid token format, clearing')
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      return false
    }
    
    const payload = JSON.parse(atob(tokenPayload))
    console.log('🔍 Token payload:', payload)
    
    const exp = payload.exp * 1000 // Convert to milliseconds
    const now = Date.now()
    
    if (now >= exp) {
      console.warn('⚠️ Token expired at', new Date(exp), 'current time:', new Date(now))
      localStorage.removeItem('token')
      localStorage.removeItem('user')
      return false
    }
    
    console.log('✅ Token is valid')
    return true
  } catch (error) {
    console.warn('⚠️ Error validating token, clearing:', error)
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    return false
  }
}

// Încarcă favorites din backend
const loadFavorites = async () => {
  // Nu face nimic dacă nu e autentificat
  if (!isAuthenticated()) {
    favorites.value = []
    console.log('ℹ️ Not authenticated, skipping favorites load')
    return
  }
  
  try {
    const response = await favoritesApi.get('/Favorites')
    favorites.value = response.data
    console.log('✅ Loaded', favorites.value.length, 'favorite locations from backend')
  } catch (error: any) {
    // Șterge token-ul invalid la 401
    if (error.response?.status === 401) {
      console.warn('⚠️ 401 Unauthorized - clearing invalid token')
      localStorage.removeItem('token')
      localStorage.removeItem('user')
    }
    console.error('❌ Error loading favorites:', error.message || 'Unknown error')
    favorites.value = []
  }
}

export const useFavorites = () => {
  // Inițializează la prima utilizare doar dacă există token
  if (!isInitialized) {
    isInitialized = true
    if (isAuthenticated()) {
      loadFavorites()
    }
  }
  
  // Computed pentru diferite tipuri
  const homeFavorite = computed(() => 
    favorites.value.find(f => f.type === 'home')
  )
  
  const workFavorite = computed(() => 
    favorites.value.find(f => f.type === 'work')
  )
  
  const customFavorites = computed(() => 
    favorites.value.filter(f => f.type === 'custom')
  )
  
  // Adaugă o locație favorită
  const addFavorite = async (location: Omit<FavoriteLocation, 'id' | 'createdAt'>): Promise<FavoriteLocation | null> => {
    if (!isAuthenticated()) {
      console.error('❌ Cannot add favorite: not authenticated')
      return null
    }
    
    try {

      const response = await favoritesApi.post('/Favorites', location)
      const newFavorite = response.data
      
      // Update local state
      if (location.type !== 'custom') {
        const existingIndex = favorites.value.findIndex(f => f.type === location.type)
        if (existingIndex !== -1) {
          favorites.value[existingIndex] = newFavorite
        } else {
          favorites.value.push(newFavorite)
        }
      } else {
        favorites.value.push(newFavorite)
      }
      
      console.log('✅ Added favorite:', newFavorite.name)
      return newFavorite
    } catch (error) {
      console.error('❌ Error adding favorite:', error)
      return null
    }
  }
  
  // Șterge o locație favorită
  const removeFavorite = async (id: string): Promise<boolean> => {
    if (!isAuthenticated()) {
      console.error('❌ Cannot remove favorite: not authenticated')
      return false
    }
    
    try {

      await favoritesApi.delete(`/Favorites/${id}`)
      
      const index = favorites.value.findIndex(f => f.id === id)
      if (index !== -1) {
        favorites.value.splice(index, 1)
        console.log('✅ Removed favorite')
        return true
      }
      return false
    } catch (error) {
      console.error('❌ Error removing favorite:', error)
      return false
    }
  }
  
  // Actualizează o locație favorită
  const updateFavorite = async (id: string, updates: Partial<FavoriteLocation>): Promise<boolean> => {
    if (!isAuthenticated()) {
      console.error('❌ Cannot update favorite: not authenticated')
      return false
    }
    
    try {

      const index = favorites.value.findIndex(f => f.id === id)
      if (index === -1) {
        return false
      }

      const existing = favorites.value[index]
      if (!existing) {
        return false
      }

      const updatedFavorite = {
        ...existing,
        ...updates,
        id, // Nu permitem schimbarea ID-ului
        createdAt: existing.createdAt // Nu permitem schimbarea datei
      }

      await favoritesApi.put(`/Favorites/${id}`, updatedFavorite)
      
      favorites.value[index] = updatedFavorite
      console.log('✅ Updated favorite')
      return true
    } catch (error) {
      console.error('❌ Error updating favorite:', error)
      return false
    }
  }
  
  // Găsește o locație favorită după ID
  const getFavorite = (id: string): FavoriteLocation | null => {
    return favorites.value.find(f => f.id === id) || null
  }
  
  // Verifică dacă o locație este favorită
  const isFavorite = (lat: number, lon: number, threshold = 0.001): boolean => {
    return favorites.value.some(f => 
      Math.abs(f.lat - lat) < threshold && 
      Math.abs(f.lon - lon) < threshold
    )
  }
  
  // Găsește favorita cea mai apropiată de o locație
  const getNearestFavorite = (lat: number, lon: number): FavoriteLocation | null => {
    if (favorites.value.length === 0) return null
    
    let nearest = favorites.value[0]
    if (!nearest) return null
    
    let minDistance = calculateDistance(lat, lon, nearest.lat, nearest.lon)
    
    for (const favorite of favorites.value) {
      const distance = calculateDistance(lat, lon, favorite.lat, favorite.lon)
      if (distance < minDistance) {
        minDistance = distance
        nearest = favorite
      }
    }
    
    return nearest
  }
  
  // Șterge toate favorite
  const clearFavorites = async (): Promise<boolean> => {
    if (!isAuthenticated()) {
      console.error('❌ Cannot clear favorites: not authenticated')
      return false
    }
    
    try {

      await favoritesApi.delete('/Favorites')
      favorites.value = []
      console.log('✅ Cleared all favorites')
      return true
    } catch (error) {
      console.error('❌ Error clearing favorites:', error)
      return false
    }
  }

  // Reîncarcă favorites din backend
  const refreshFavorites = async (): Promise<void> => {
    isInitialized = false
    favorites.value = []
    await loadFavorites()
    isInitialized = true
  }
  
  return {
    favorites: computed(() => favorites.value),
    homeFavorite,
    workFavorite,
    customFavorites,
    addFavorite,
    removeFavorite,
    updateFavorite,
    getFavorite,
    isFavorite,
    getNearestFavorite,
    clearFavorites,
    refreshFavorites
  }
}

// Helper pentru calcularea distanței
const calculateDistance = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371 // km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a = 
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}
