<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from 'vue'
import Sidebar from '../components/Sidebar.vue'
import MapView from '../components/MapView.vue'
import apiService, { type Station } from '../services/apiService'
import type { PlanResult } from '../components/Sidebar.vue'

const selectedStations = ref<Station[]>([])
const selectedRouteId = ref<number | null>(null)
const allStations = ref<Station[]>([])
const sidebarVisible = ref(true)
const activeTripPlan = ref<PlanResult | null>(null)
const mapRef = ref<any>(null)

// Mapare culori pentru fiecare traseu
const routeColors: Record<number, string> = {
  1: '#FF0000',  // Linia 1 - Roșu
  2: '#0000FF',  // Linia 11 - Albastru
  3: '#00AA00'   // Linia 2 - Verde
}

const loadAllStations = async () => {
  try {
    allStations.value = await apiService.getStations()
  } catch {}
}

const handleRouteSelected = (routeId: number, stations: Station[]) => {
  selectedStations.value = stations
  selectedRouteId.value = routeId

  // Centrează harta pe prima stație dacă există
  if (stations.length > 0 && mapRef.value && typeof mapRef.value.centerMap === 'function') {
    const firstStation = stations[0]
    if (!firstStation) return
    mapRef.value.centerMap(firstStation.latitude, firstStation.longitude, 14)
  }
}

const handlePlanSelected = (plan: PlanResult) => {
  activeTripPlan.value = plan
}

// Handler pentru toggle sidebar
const handleSidebarToggle = (visible: boolean) => {
  sidebarVisible.value = visible
}

// Watch pentru schimbări în vizibilitatea sidebar-ului
watch(sidebarVisible, () => {
  // Forțează refresh-ul hărții de mai multe ori pentru siguranță
  nextTick(() => {
    // Imediat după ce DOM-ul se actualizează
    if (mapRef.value && typeof mapRef.value.invalidateSize === 'function') {
      mapRef.value.invalidateSize()
    }
    
    // După 100ms
    setTimeout(() => {
      if (mapRef.value && typeof mapRef.value.invalidateSize === 'function') {
        mapRef.value.invalidateSize()
      }
    }, 100)
    
    // După ce animația se termină (350ms)
    setTimeout(() => {
      if (mapRef.value && typeof mapRef.value.invalidateSize === 'function') {
        mapRef.value.invalidateSize()
      }
    }, 350)
    
    // Un ultim refresh la 500ms
    setTimeout(() => {
      if (mapRef.value && typeof mapRef.value.invalidateSize === 'function') {
        mapRef.value.invalidateSize()
      }
    }, 500)
  })
})

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
      :all-stations="allStations"
      @route-selected="handleRouteSelected"
      @plan-selected="handlePlanSelected"
    />
    
    <!-- Harta ocupă restul ecranului -->
    <div class="map-wrapper">
      <MapView
        ref="mapRef"
        :stations="selectedStations"
        :all-stations="allStations"
        :route-color="selectedRouteId ? routeColors[selectedRouteId] : '#2563eb'"
        :trip-plan="activeTripPlan"
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
  background: var(--bg-secondary);
}

/* Sidebar cu trasee - mai îngust și modern */
.sidebar {
  flex-shrink: 0;
  width: 320px;
  height: 100vh;
  z-index: 100;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 2px 0 12px rgba(0, 0, 0, 0.08);
  position: relative;
}

.sidebar.sidebar-hidden {
  width: 0;
  min-width: 0;
  box-shadow: none;
  overflow: hidden;
}

/* Map Wrapper */
.map-wrapper {
  flex: 1;
  height: 100vh;
  position: relative;
  min-width: 0;
  overflow: hidden;
}

/* Desktop - adaugă padding bottom pentru bottom nav */
@media (min-width: 769px) {
  .app-container {
    padding-bottom: 0;
  }
}

/* Mobile styles - ascunde sidebar, afișează bottom nav */
@media (max-width: 768px) {
  .sidebar {
    position: fixed;
    left: 0;
    top: 0;
    width: 300px;
    height: 100vh;
    z-index: 1001;
    transform: translateX(-100%);
    transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    /* Override desktop width: 0 collapse */
    min-width: 300px !important;
    overflow: visible !important;
    box-shadow: 4px 0 24px rgba(0, 0, 0, 0.15) !important;
  }

  .sidebar.sidebar-visible {
    transform: translateX(0);
  }

  /* Desktop sidebar-hidden has no effect on mobile */
  .sidebar.sidebar-hidden {
    width: 300px !important;
    min-width: 300px !important;
    overflow: visible !important;
    box-shadow: none !important;
  }

  .map-wrapper {
    width: 100%;
    padding-bottom: env(safe-area-inset-bottom, 70px);
    padding-bottom: max(70px, calc(70px + env(safe-area-inset-bottom)));
  }

  /* Overlay when sidebar is open on mobile */
  .app-container::before {
    content: '';
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    z-index: 1000;
    opacity: 0;
    pointer-events: none;
    transition: opacity 0.3s;
  }

  .app-container:has(.sidebar.sidebar-visible)::before {
    opacity: 1;
    pointer-events: auto;
  }
}
</style>
