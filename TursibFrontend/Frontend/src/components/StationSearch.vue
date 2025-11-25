<template>
  <div class="search-container">
    <input
      v-model="searchQuery"
      type="text"
      placeholder="🔍 Caută stație..."
      class="search-input"
      @focus="showResults = true"
    />
    
    <div v-if="showResults && filteredStations.length > 0" class="search-results">
      <div
        v-for="station in filteredStations"
        :key="station.id"
        class="search-result-item"
        @click="selectStation(station)"
      >
        <span class="station-name">{{ station.name }}</span>
        <span v-if="userLocation" class="station-distance">
          {{ calculateDistance(station) }} km
        </span>
      </div>
    </div>
    
    <div v-if="showResults && searchQuery && filteredStations.length === 0" class="no-results">
      Nu s-au găsit stații
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Station } from '@/services/apiService'

interface Props {
  stations: Station[]
  userLocation?: { lat: number; lon: number } | null
}

const props = withDefaults(defineProps<Props>(), {
  stations: () => [],
  userLocation: null
})

const emit = defineEmits<{
  stationSelected: [station: Station]
}>()

const searchQuery = ref('')
const showResults = ref(false)

// Filtrează stațiile după query
const filteredStations = computed(() => {
  if (!searchQuery.value.trim()) {
    // Dacă nu caută, arată cele mai apropiate de utilizator
    if (props.userLocation && props.stations.length > 0) {
      return [...props.stations]
        .sort((a, b) => {
          const distA = getDistance(
            props.userLocation!.lat,
            props.userLocation!.lon,
            a.latitude,
            a.longitude
          )
          const distB = getDistance(
            props.userLocation!.lat,
            props.userLocation!.lon,
            b.latitude,
            b.longitude
          )
          return distA - distB
        })
        .slice(0, 5) // Top 5 cele mai apropiate
    }
    return []
  }
  
  const query = searchQuery.value.toLowerCase()
  return props.stations
    .filter(s => s.name.toLowerCase().includes(query))
    .slice(0, 10) // Max 10 rezultate
})

const selectStation = (station: Station) => {
  searchQuery.value = station.name
  showResults.value = false
  emit('stationSelected', station)
}

const calculateDistance = (station: Station): string => {
  if (!props.userLocation) return ''
  
  const dist = getDistance(
    props.userLocation.lat,
    props.userLocation.lon,
    station.latitude,
    station.longitude
  )
  
  return dist.toFixed(2)
}

// Formula Haversine pentru distanță între două coordonate GPS
function getDistance(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371 // Raza Pământului în km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  
  const a = 
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2)
  
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}

// Ascunde rezultatele când se dă click în afară
const handleClickOutside = (e: MouseEvent) => {
  const target = e.target as HTMLElement
  if (!target.closest('.search-container')) {
    showResults.value = false
  }
}

watch(showResults, (newVal) => {
  if (newVal) {
    document.addEventListener('click', handleClickOutside)
  } else {
    document.removeEventListener('click', handleClickOutside)
  }
})
</script>

<style scoped>
.search-container {
  position: absolute;
  top: 20px;
  left: 370px; /* După sidebar */
  z-index: 1000;
  width: 300px;
}

.search-input {
  width: 100%;
  padding: 12px 16px;
  border: 2px solid #2563eb;
  border-radius: 25px;
  font-size: 16px;
  outline: none;
  background: white;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
  transition: all 0.3s ease;
}

.search-input:focus {
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.3);
}

.search-results {
  position: absolute;
  top: 55px;
  left: 0;
  right: 0;
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  max-height: 400px;
  overflow-y: auto;
}

.search-result-item {
  padding: 12px 16px;
  cursor: pointer;
  border-bottom: 1px solid #e5e7eb;
  display: flex;
  justify-content: space-between;
  align-items: center;
  transition: background 0.2s ease;
}

.search-result-item:hover {
  background: #f3f4f6;
}

.search-result-item:last-child {
  border-bottom: none;
}

.station-name {
  font-weight: 500;
  color: #1f2937;
}

.station-distance {
  font-size: 12px;
  color: #6b7280;
  font-weight: 600;
}

.no-results {
  position: absolute;
  top: 55px;
  left: 0;
  right: 0;
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  padding: 16px;
  text-align: center;
  color: #6b7280;
}
</style>
