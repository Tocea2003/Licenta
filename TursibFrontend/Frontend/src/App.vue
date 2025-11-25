<script setup lang="ts">
import { ref, onMounted } from 'vue'
import Sidebar from './components/Sidebar.vue'
import MapView from './components/MapView.vue'
import apiService, { type Station } from './services/apiService'

// State pentru stațiile și traseul selectat
const selectedStations = ref<Station[]>([])
const selectedRouteId = ref<number | null>(null)
const allStations = ref<Station[]>([]) // Toate stațiile pentru search
const sidebarOpen = ref(false) // State pentru sidebar pe mobile
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

onMounted(() => {
  loadAllStations()
})
</script>

<template>
  <div class="app-container">
    <!-- Buton hamburger pentru mobile -->
    <button class="hamburger-btn" @click="sidebarOpen = !sidebarOpen" :class="{ open: sidebarOpen }">
      <span></span>
      <span></span>
      <span></span>
    </button>
    
    <!-- Sidebar fixat în stânga -->
    <Sidebar class="sidebar" :class="{ open: sidebarOpen }" @route-selected="handleRouteSelected" />
    
    <!-- Overlay pentru a închide sidebar-ul pe mobile -->
    <div v-if="sidebarOpen" class="sidebar-overlay" @click="sidebarOpen = false"></div>
    
    <!-- Harta ocupă restul ecranului -->
    <div class="map-wrapper">
      <MapView 
        ref="mapRef"
        :stations="selectedStations"
        :all-stations="allStations"
        :route-color="selectedRouteId ? routeColors[selectedRouteId] : '#2563eb'"
        @route-selected="handleRouteSelected"
      />
    </div>
  </div>
</template>

<style>
:root {
  --sidebar-width: 250px; /* mai mic pentru desktop */
}

/* Reset global */
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body, #app {
  height: 100%;
  font-family: Inter, system-ui, Avenir, Helvetica, Arial, sans-serif;
  overflow: hidden; /* Previne scroll-bar-uri nedorite pe body */
}
/* Container (folosit pentru layout) */
.app-container {
  position: relative;
  height: 100vh;
  width: 100%;
}

/* Sidebar fixată în stânga */
.sidebar {
  position: fixed;
  left: 0;
  top: 0;
  bottom: 0;
  width: var(--sidebar-width);
  max-width: 40%;
  background: var(--sidebar-bg, #fff);
  z-index: 1000;
  overflow-y: auto;
  /* optional: umbră pentru separare */
  box-shadow: 2px 0 8px rgba(0,0,0,0.08);
}

/* Buton hamburger pentru mobile */
.hamburger-btn {
  display: none;
  position: fixed;
  top: 20px;
  left: 20px;
  z-index: 1002;
  width: 48px;
  height: 48px;
  background: white;
  border: none;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  cursor: pointer;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 0;
}

.hamburger-btn span {
  display: block;
  width: 24px;
  height: 3px;
  background: #3b82f6;
  border-radius: 2px;
  transition: all 0.3s ease;
}

.hamburger-btn.open span:nth-child(1) {
  transform: rotate(45deg) translate(6px, 6px);
}

.hamburger-btn.open span:nth-child(2) {
  opacity: 0;
}

.hamburger-btn.open span:nth-child(3) {
  transform: rotate(-45deg) translate(6px, -6px);
}

/* Overlay pentru sidebar pe mobile */
.sidebar-overlay {
  display: none;
}

/* Mobile: hide sidebar by default, show as overlay */
@media (max-width: 768px) {
  .hamburger-btn {
    display: flex;
  }
  
  .sidebar-overlay {
    display: block;
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.5);
    z-index: 999;
  }
  
  .sidebar {
    transform: translateX(-100%);
    transition: transform 0.3s ease;
    width: 85%;
    max-width: 320px;
    z-index: 1001;
  }
  
  .sidebar.open {
    transform: translateX(0);
  }
}

/* Wrapper pentru hartă - ocupă restul spațiului */
.map-wrapper {
  position: absolute;
  left: var(--sidebar-width);
  top: 0;
  right: 0;
  bottom: 0;
  align-items: stretch;
  justify-content: stretch;
  /* Dacă vrei scroll intern în map-wrapper, schimbă overflow */
  overflow: hidden;
}

/* Mobile: full width map */
@media (max-width: 768px) {
  .map-wrapper {
    left: 0;
    width: 100%;
  }
}

/* Dacă MapView intern are elemente absolute, asigură că ocupă 100% */
.map-wrapper > * {
  width: 100%;
  height: 100%;
}
</style>