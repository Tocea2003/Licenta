<script setup lang="ts">
import { ref } from 'vue'
import Sidebar from './components/Sidebar.vue'
import MapView from './components/MapView.vue'
import type { Station } from './services/apiService'

// State pentru stațiile și traseul selectat
const selectedStations = ref<Station[]>([])
const mapRef = ref<InstanceType<typeof MapView> | null>(null)

// Handler când un traseu este selectat din Sidebar
const handleRouteSelected = (routeId: number, stations: Station[]) => {
  console.log(`🚌 Traseu selectat: ${routeId}`)
  selectedStations.value = stations
  
  // Centrează harta pe prima stație dacă există
  if (stations.length > 0 && mapRef.value) {
    const firstStation = stations[0]
    mapRef.value.centerMap(firstStation.latitude, firstStation.longitude, 14)
  }
}
</script>

<template>
  <div class="app-container">
    <!-- Sidebar cu lista de trasee -->
    <Sidebar @route-selected="handleRouteSelected" />
    
    <!-- Harta cu stațiile -->
    <div class="map-wrapper">
      <MapView 
        ref="mapRef"
        :stations="selectedStations"
        :route-color="#2563eb"
      />
    </div>
  </div>
</template>

<style>
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body {
  height: 100%;
  font-family: Inter, system-ui, Avenir, Helvetica, Arial, sans-serif;
}

#app {
  height: 100%;
}

.app-container {
  display: flex;
  height: 100%;
  width: 100%;
}

.map-wrapper {
  flex: 1;
  height: 100%;
  position: relative;
}
</style>

</style>
