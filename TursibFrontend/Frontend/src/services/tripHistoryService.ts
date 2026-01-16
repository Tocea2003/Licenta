// Service pentru gestionarea istoricului de călătorii

export interface TripHistoryItem {
  id: string
  timestamp: number
  startLocation: string
  endLocation: string
  startCoords: { lat: number; lon: number }
  endCoords: { lat: number; lon: number }
  routeType: 'direct' | 'transfer'
  routeDetails: {
    busLines: string[]
    totalDuration: number
    totalStations: number
    transferStation?: string
  }
}

const STORAGE_KEY = 'tursib_trip_history'
const MAX_HISTORY_ITEMS = 50 // Păstrăm ultimele 50 de călătorii

class TripHistoryService {
  /**
   * Salvează o călătorie în istoric
   */
  saveTrip(trip: Omit<TripHistoryItem, 'id' | 'timestamp'>): void {
    try {
      const history = this.getHistory()
      
      const newTrip: TripHistoryItem = {
        ...trip,
        id: this.generateId(),
        timestamp: Date.now()
      }
      
      // Adaugă la început (cele mai recente primele)
      history.unshift(newTrip)
      
      // Păstrează doar ultimele MAX_HISTORY_ITEMS
      const trimmedHistory = history.slice(0, MAX_HISTORY_ITEMS)
      
      localStorage.setItem(STORAGE_KEY, JSON.stringify(trimmedHistory))
      
      console.log('✅ Călătorie salvată în istoric:', newTrip)
    } catch (error) {
      console.error('❌ Eroare la salvarea călătoriei:', error)
    }
  }
  
  /**
   * Obține tot istoricul de călătorii
   */
  getHistory(): TripHistoryItem[] {
    try {
      const data = localStorage.getItem(STORAGE_KEY)
      if (!data) return []
      
      return JSON.parse(data) as TripHistoryItem[]
    } catch (error) {
      console.error('❌ Eroare la citirea istoricului:', error)
      return []
    }
  }
  
  /**
   * Obține o călătorie specifică după ID
   */
  getTripById(id: string): TripHistoryItem | null {
    const history = this.getHistory()
    return history.find(trip => trip.id === id) || null
  }
  
  /**
   * Șterge o călătorie din istoric
   */
  deleteTrip(id: string): void {
    try {
      const history = this.getHistory()
      const filtered = history.filter(trip => trip.id !== id)
      localStorage.setItem(STORAGE_KEY, JSON.stringify(filtered))
      console.log('✅ Călătorie ștearsă din istoric:', id)
    } catch (error) {
      console.error('❌ Eroare la ștergerea călătoriei:', error)
    }
  }
  
  /**
   * Șterge tot istoricul
   */
  clearHistory(): void {
    try {
      localStorage.removeItem(STORAGE_KEY)
      console.log('✅ Istoric șters complet')
    } catch (error) {
      console.error('❌ Eroare la ștergerea istoricului:', error)
    }
  }
  
  /**
   * Caută în istoric după text
   */
  searchHistory(query: string): TripHistoryItem[] {
    const history = this.getHistory()
    const lowerQuery = query.toLowerCase()
    
    return history.filter(trip => 
      trip.startLocation.toLowerCase().includes(lowerQuery) ||
      trip.endLocation.toLowerCase().includes(lowerQuery) ||
      trip.routeDetails.busLines.some(line => line.toLowerCase().includes(lowerQuery))
    )
  }
  
  /**
   * Obține călătoriile recente (ultimele N)
   */
  getRecentTrips(count: number = 10): TripHistoryItem[] {
    const history = this.getHistory()
    return history.slice(0, count)
  }
  
  /**
   * Verifică dacă există istorie
   */
  hasHistory(): boolean {
    return this.getHistory().length > 0
  }
  
  /**
   * Generează un ID unic pentru călătorie
   */
  private generateId(): string {
    return `trip_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`
  }
  
  /**
   * Formatează data pentru afișare
   */
  formatDate(timestamp: number): string {
    const date = new Date(timestamp)
    const now = new Date()
    const diffMs = now.getTime() - date.getTime()
    const diffMins = Math.floor(diffMs / 60000)
    const diffHours = Math.floor(diffMs / 3600000)
    const diffDays = Math.floor(diffMs / 86400000)
    
    // Astăzi
    if (diffDays === 0) {
      if (diffMins < 60) {
        return diffMins <= 1 ? 'Acum un minut' : `Acum ${diffMins} minute`
      }
      return diffHours === 1 ? 'Acum o oră' : `Acum ${diffHours} ore`
    }
    
    // Ieri
    if (diffDays === 1) {
      return `Ieri la ${date.toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })}`
    }
    
    // Ultima săptămână
    if (diffDays < 7) {
      const days = ['Duminică', 'Luni', 'Marți', 'Miercuri', 'Joi', 'Vineri', 'Sâmbătă']
      return `${days[date.getDay()]} la ${date.toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })}`
    }
    
    // Mai vechi
    return date.toLocaleDateString('ro-RO', { 
      day: 'numeric', 
      month: 'short', 
      year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined,
      hour: '2-digit',
      minute: '2-digit'
    })
  }
  
  /**
   * Formatează durata în text
   */
  formatDuration(minutes: number): string {
    if (minutes < 60) {
      return `${Math.round(minutes)} min`
    }
    const hours = Math.floor(minutes / 60)
    const mins = Math.round(minutes % 60)
    return mins > 0 ? `${hours}h ${mins}min` : `${hours}h`
  }
}

// Export singleton
export const tripHistoryService = new TripHistoryService()
