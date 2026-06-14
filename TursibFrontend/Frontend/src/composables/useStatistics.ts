import { ref, computed } from 'vue'

export interface UserStatistics {
  totalSearches: number
  totalTrips: number
  favoriteRoutes: { routeId: number; count: number }[]
  favoriteStations: { stationId: number; count: number }[]
  totalTimeUsed: number // minutes
  searchHistory: { date: string; count: number }[]
  dailyActivity: { date: string; count: number }[]
  lastUpdated: number
}

const STORAGE_KEY = 'tursib_user_statistics'

// State global pentru statistici
const statistics = ref<UserStatistics>({
  totalSearches: 0,
  totalTrips: 0,
  favoriteRoutes: [],
  favoriteStations: [],
  totalTimeUsed: 0,
  searchHistory: [],
  dailyActivity: [],
  lastUpdated: Date.now()
})

let isInitialized = false

// Încarcă statisticile din localStorage
const loadStatistics = () => {
  if (isInitialized) return
  
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      statistics.value = JSON.parse(stored)
      console.log('✅ Statistics loaded')
    }
  } catch (error) {
    console.error('❌ Error loading statistics:', error)
  }
  
  isInitialized = true
}

// Salvează statisticile în localStorage
const saveStatistics = () => {
  try {
    statistics.value.lastUpdated = Date.now()
    localStorage.setItem(STORAGE_KEY, JSON.stringify(statistics.value))
  } catch (error) {
    console.error('❌ Error saving statistics:', error)
  }
}

const bumpDailyActivity = () => {
  if (!statistics.value.dailyActivity) statistics.value.dailyActivity = []
  const today = new Date().toISOString().split('T')[0]
  if (!today) return
  const entry = statistics.value.dailyActivity.find(e => e.date === today)
  if (entry) {
    entry.count++
  } else {
    statistics.value.dailyActivity.unshift({ date: today, count: 1 })
    if (statistics.value.dailyActivity.length > 30) {
      statistics.value.dailyActivity = statistics.value.dailyActivity.slice(0, 30)
    }
  }
}

export const useStatistics = () => {
  // Inițializează la prima utilizare
  if (!isInitialized) {
    loadStatistics()
  }

  // Incrementează contorul de căutări
  const incrementSearches = () => {
    statistics.value.totalSearches++
    bumpDailyActivity()

    const today = new Date().toISOString().split('T')[0]
    if (!today) return
    const existingEntry = statistics.value.searchHistory.find(entry => entry.date === today)
    
    if (existingEntry) {
      existingEntry.count++
    } else {
      statistics.value.searchHistory.unshift({ date: today, count: 1 })
      // Păstrează doar ultimele 30 de zile
      if (statistics.value.searchHistory.length > 30) {
        statistics.value.searchHistory = statistics.value.searchHistory.slice(0, 30)
      }
    }
    
    saveStatistics()
  }
  
  // Incrementează contorul de călătorii
  const incrementTrips = () => {
    statistics.value.totalTrips++
    bumpDailyActivity()
    saveStatistics()
  }
  
  // Adaugă timp de utilizare (în minute)
  const addTimeUsed = (minutes: number) => {
    statistics.value.totalTimeUsed += minutes
    saveStatistics()
  }
  
  // Înregistrează folosirea unui traseu
  const recordRouteUsage = (routeId: number) => {
    bumpDailyActivity()
    const existing = statistics.value.favoriteRoutes.find(r => r.routeId === routeId)
    
    if (existing) {
      existing.count++
    } else {
      statistics.value.favoriteRoutes.push({ routeId, count: 1 })
    }
    
    // Sortează și păstrează top 10
    statistics.value.favoriteRoutes.sort((a, b) => b.count - a.count)
    if (statistics.value.favoriteRoutes.length > 10) {
      statistics.value.favoriteRoutes = statistics.value.favoriteRoutes.slice(0, 10)
    }
    
    saveStatistics()
  }
  
  // Înregistrează folosirea unei stații
  const recordStationUsage = (stationId: number) => {
    bumpDailyActivity()
    const existing = statistics.value.favoriteStations.find(s => s.stationId === stationId)
    
    if (existing) {
      existing.count++
    } else {
      statistics.value.favoriteStations.push({ stationId, count: 1 })
    }
    
    // Sortează și păstrează top 10
    statistics.value.favoriteStations.sort((a, b) => b.count - a.count)
    if (statistics.value.favoriteStations.length > 10) {
      statistics.value.favoriteStations = statistics.value.favoriteStations.slice(0, 10)
    }
    
    saveStatistics()
  }
  
  // Resetează statisticile
  const resetStatistics = () => {
    statistics.value = {
      totalSearches: 0,
      totalTrips: 0,
      favoriteRoutes: [],
      favoriteStations: [],
      totalTimeUsed: 0,
      searchHistory: [],
      dailyActivity: [],
      lastUpdated: Date.now()
    }
    saveStatistics()
    console.log('🗑️ Statistics reset')
  }
  
  // Computed properties
  const averageSearchesPerDay = computed(() => {
    if (statistics.value.searchHistory.length === 0) return 0
    const totalSearches = statistics.value.searchHistory.reduce((sum, entry) => sum + entry.count, 0)
    return Math.round(totalSearches / statistics.value.searchHistory.length)
  })
  
  const mostUsedRoute = computed(() => {
    if (statistics.value.favoriteRoutes.length === 0) return null
    return statistics.value.favoriteRoutes[0]
  })
  
  const mostUsedStation = computed(() => {
    if (statistics.value.favoriteStations.length === 0) return null
    return statistics.value.favoriteStations[0]
  })
  
  return {
    statistics: computed(() => statistics.value),
    incrementSearches,
    incrementTrips,
    addTimeUsed,
    recordRouteUsage,
    recordStationUsage,
    resetStatistics,
    averageSearchesPerDay,
    mostUsedRoute,
    mostUsedStation
  }
}
