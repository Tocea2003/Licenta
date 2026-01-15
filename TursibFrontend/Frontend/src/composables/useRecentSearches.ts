import { ref, computed } from 'vue'

export interface RecentSearch {
  id: string
  query: string
  type: 'station' | 'address' | 'route'
  result: {
    name: string
    lat?: number
    lon?: number
    stationId?: number
    routeId?: number
  }
  timestamp: number
}

const STORAGE_KEY = 'tursib_recent_searches'
const MAX_RECENT_SEARCHES = 10

// State global pentru recent searches
const recentSearches = ref<RecentSearch[]>([])
let isInitialized = false

// Încarcă recent searches din localStorage
const loadRecentSearches = () => {
  if (isInitialized) return
  
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      recentSearches.value = JSON.parse(stored)
      console.log('✅ Loaded', recentSearches.value.length, 'recent searches')
    }
  } catch (error) {
    console.error('❌ Error loading recent searches:', error)
    recentSearches.value = []
  }
  
  isInitialized = true
}

// Salvează recent searches în localStorage
const saveRecentSearches = () => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(recentSearches.value))
  } catch (error) {
    console.error('❌ Error saving recent searches:', error)
  }
}

export const useRecentSearches = () => {
  // Inițializează la prima utilizare
  if (!isInitialized) {
    loadRecentSearches()
  }
  
  // Computed pentru ultimele căutări (limitat la MAX_RECENT_SEARCHES)
  const latestSearches = computed(() => 
    recentSearches.value.slice(0, MAX_RECENT_SEARCHES)
  )
  
  // Computed pentru căutări grupate pe tip
  const searchesByType = computed(() => {
    return {
      stations: recentSearches.value.filter(s => s.type === 'station'),
      addresses: recentSearches.value.filter(s => s.type === 'address'),
      routes: recentSearches.value.filter(s => s.type === 'route')
    }
  })
  
  // Adaugă o căutare recentă
  const addRecentSearch = (search: Omit<RecentSearch, 'id' | 'timestamp'>): RecentSearch => {
    // Verifică dacă există deja o căutare similară
    const existingIndex = recentSearches.value.findIndex(s => 
      s.type === search.type && 
      s.query.toLowerCase() === search.query.toLowerCase() &&
      s.result.name === search.result.name
    )
    
    // Dacă există, șterge-o (o vom adăuga din nou la început)
    if (existingIndex !== -1) {
      recentSearches.value.splice(existingIndex, 1)
    }
    
    const newSearch: RecentSearch = {
      ...search,
      id: `search_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      timestamp: Date.now()
    }
    
    // Adaugă la început
    recentSearches.value.unshift(newSearch)
    
    // Limitează la MAX_RECENT_SEARCHES
    if (recentSearches.value.length > MAX_RECENT_SEARCHES) {
      recentSearches.value = recentSearches.value.slice(0, MAX_RECENT_SEARCHES)
    }
    
    saveRecentSearches()
    console.log('📝 Added recent search:', newSearch.query)
    
    return newSearch
  }
  
  // Șterge o căutare recentă
  const removeRecentSearch = (id: string): boolean => {
    const index = recentSearches.value.findIndex(s => s.id === id)
    if (index !== -1) {
      recentSearches.value.splice(index, 1)
      saveRecentSearches()
      return true
    }
    return false
  }
  
  // Șterge toate căutările recente
  const clearRecentSearches = () => {
    recentSearches.value = []
    saveRecentSearches()
    console.log('🗑️ Cleared all recent searches')
  }
  
  // Obține o căutare după ID
  const getRecentSearch = (id: string): RecentSearch | undefined => {
    return recentSearches.value.find(s => s.id === id)
  }
  
  // Formatează timpul relativ (ex: "acum 5 minute")
  const getRelativeTime = (timestamp: number): string => {
    const now = Date.now()
    const diff = now - timestamp
    
    const minutes = Math.floor(diff / 60000)
    const hours = Math.floor(diff / 3600000)
    const days = Math.floor(diff / 86400000)
    
    if (minutes < 1) return 'acum'
    if (minutes < 60) return `acum ${minutes} min`
    if (hours < 24) return `acum ${hours} ore`
    if (days < 7) return `acum ${days} zile`
    
    return new Date(timestamp).toLocaleDateString('ro-RO', {
      day: 'numeric',
      month: 'short'
    })
  }
  
  return {
    recentSearches: computed(() => recentSearches.value),
    latestSearches,
    searchesByType,
    addRecentSearch,
    removeRecentSearch,
    clearRecentSearches,
    getRecentSearch,
    getRelativeTime
  }
}
