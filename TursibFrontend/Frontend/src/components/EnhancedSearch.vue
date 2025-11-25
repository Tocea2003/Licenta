<template>
  <div class="enhanced-search-container">
    <div class="search-box">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="🔍 Caută stație sau adresă..."
        class="search-input"
        @input="handleSearch"
        @focus="showResults = true"
      />
      <button v-if="searchQuery" @click="clearSearch" class="clear-btn">✕</button>
    </div>
    
    <div v-if="showResults && (geocodeResults.length > 0 || filteredStations.length > 0)" class="search-results">
      <!-- Rezultate geocoding (adrese) -->
      <div v-if="geocodeResults.length > 0" class="results-section">
        <div class="section-title">📍 Adrese</div>
        <div
          v-for="(result, index) in geocodeResults"
          :key="`address-${index}`"
          class="search-result-item"
          @click="selectAddress(result)"
        >
          <span class="result-name">{{ result.display_name }}</span>
        </div>
      </div>
      
      <!-- Stații -->
      <div v-if="filteredStations.length > 0" class="results-section">
        <div class="section-title">🚏 Stații</div>
        <div
          v-for="station in filteredStations"
          :key="`station-${station.id}`"
          class="search-result-item"
          @click="selectStation(station)"
        >
          <span class="result-name">{{ station.name }}</span>
          <span v-if="selectedLocation" class="result-distance">
            {{ calculateDistance(station.latitude, station.longitude) }} km
          </span>
        </div>
      </div>
    </div>
    
    <div v-if="showResults && searchQuery && geocodeResults.length === 0 && filteredStations.length === 0 && !isSearching" class="no-results">
      Nu s-au găsit rezultate
    </div>
    
    <div v-if="isSearching" class="searching">
      Căutare...
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { Station } from '@/services/apiService'

interface Props {
  stations: Station[]
  userLocation?: { lat: number; lon: number } | null
}

const props = withDefaults(defineProps<Props>(), {
  stations: () => [],
  userLocation: null
})

interface GeocodeResult {
  lat: string
  lon: string
  display_name: string
}

const emit = defineEmits<{
  stationSelected: [station: Station]
  addressSelected: [location: { lat: number; lon: number; name: string }]
  walkingDirectionsRequested: [start: { lat: number; lon: number; name: string }, end: Station]
  multimodalRouteRequested: [userLocation: { lat: number; lon: number }, destination: { lat: number; lon: number; name: string }]
}>()

const searchQuery = ref('')
const showResults = ref(false)
const geocodeResults = ref<GeocodeResult[]>([])
const isSearching = ref(false)
const selectedLocation = ref<{ lat: number; lon: number } | null>(null)

// Debounce timer
let searchTimeout: number | null = null

// Filtrează stațiile după query
const filteredStations = computed(() => {
  if (!searchQuery.value.trim()) {
    return []
  }
  
  const query = searchQuery.value.toLowerCase()
  const filtered = props.stations
    .filter(s => s.name.toLowerCase().includes(query))
    .slice(0, 5)
  
  // Sortează după distanță dacă avem locația
  if (selectedLocation.value) {
    return filtered.sort((a, b) => {
      const distA = getDistance(
        selectedLocation.value!.lat,
        selectedLocation.value!.lon,
        a.latitude,
        a.longitude
      )
      const distB = getDistance(
        selectedLocation.value!.lat,
        selectedLocation.value!.lon,
        b.latitude,
        b.longitude
      )
      return distA - distB
    })
  }
  
  return filtered
})

// Geocoding API (Nominatim - OpenStreetMap)
const searchAddress = async (query: string) => {
  if (query.length < 3) {
    geocodeResults.value = []
    return
  }
  
  isSearching.value = true
  
  try {
    // Adaugă "Sibiu, Romania" pentru context local
    const searchQuery = `${query}, Sibiu, Romania`
    
    // Parametri îmbunătățiți pentru Nominatim:
    // - addressdetails=1: include detalii complete adresă
    // - extratags=1: include tag-uri suplimentare OSM
    // - namedetails=1: include variante ale numelui
    const params = new URLSearchParams({
      format: 'json',
      q: searchQuery,
      limit: '5',
      addressdetails: '1',
      extratags: '1',
      namedetails: '1',
      'accept-language': 'ro' // Preferință pentru limba română
    })
    
    const response = await fetch(
      `https://nominatim.openstreetmap.org/search?${params.toString()}`
    )
    const data = await response.json()
    
    // Filtrează rezultatele pentru a exclude cele prea imprecise
    geocodeResults.value = data.filter((result: any) => {
      // Exclude rezultate cu bbox (bounding box) prea mare
      // care indică o zonă largă, nu o adresă precisă
      if (result.boundingbox) {
        const bbox = result.boundingbox.map((coord: string) => parseFloat(coord))
        const latDiff = Math.abs(bbox[1] - bbox[0])
        const lonDiff = Math.abs(bbox[3] - bbox[2])
        
        // Dacă bbox-ul este mai mare de 0.01 grade (~1km), exclude
        if (latDiff > 0.01 || lonDiff > 0.01) {
          console.log('⚠️ Exclude rezultat imprecis:', result.display_name)
          return false
        }
      }
      return true
    })
    
    console.log(`🔍 Găsite ${geocodeResults.value.length} rezultate precise pentru "${query}"`)
  } catch (error) {
    console.error('Eroare la geocoding:', error)
    geocodeResults.value = []
  } finally {
    isSearching.value = false
  }
}

// Handle search cu debounce
const handleSearch = () => {
  if (searchTimeout) {
    clearTimeout(searchTimeout)
  }
  
  searchTimeout = setTimeout(() => {
    if (searchQuery.value.trim()) {
      searchAddress(searchQuery.value)
    } else {
      geocodeResults.value = []
    }
  }, 500) // 500ms debounce
}

// Calculează distanța folosind formula Haversine
const getDistance = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
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

const calculateDistance = (lat: number, lon: number): string => {
  if (!selectedLocation.value) return ''
  const dist = getDistance(selectedLocation.value.lat, selectedLocation.value.lon, lat, lon)
  return dist.toFixed(2)
}

// Găsește cea mai apropiată stație de o locație
const findNearestStation = (lat: number, lon: number): Station | null => {
  if (props.stations.length === 0) return null
  
  let nearest: Station | undefined = props.stations[0]
  if (!nearest) return null
  
  let minDistance = getDistance(lat, lon, nearest.latitude, nearest.longitude)
  
  for (const station of props.stations) {
    const distance = getDistance(lat, lon, station.latitude, station.longitude)
    if (distance < minDistance) {
      minDistance = distance
      nearest = station
    }
  }
  
  return nearest || null
}

const selectAddress = (result: GeocodeResult) => {
  const destinationLat = parseFloat(result.lat)
  const destinationLon = parseFloat(result.lon)
  const destinationName = result.display_name.split(',')[0] || 'Destinație'
  
  // Dacă avem locația utilizatorului, calculează traseu multimodal
  if (props.userLocation) {
    console.log(`🚀 Calculez traseu multimodal de la locația ta la ${destinationName}`)
    
    selectedLocation.value = { lat: destinationLat, lon: destinationLon }
    
    // Emite eveniment pentru traseu multimodal (mers pe jos + autobuz + mers pe jos)
    emit('multimodalRouteRequested', 
      props.userLocation,
      { lat: destinationLat, lon: destinationLon, name: destinationName }
    )
    
    emit('addressSelected', { lat: destinationLat, lon: destinationLon, name: destinationName })
  } else {
    // Fallback: fără locație GPS, doar walking directions la cea mai apropiată stație
    console.log(`⚠️ Nicio locație GPS - afișez doar stația cea mai apropiată de ${destinationName}`)
    
    selectedLocation.value = { lat: destinationLat, lon: destinationLon }
    
    const nearestStation = findNearestStation(destinationLat, destinationLon)
    
    if (nearestStation) {
      const distance = getDistance(destinationLat, destinationLon, nearestStation.latitude, nearestStation.longitude)
      console.log(`🎯 Cea mai apropiată stație de "${result.display_name}": ${nearestStation.name} (${distance.toFixed(2)} km)`)
      
      const startLocation = {
        lat: destinationLat,
        lon: destinationLon,
        name: destinationName
      }
      
      emit('addressSelected', startLocation)
      emit('walkingDirectionsRequested', startLocation, nearestStation)
    }
  }
  
  showResults.value = false
  searchQuery.value = destinationName
}

const selectStation = (station: Station) => {
  emit('stationSelected', station)
  showResults.value = false
  searchQuery.value = station.name
}

const clearSearch = () => {
  searchQuery.value = ''
  geocodeResults.value = []
  selectedLocation.value = null
  showResults.value = false
}

// Închide rezultatele când se dă click în afară
const handleClickOutside = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  if (!target.closest('.enhanced-search-container')) {
    showResults.value = false
  }
}

// Adaugă event listener pentru click outside
if (typeof window !== 'undefined') {
  window.addEventListener('click', handleClickOutside)
}
</script>

<style scoped>
.enhanced-search-container {
  position: absolute;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  width: 500px;
  max-width: calc(100vw - 40px);
}

/* Mobile: poziționare ajustată pentru butonul hamburger */
@media (max-width: 768px) {
  .enhanced-search-container {
    top: 80px;
    left: 20px;
    right: 20px;
    width: auto;
    max-width: none;
    transform: none;
  }
}

/* Mobile responsive */
@media (max-width: 768px) {
  .enhanced-search-container {
    top: 10px;
    left: 10px;
    right: 10px;
    width: auto;
    max-width: none;
  }
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-input {
  width: 100%;
  padding: 12px 40px 12px 16px;
  border: 2px solid #e5e7eb;
  border-radius: 12px;
  font-size: 15px;
  background: white;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
  transition: all 0.2s;
}

/* Mobile touch optimization */
@media (max-width: 768px) {
  .search-input {
    padding: 14px 40px 14px 16px;
    font-size: 16px; /* Prevents zoom on iOS */
    border-radius: 10px;
  }
}

.search-input:focus {
  outline: none;
  border-color: #3b82f6;
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.2);
}

.clear-btn {
  position: absolute;
  right: 12px;
  background: none;
  border: none;
  font-size: 18px;
  color: #9ca3af;
  cursor: pointer;
  padding: 4px 8px;
.search-results {
  margin-top: 8px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  max-height: 400px;
  overflow-y: auto;
}
}

/* Mobile responsive results */
@media (max-width: 768px) {
  .search-results {
    max-height: 60vh;
    border-radius: 10px;
  }
}

.search-results {
  margin-top: 8px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  max-height: 400px;
  overflow-y: auto;
}

.results-section {
  padding: 8px 0;
}

.results-section + .results-section {
  border-top: 1px solid #e5e7eb;
}

.section-title {
  padding: 12px 16px 8px;
  font-size: 12px;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  background: white;
}

.search-result-item {
  padding: 12px 16px;
  cursor: pointer;
  display: flex;
  justify-content: space-between;
  align-items: center;
  transition: background 0.2s;
  background: white;
}

.search-result-item:hover {
  background: #f3f4f6;
}

/* Mobile touch targets */
@media (max-width: 768px) {
  .search-result-item {
    padding: 16px;
    min-height: 48px; /* Touch target size */
  }
  
  .search-result-item:active {
    background: #e5e7eb;
  }
}

.search-result-item {
  padding: 12px 16px;
  cursor: pointer;
  display: flex;
  justify-content: space-between;
  align-items: center;
  transition: background 0.2s;
  background: white;
}

.search-result-item:hover {
  background: #f3f4f6;
}

.result-name {
  color: #1f2937;
  font-size: 14px;
  flex: 1;
  font-weight: 500;
}

.result-distance {
  color: #6b7280;
  font-size: 12px;
  font-weight: 600;
  margin-left: 12px;
}

.no-results, .searching {
  margin-top: 8px;
  padding: 16px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  color: #6b7280;
  text-align: center;
  font-size: 14px;
}

.searching {
  color: #3b82f6;
}

/* Scrollbar styling */
.search-results::-webkit-scrollbar {
  width: 6px;
}

.search-results::-webkit-scrollbar-track {
  background: #f3f4f6;
  border-radius: 12px;
}

.search-results::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 12px;
}

.search-results::-webkit-scrollbar-thumb:hover {
  background: #9ca3af;
}

</style>
