<script setup lang="ts">
import { ref, onMounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Sidebar from '../components/Sidebar.vue'
import MapView from '../components/MapView.vue'
import BottomNav from '@/components/BottomNav.vue'
import apiService, { type Station } from '../services/apiService'
import type { PlanResult } from '../components/Sidebar.vue'

const selectedStations = ref<Station[]>([])
const selectedRouteId = ref<number | null>(null)
const allStations = ref<Station[]>([])
const sidebarVisible = ref(window.innerWidth >= 768)
const activeTripPlan = ref<PlanResult | null>(null)
const mapRef = ref<any>(null)
const sidebarRef = ref<any>(null)

const vueRoute = useRoute()
const router = useRouter()

watch(() => vueRoute.query.tab, (tab) => {
  if (tab === 'plan') {
    sidebarVisible.value = true
    nextTick(() => {
      sidebarRef.value?.openPlanTab()
      router.replace({ query: {} })
    })
  }
}, { immediate: true })

watch(() => vueRoute.query, (query) => {
  if (query.lat && query.lon) {
    const lat = parseFloat(query.lat as string)
    const lon = parseFloat(query.lon as string)
    const zoom = query.zoom ? parseInt(query.zoom as string) : 17
    if (!isNaN(lat) && !isNaN(lon)) {
      nextTick(() => {
        if (mapRef.value?.showAddressLocation) {
          mapRef.value.showAddressLocation({ lat, lon, name: (query.label as string) || '' })
        } else if (mapRef.value?.centerMap) {
          mapRef.value.centerMap(lat, lon, zoom)
        }
        router.replace({ query: {} })
      })
    }
  }
})

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

const handleLocationSelected = (location: { lat: number; lon: number; name: string }) => {
  if (mapRef.value && typeof mapRef.value.showAddressLocation === 'function') {
    mapRef.value.showAddressLocation(location)
    return
  }

  if (mapRef.value && typeof mapRef.value.centerMap === 'function') {
    mapRef.value.centerMap(location.lat, location.lon, 17)
  }
}

// Handler pentru toggle sidebar
const handleSidebarToggle = (visible: boolean) => {
  sidebarVisible.value = visible
}

const closeSidebarOnMobile = () => {
  if (window.innerWidth < 768) {
    sidebarVisible.value = false
    mapRef.value?.setSidebarOpen(false)
  }
}

// Watch pentru schimbări în vizibilitatea sidebar-ului
watch(sidebarVisible, () => {
  // Single call after the sidebar CSS transition ends (~350ms)
  setTimeout(() => {
    if (mapRef.value && typeof mapRef.value.invalidateSize === 'function') {
      mapRef.value.invalidateSize()
    }
  }, 350)
})

onMounted(() => {
  loadAllStations()
})
</script>

<template>
  <div class="app-container">
    <!-- Overlay pentru închiderea sidebar-ului pe mobile -->
    <div
      v-if="sidebarVisible"
      class="mobile-overlay"
      @click="closeSidebarOnMobile"
    />

    <!-- Sidebar cu trasee și stații -->
    <Sidebar
      ref="sidebarRef"
      class="sidebar"
      :class="{ 'sidebar-hidden': !sidebarVisible, 'sidebar-visible': sidebarVisible }"
      :all-stations="allStations"
      @route-selected="handleRouteSelected"
      @plan-selected="handlePlanSelected"
      @location-selected="handleLocationSelected"
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

    <!-- Bottom Navigation -->
    <BottomNav />
  </div>
</template>

<style scoped>
/* Container (folosit pentru layout) */
.app-container {
  position: relative;
  height: 100dvh;
  width: 100%;
  display: flex;
  overflow: hidden;
  background: var(--bg-secondary);
}

/* Sidebar cu trasee - mai îngust și modern */
.sidebar {
  flex-shrink: 0;
  width: 320px;
  height: calc(100vh - 24px);
  margin: 12px 0 12px 12px;
  border-radius: 20px;
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

/* Overlay - ascuns pe desktop */
.mobile-overlay {
  display: none;
}

/* Map Wrapper */
.map-wrapper {
  flex: 1;
  height: 100dvh;
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
@media (max-width: 767px) {
  .sidebar {
    position: fixed;
    left: 0;
    top: 0;
    margin: 0;
    height: 100vh;
    border-radius: 0;
    width: 280px;
    z-index: 1001;
    transform: translateX(-100%);
  }
  
  .sidebar.sidebar-visible {
    transform: translateX(0);
  }
  
  .map-wrapper {
    width: 100%;
    padding-bottom: 70px; /* Space pentru bottom nav */
  }
  
  /* Overlay când sidebar e deschis pe mobile */
  .mobile-overlay {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    z-index: 1150;
    animation: fadeIn 0.3s ease;
  }

  @keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
  }

  .sidebar {
    z-index: 1200;
  }
}
</style>
