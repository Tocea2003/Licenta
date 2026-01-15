<template>
  <aside class="sidebar">
    <div class="sidebar-header">
      <h1>🚌 Tursib Tracker</h1>
      <p>Sibiu - Transport Public</p>
    </div>

    <div class="sidebar-content">
      <!-- Quick Actions -->
      <div class="quick-actions">
        <button @click="toggleTripMode" class="action-btn trip-btn" :class="{ active: tripMode }">
          <span class="icon">🗓️</span>
          <span class="label">Planifică</span>
        </button>
        <button @click="goToFavorites" class="action-btn favorites-btn">
          <span class="icon">❤️</span>
          <span class="label">Favorite</span>
        </button>
        <button @click="goToStatistics" class="action-btn stats-btn">
          <span class="icon">📊</span>
          <span class="label">Statistici</span>
        </button>
      </div>

      <!-- Quick Stats -->
      <div class="quick-stats">
        <div class="stat-card">
          <div class="stat-value">{{ routes.length }}</div>
          <div class="stat-label">Trasee</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ favoriteCount }}</div>
          <div class="stat-label">Favorite</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ searchCount }}</div>
          <div class="stat-label">Căutări</div>
        </div>
      </div>

      <!-- Loading State -->
      <div v-if="loading" class="loading">
        <div class="spinner"></div>
        <p>Se încarcă traseele...</p>
      </div>

      <!-- Error State -->
      <div v-else-if="error" class="error">
        <p>❌ {{ error }}</p>
        <button @click="loadRoutes" class="retry-btn">Reîncearcă</button>
      </div>

      <!-- Routes List -->
      <div v-else class="routes-section">
        <h2>Trasee Disponibile</h2>
        
        <div v-if="routes.length === 0" class="empty-state">
          <p>Nu există trasee disponibile.</p>
        </div>

        <div v-else class="routes-list">
          <button
            v-for="route in routes"
            :key="route.id"
            class="route-btn"
            :class="{ active: selectedRouteId === route.id }"
            @click="selectRoute(route.id)"
          >
            <span class="route-number">{{ route.routeNumber }}</span>
            <span class="route-name">{{ route.name }}</span>
          </button>
        </div>
      </div>

      <!-- Selected Route Info -->
      <div v-if="selectedRouteId" class="selected-route-info">
        <h3>📍 Stații pe traseu:</h3>
        <div v-if="loadingStations" class="loading-small">
          Se încarcă stațiile...
        </div>
        <ul v-else class="stations-list">
          <li v-for="(station, index) in currentStations" :key="station.id">
            <span class="station-number">{{ index + 1 }}</span>
            <span class="station-name">{{ station.name }}</span>
          </li>
        </ul>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import apiService, { type Route, type Station } from '@/services/apiService'
import { useFavorites } from '@/composables/useFavorites'
import { useStatistics } from '@/composables/useStatistics'

// Router
const router = useRouter()

// Composables
const { favorites } = useFavorites()
const { statistics, recordRouteUsage } = useStatistics()

// Emits pentru comunicare cu componenta părinte
const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  tripModeChanged: [enabled: boolean]
}>()

// State
const routes = ref<Route[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedRouteId = ref<number | null>(null)
const currentStations = ref<Station[]>([])
const loadingStations = ref(false)
const tripMode = ref(false)

// Computed stats
const favoriteCount = computed(() => favorites.value.length)
const searchCount = computed(() => statistics.value.totalSearches)

// Quick actions
const goToFavorites = () => router.push('/favorites')
const goToStatistics = () => router.push('/statistics')

// Toggle trip planning mode
const toggleTripMode = () => {
  tripMode.value = !tripMode.value
  emit('tripModeChanged', tripMode.value)
  
  // Reset selection when switching modes
  if (tripMode.value) {
    selectedRouteId.value = null
    currentStations.value = []
  }
}

// Încarcă toate traseele la montarea componentei
const loadRoutes = async () => {
  loading.value = true
  error.value = null
  
  try {
    // Verifică cache-ul localStorage
    const cachedRoutes = localStorage.getItem('routes')
    const cachedTimestamp = localStorage.getItem('routesTimestamp')
    
    // Cache valid 30 minute
    if (cachedRoutes && cachedTimestamp && Date.now() - parseInt(cachedTimestamp) < 1800000) {
      routes.value = JSON.parse(cachedRoutes)
      console.log('✅ Trasee încărcate din cache:', routes.value.length)
    } else {
      // Încarcă din API
      const fetchedRoutes = await apiService.getRoutes()
      routes.value = fetchedRoutes
      
      // Salvează în cache
      localStorage.setItem('routes', JSON.stringify(fetchedRoutes))
      localStorage.setItem('routesTimestamp', Date.now().toString())
      
      console.log('✅ Trasee încărcate din API:', routes.value.length)
    }
  } catch (err: any) {
    console.error('❌ Eroare la încărcarea traseelor:', err)
    error.value = 'Nu s-au putut încărca traseele. Verifică dacă API-ul rulează.'
  } finally {
    loading.value = false
  }
}

// Cache pentru stații
const stationsCache = new Map<number, Station[]>()

// Selectează un traseu și încarcă stațiile lui cu cache
const selectRoute = async (routeId: number) => {
  // Track route usage for statistics
  recordRouteUsage(routeId)
  
  selectedRouteId.value = routeId
  loadingStations.value = true
  
  try {
    // Verifică cache-ul
    if (stationsCache.has(routeId)) {
      currentStations.value = stationsCache.get(routeId)!
      console.log(`✅ Stații pentru traseul ${routeId} din cache:`, currentStations.value.length)
    } else {
      // Încarcă din API
      const stations = await apiService.getRouteStations(routeId)
      currentStations.value = stations
      stationsCache.set(routeId, stations)
      console.log(`✅ Stații pentru traseul ${routeId} din API:`, stations.length)
    }
    
    // Emite eveniment către componenta părinte (pentru hartă)
    emit('routeSelected', routeId, currentStations.value)
  } catch (err) {
    console.error(`❌ Eroare la încărcarea stațiilor pentru traseul ${routeId}:`, err)
    currentStations.value = []
  } finally {
    loadingStations.value = false
  }
}

// Încarcă traseele la montare
onMounted(() => {
  loadRoutes()
})
</script>

<style scoped>
.sidebar {
  width: 100%;
  height: 100vh;
  background: var(--bg-secondary);
  color: var(--text-primary);
  display: flex;
  flex-direction: column;
  box-shadow: var(--shadow-lg);
}

.sidebar-header {
  padding: 2rem 1.5rem;
  background: var(--gradient-primary);
  border-bottom: 2px solid rgba(59, 130, 246, 0.3);
}

.sidebar-header h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: bold;
}

.sidebar-header p {
  margin: 0.5rem 0 0 0;
  opacity: 0.9;
  font-size: 0.9rem;
}

.sidebar-content {
  flex: 1;
  overflow-y: auto;
  padding: 1.5rem;
}

/* Trip Planner Button */
.quick-actions {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-bottom: 16px;
}

.action-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 12px 8px;
  background: var(--bg-primary);
  color: var(--text-primary);
  border: 2px solid var(--border-color);
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: var(--shadow-sm);
  gap: 6px;
}

.action-btn:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
  border-color: #3b82f6;
}

.action-btn .icon {
  font-size: 24px;
}

.action-btn .label {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.trip-btn.active {
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  border-color: transparent;
  color: white;
  box-shadow: 0 2px 8px rgba(16, 185, 129, 0.3);
}

.favorites-btn:hover {
  border-color: #ef4444;
  color: #ef4444;
}

.stats-btn:hover {
  border-color: #8b5cf6;
  color: #8b5cf6;
}

/* Quick Stats */
.quick-stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-bottom: 20px;
}

.stat-card {
  background: var(--gradient-primary);
  padding: 12px;
  border-radius: 12px;
  text-align: center;
  box-shadow: var(--shadow-sm);
  border: 1px solid rgba(59, 130, 246, 0.3);
}

.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: white;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 10px;
  text-transform: uppercase;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.85);
  letter-spacing: 0.5px;
}

/* Loading State */
.loading {
  text-align: center;
  padding: 2rem 1rem;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid rgba(255, 255, 255, 0.1);
  border-top-color: #2563eb;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin: 0 auto 1rem;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Error State */
.error {
  text-align: center;
  padding: 2rem 1rem;
  color: #ef4444;
}

.error-message {
  background: #fef2f2;
  color: #991b1b;
  padding: 1rem;
  border-radius: 10px;
  border: 1px solid #fecaca;
  margin: 1rem 0;
  font-size: 0.875rem;
}

.retry-btn {
  margin-top: 0.75rem;
  padding: 10px 18px;
  background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 600;
  transition: all 0.2s;
  box-shadow: 0 2px 6px rgba(239, 68, 68, 0.25);
}

.retry-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 10px rgba(239, 68, 68, 0.3);
}

/* Routes Section */
.routes-section h2 {
  font-size: 0.95rem;
  margin: 0 0 0.75rem 0;
  color: var(--text-secondary);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.routes-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.route-btn {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: var(--bg-primary);
  border: 2px solid var(--border-color);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  text-align: left;
  color: var(--text-primary);
}

.route-btn:hover {
  background: var(--bg-tertiary);
  border-color: #3b82f6;
  transform: translateX(2px);
  box-shadow: var(--shadow-sm);
}

.route-btn.active {
  background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
  border-color: transparent;
  color: white;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
}

.route-number {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 36px;
  height: 36px;
  background: white;
  border-radius: 8px;
  font-weight: 700;
  font-size: 1rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  color: #1f2937;
}

.route-btn.active .route-number {
  background: rgba(255, 255, 255, 0.25);
  box-shadow: none;
  color: white;
}

.route-name {
  flex: 1;
  font-size: 0.875rem;
  font-weight: 500;
}

/* Selected Route Info */
.selected-route-info {
  margin-top: 1.5rem;
  padding-top: 1.25rem;
  border-top: 2px solid var(--border-color);
}

.selected-route-info h3 {
  font-size: 0.875rem;
  margin: 0 0 0.75rem 0;
  color: var(--text-secondary);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.loading-small {
  color: var(--text-tertiary);
  font-size: 0.85rem;
  font-style: italic;
  padding: 1rem;
  text-align: center;
}

.stations-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  max-height: 300px;
  overflow-y: auto;
}

.stations-list li {
  display: flex;
  align-items: center;
  gap: 0.65rem;
  padding: 0.65rem 0.75rem;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  font-size: 0.85rem;
  color: var(--text-primary);
  transition: all 0.15s;
}

.stations-list li:hover {
  background: var(--bg-tertiary);
  border-color: #bfdbfe;
  transform: translateX(2px);
}

.station-number {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 24px;
  background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
  border-radius: 50%;
  font-weight: 700;
  font-size: 0.75rem;
  color: white;
  flex-shrink: 0;
}

.station-name {
  flex: 1;
  font-weight: 500;
  line-height: 1.3;
}

.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  color: var(--text-tertiary);
  font-size: 0.875rem;
}

/* Scrollbar styling - modern și subtil */
.sidebar-content::-webkit-scrollbar,
.stations-list::-webkit-scrollbar {
  width: 6px;
}

.sidebar-content::-webkit-scrollbar-track,
.stations-list::-webkit-scrollbar-track {
  background: transparent;
}

.sidebar-content::-webkit-scrollbar-thumb,
.stations-list::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 3px;
}

.sidebar-content::-webkit-scrollbar-thumb:hover,
.stations-list::-webkit-scrollbar-thumb:hover {
  background: #9ca3af;
}
</style>