<script setup lang="ts">
import { ref, onMounted } from 'vue'
import Sidebar from '../components/Sidebar.vue'
import MapView from '../components/MapView.vue'
import apiService, { type Station } from '../services/apiService'

// State pentru stațiile și traseul selectat
const selectedStations = ref<Station[]>([])
const selectedRouteId = ref<number | null>(null)
const allStations = ref<Station[]>([]) // Toate stațiile pentru search
const tripMode = ref(false) // Trip planning mode
const sidebarVisible = ref(true) // Sidebar visibility
// Folosim `any` pentru ref-ul componentei ca să evităm erori de tipare legate de InstanceType
const mapRef = ref<any>(null)

// Mapare culori pentru fiecare traseu
const routeColors: Record<number, string> = {
  1: '#FF0000',  // Linia 1 - Roșu
  2: '#0000FF',  // Linia 11 - Albastru
  3: '#00AA00'   // Linia 2 - Verde
}

// Încarcă toate stațiile la inițializare
const loadAllStations = async () => {
  try {
    allStations.value = await apiService.getStations()
    console.log('✅ Toate stațiile încărcate:', allStations.value.length)
  } catch (error) {
    console.error('❌ Eroare la încărcarea stațiilor:', error)
  }
}

// Handler pentru trip mode toggle
const handleTripModeChanged = (enabled: boolean) => {
  tripMode.value = enabled
  console.log(`🗓️ Trip mode: ${enabled ? 'ACTIVAT' : 'DEZACTIVAT'}`)
  
  // Pass trip mode to MapView
  if (mapRef.value && typeof mapRef.value.setTripMode === 'function') {
    mapRef.value.setTripMode(enabled)
  }
}

// Handler când un traseu este selectat din Sidebar
const handleRouteSelected = (routeId: number, stations: Station[]) => {
  console.log(`🚌 Traseu selectat: ${routeId}`)
  selectedStations.value = stations
  selectedRouteId.value = routeId
  
  // Centrează harta pe prima stație dacă există
  if (stations.length > 0 && mapRef.value && typeof mapRef.value.centerMap === 'function') {
    const firstStation = stations[0]
    if (!firstStation) return
    mapRef.value.centerMap(firstStation.latitude, firstStation.longitude, 14)
  }
}

// Handler pentru toggle sidebar
const handleSidebarToggle = (visible: boolean) => {
  sidebarVisible.value = visible
}

onMounted(() => {
  loadAllStations()
})
</script>

<template>
  <div class="app-container">
    <!-- Sidebar cu trasee și stații - mereu vizibil -->
    <Sidebar 
      class="sidebar"
      :class="{ 'sidebar-hidden': !sidebarVisible }"
      @route-selected="handleRouteSelected"
      @trip-mode-changed="handleTripModeChanged"
    />
    
    <!-- Harta ocupă restul ecranului -->
    <div class="map-wrapper">
      <MapView 
        ref="mapRef"
        :stations="selectedStations"
        :all-stations="allStations"
        :route-color="selectedRouteId ? routeColors[selectedRouteId] : '#2563eb'"
        @route-selected="handleRouteSelected"
        @sidebar-toggle="handleSidebarToggle"
      />
    </div>
  </div>
</template>

<style scoped>
/* Container (folosit pentru layout) */
.app-container {
  position: relative;
  height: 100vh;
  width: 100%;
  display: flex;
  overflow: hidden;
  background: #f8fafc;
}

/* Sidebar cu trasee - mereu vizibil */
.sidebar {
  flex-shrink: 0;
  width: 350px;
  height: 100vh;
  z-index: 100;
  transition: transform 0.3s ease-in-out;
}

.sidebar.sidebar-hidden {
  transform: translateX(-100%);
}

/* Map Wrapper */
.map-wrapper {
  flex: 1;
  height: 100vh;
  position: relative;
}

/* Mobile styles */
@media (max-width: 768px) {
  :root {
    --sidebar-width: 250px;
  }
  
  .sidebar {
    width: var(--sidebar-width);
  }
  
  .map-wrapper {
    margin-left: var(--sidebar-width);
  }
}
</style>
