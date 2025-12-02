<template>
  <aside class="sidebar">
    <div class="sidebar-header">
      <h1>🚌 Tursib Tracker</h1>
      <p>Sibiu - Transport Public</p>
    </div>

    <div class="sidebar-content">
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
import { ref, onMounted } from 'vue'
import apiService, { type Route, type Station } from '@/services/apiService'

// Emits pentru comunicare cu componenta părinte
const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
}>()

// State
const routes = ref<Route[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedRouteId = ref<number | null>(null)
const currentStations = ref<Station[]>([])
const loadingStations = ref(false)

// Încarcă toate traseele la montarea componentei
const loadRoutes = async () => {
  loading.value = true
  error.value = null
  
  try {
    routes.value = await apiService.getRoutes()
    console.log('✅ Trasee încărcate:', routes.value)
  } catch (err: any) {
    console.error('❌ Eroare la încărcarea traseelor:', err)
    error.value = 'Nu s-au putut încărca traseele. Verifică dacă API-ul rulează.'
  } finally {
    loading.value = false
  }
}

// Selectează un traseu și încarcă stațiile lui
const selectRoute = async (routeId: number) => {
  selectedRouteId.value = routeId
  loadingStations.value = true
  
  try {
    const stations = await apiService.getRouteStations(routeId)
    currentStations.value = stations
    console.log(`✅ Stații pentru traseul ${routeId}:`, stations)
    
    // Emite eveniment către componenta părinte (pentru hartă)
    emit('routeSelected', routeId, stations)
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
  background: #1f2937;
  color: white;
  display: flex;
  flex-direction: column;
  box-shadow: 2px 0 10px rgba(0, 0, 0, 0.3);
}

.sidebar-header {
  padding: 2rem 1.5rem;
  background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
  border-bottom: 2px solid #1e40af;
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

.retry-btn {
  margin-top: 1rem;
  padding: 0.5rem 1.5rem;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
}

.retry-btn:hover {
  background: #1d4ed8;
}

/* Routes Section */
.routes-section h2 {
  font-size: 1.1rem;
  margin: 0 0 1rem 0;
  color: #e5e7eb;
}

.routes-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.route-btn {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: #374151;
  border: 2px solid transparent;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  color: white;
}

.route-btn:hover {
  background: #4b5563;
  border-color: #2563eb;
}

.route-btn.active {
  background: #2563eb;
  border-color: #1d4ed8;
}

.route-number {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 40px;
  height: 40px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  font-weight: bold;
  font-size: 1.1rem;
}

.route-name {
  flex: 1;
  font-size: 0.95rem;
}

/* Selected Route Info */
.selected-route-info {
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid #374151;
}

.selected-route-info h3 {
  font-size: 1rem;
  margin: 0 0 1rem 0;
  color: #e5e7eb;
}

.loading-small {
  color: #9ca3af;
  font-size: 0.9rem;
  font-style: italic;
}

.stations-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.stations-list li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #374151;
  border-radius: 6px;
  font-size: 0.9rem;
}

.station-number {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 28px;
  height: 28px;
  background: #2563eb;
  border-radius: 50%;
  font-weight: bold;
  font-size: 0.85rem;
}

.station-name {
  flex: 1;
}

.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  color: #9ca3af;
}

/* Scrollbar styling */
.sidebar-content::-webkit-scrollbar {
  width: 8px;
}

.sidebar-content::-webkit-scrollbar-track {
  background: #1f2937;
}

.sidebar-content::-webkit-scrollbar-thumb {
  background: #4b5563;
  border-radius: 4px;
}

.sidebar-content::-webkit-scrollbar-thumb:hover {
  background: #6b7280;
}
</style>