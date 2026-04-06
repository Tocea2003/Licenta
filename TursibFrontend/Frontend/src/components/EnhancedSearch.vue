<template>
  <div class="enhanced-search-container">
    <!-- Quick Access Favorites (when not searching) -->
    <div v-if="!searchQuery && !originQuery && favorites.length > 0" class="quick-access">
      <div class="section-title">⭐ Favorite</div>
      <div class="favorites-grid">
        <button
          v-for="favorite in favorites.slice(0, 3)"
          :key="favorite.id"
          @click="selectFavorite(favorite)"
          class="favorite-chip"
        >
          <span class="chip-icon">{{ favorite.icon }}</span>
          <span class="chip-name">{{ favorite.name }}</span>
        </button>
      </div>
    </div>

    <!-- Recent Searches (when not searching) -->
    <div v-if="!searchQuery && !originQuery && latestSearches.length > 0" class="recent-searches">
      <div class="section-title">
        🕒 Căutări Recente
        <button @click="clearAllSearches" class="clear-all-btn">Șterge tot</button>
      </div>
      <div
        v-for="search in latestSearches.slice(0, 5)"
        :key="search.id"
        @click="selectRecentSearch(search)"
        class="recent-item"
      >
        <span class="recent-icon">{{ getSearchIcon(search.type) }}</span>
        <div class="recent-info">
          <span class="recent-name">{{ search.result.name }}</span>
          <span class="recent-time">{{ getRelativeTime(search.timestamp) }}</span>
        </div>
        <button @click.stop="removeSearch(search.id)" class="remove-btn">×</button>
      </div>
    </div>
    
    <!-- Origin Search (only in trip mode) -->
    <div v-if="tripMode" class="search-box origin-box">
      <div class="search-label">📍 Plecare din:</div>
      <input
        v-model="originQuery"
        type="text"
        placeholder="🔍 Caută adresă sau stație de plecare..."
        class="search-input"
        @input="handleOriginSearch"
        @focus="showOriginResults = true"
      />
      <button v-if="originQuery" @click="clearOriginSearch" class="clear-btn">✕</button>
    </div>
    
    <div v-if="tripMode && showOriginResults && (geocodeOriginResults.length > 0 || filteredOriginStations.length > 0)" class="search-results">
      <!-- Rezultate geocoding (adrese) -->
      <div v-if="geocodeOriginResults.length > 0" class="results-section">
        <div class="section-title">📍 Adrese</div>
        <div
          v-for="(result, index) in geocodeOriginResults"
          :key="`origin-address-${index}`"
          class="search-result-item"
          @click="selectOriginAddress(result)"
        >
          <span class="result-name">{{ result.display_name }}</span>
        </div>
      </div>
      
      <!-- Stații -->
      <div v-if="filteredOriginStations.length > 0" class="results-section">
        <div class="section-title">🚏 Stații</div>
        <div
          v-for="station in filteredOriginStations"
          :key="`origin-station-${station.id}`"
          class="search-result-item"
          @click="selectOriginStation(station)"
        >
          <span class="result-name">{{ station.name }}</span>
          <span v-if="selectedLocation" class="result-distance">
            {{ calculateDistance(station.latitude, station.longitude) }} km
          </span>
        </div>
      </div>
    </div>

    <!-- Destination Search -->
    <div class="search-box">
      <div v-if="tripMode" class="search-label">🎯 Destinație:</div>
      <input
        v-model="searchQuery"
        type="text"
        :placeholder="tripMode ? '🔍 Destinație...' : '🔍 Caută stație...'"
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

    <!-- Search Routes Button (only in trip mode) -->
    <button 
      v-if="tripMode && originLocation && destinationLocation" 
      @click="searchRoutes" 
      class="search-routes-btn"
      :disabled="isSearchingRoutes"
    >
      {{ isSearchingRoutes ? '🔍 Căutare...' : '🚌 Caută Rute' }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { Station, Route } from '@/services/apiService'
import apiService from '@/services/apiService'
import { useFavorites, type FavoriteLocation } from '@/composables/useFavorites'
import { useRecentSearches } from '@/composables/useRecentSearches'
import { useStatistics } from '@/composables/useStatistics'

interface Props {
  stations: Station[]
  userLocation?: { lat: number; lon: number } | null
  tripMode?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  stations: () => [],
  userLocation: null,
  tripMode: false
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
  routeSearchRequested: [origin: { lat: number; lon: number; name: string }, destination: { lat: number; lon: number; name: string }]
}>()

// Favorites and Recent Searches
const { favorites } = useFavorites()
const { latestSearches, addRecentSearch, removeRecentSearch, clearRecentSearches: clearAllSearches, getRelativeTime } = useRecentSearches()
const { incrementSearches, recordStationUsage } = useStatistics()

const searchQuery = ref('')
const showResults = ref(false)
const geocodeResults = ref<GeocodeResult[]>([])
const isSearching = ref(false)
const selectedLocation = ref<{ lat: number; lon: number } | null>(null)

// Origin search state
const originQuery = ref('')
const showOriginResults = ref(false)
const geocodeOriginResults = ref<GeocodeResult[]>([])
const filteredOriginStations = ref<Station[]>([])
const originLocation = ref<{ lat: number; lon: number; name: string } | null>(null)
const destinationLocation = ref<{ lat: number; lon: number; name: string } | null>(null)
const isSearchingRoutes = ref(false)

// Debounce timers
let searchTimeout: number | null = null
let originSearchTimeout: number | null = null

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
    // Ignoră erorile de rețea temporare (network changed, fetch failed)
    if (error instanceof TypeError && (error.message.includes('fetch') || error.message.includes('network'))) {
      console.warn('⚠️ Eroare temporară de rețea la geocoding - se va reîncerca automat')
    } else {
      console.error('❌ Eroare la geocoding:', error)
    }
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

// Handle origin search
const handleOriginSearch = () => {
  showOriginResults.value = true
  
  if (originQuery.value.length < 2) {
    filteredOriginStations.value = []
    geocodeOriginResults.value = []
    return
  }
  
  // Filter stations
  const query = originQuery.value.toLowerCase()
  filteredOriginStations.value = props.stations
    .filter(s => s.name.toLowerCase().includes(query))
    .slice(0, 5)
  
  // Geocode addresses with debounce
  if (originSearchTimeout) {
    clearTimeout(originSearchTimeout)
  }
  originSearchTimeout = setTimeout(async () => {
    if (originQuery.value.length < 3) {
      geocodeOriginResults.value = []
      return
    }
    
    try {
      const searchQuery = `${originQuery.value}, Sibiu, Romania`
      const params = new URLSearchParams({
        format: 'json',
        q: searchQuery,
        limit: '5',
        addressdetails: '1',
        'accept-language': 'ro'
      })
      
      const response = await fetch(
        `https://nominatim.openstreetmap.org/search?${params.toString()}`
      )
      const data = await response.json()
      
      geocodeOriginResults.value = data.filter((result: any) => {
        if (result.boundingbox) {
          const bbox = result.boundingbox.map((coord: string) => parseFloat(coord))
          const latDiff = Math.abs(bbox[1] - bbox[0])
          const lonDiff = Math.abs(bbox[3] - bbox[2])
          if (latDiff > 0.01 || lonDiff > 0.01) return false
        }
        return true
      })
    } catch (error) {
      console.error('Geocoding error:', error)
      geocodeOriginResults.value = []
    }
  }, 500)
}

const selectOriginAddress = (result: GeocodeResult) => {
  originLocation.value = {
    lat: parseFloat(result.lat),
    lon: parseFloat(result.lon),
    name: result.display_name
  }
  originQuery.value = result.display_name
  showOriginResults.value = false
  geocodeOriginResults.value = []
  filteredOriginStations.value = []
}

const selectOriginStation = (station: Station) => {
  originLocation.value = {
    lat: station.latitude,
    lon: station.longitude,
    name: station.name
  }
  originQuery.value = station.name
  showOriginResults.value = false
  geocodeOriginResults.value = []
  filteredOriginStations.value = []
}

const clearOriginSearch = () => {
  originQuery.value = ''
  originLocation.value = null
  geocodeOriginResults.value = []
  filteredOriginStations.value = []
  showOriginResults.value = false
}

// Search routes between origin and destination
const searchRoutes = () => {
  if (!originLocation.value || !destinationLocation.value) return
  
  isSearchingRoutes.value = true
  
  // Emit event to parent (MapView) to handle route searching
  emit('routeSearchRequested', originLocation.value, destinationLocation.value)
  
  setTimeout(() => {
    isSearchingRoutes.value = false
  }, 1000)
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
  
  // Track statistics
  incrementSearches()
  
  // Salvează în recent searches
  addRecentSearch({
    query: searchQuery.value,
    type: 'address',
    result: {
      name: destinationName,
      lat: destinationLat,
      lon: destinationLon
    }
  })
  
  // In trip mode, just set destination
  if (props.tripMode) {
    destinationLocation.value = {
      lat: destinationLat,
      lon: destinationLon,
      name: result.display_name
    }
    searchQuery.value = result.display_name
    showResults.value = false
    geocodeResults.value = []
    return
  }
  
  // Original behavior for non-trip mode
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
  // Track statistics
  incrementSearches()
  recordStationUsage(station.id)
  
  // Salvează în recent searches
  addRecentSearch({
    query: searchQuery.value || station.name,
    type: 'station',
    result: {
      name: station.name,
      lat: station.latitude,
      lon: station.longitude,
      stationId: station.id
    }
  })
  
  // In trip mode, set as destination and calculate route if we have origin
  if (props.tripMode) {
    destinationLocation.value = {
      lat: station.latitude,
      lon: station.longitude,
      name: station.name
    }
    searchQuery.value = station.name
    showResults.value = false
    
    // Dacă avem și origine, calculează traseu multimodal
    if (originLocation.value) {
      console.log('🚀 Calculez traseu către stația:', station.name)
      emit('routeSearchRequested', originLocation.value, destinationLocation.value)
    }
    return
  }
  
  // Original behavior for non-trip mode
  emit('stationSelected', station)
  showResults.value = false
  searchQuery.value = station.name
}

// Select favorite location
const selectFavorite = (favorite: FavoriteLocation) => {
  const location = {
    lat: favorite.lat,
    lon: favorite.lon,
    name: favorite.name
  }
  
  // Salvează în recent searches
  addRecentSearch({
    query: favorite.name,
    type: 'address',
    result: {
      name: favorite.name,
      lat: favorite.lat,
      lon: favorite.lon
    }
  })
  
  if (props.tripMode && originLocation.value) {
    destinationLocation.value = location
    searchQuery.value = favorite.name
  } else {
    emit('addressSelected', location)
    
    if (props.userLocation) {
      const nearestStation = findNearestStation(location.lat, location.lon)
      if (nearestStation) {
        emit(
          'walkingDirectionsRequested',
          { lat: props.userLocation.lat, lon: props.userLocation.lon, name: 'My location' },
          nearestStation
        )
      }
    }
  }
}

// Select recent search
const selectRecentSearch = (search: any) => {
  if (search.type === 'station' && search.result.stationId) {
    const station = props.stations.find(s => s.id === search.result.stationId)
    if (station) {
      selectStation(station)
    }
  } else if (search.result.lat && search.result.lon) {
    const fakeResult: GeocodeResult = {
      lat: search.result.lat.toString(),
      lon: search.result.lon.toString(),
      display_name: search.result.name
    }
    selectAddress(fakeResult)
  }
}

// Remove search from recent
const removeSearch = (id: string) => {
  removeRecentSearch(id)
}

// Get icon for search type
const getSearchIcon = (type: string): string => {
  return type === 'station' ? '🚏' : '📍'
}

const clearSearch = () => {
  searchQuery.value = ''
  destinationLocation.value = null
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
  z-index: 700;
  width: 210px;
  max-width: calc(100vw - 100px);
}

@media (max-width: 767px) {
  .enhanced-search-container {
    left: 54px;
    transform: none;
    top: 10px;
    width: auto;
    max-width: calc(100vw - 54px - 130px);
  }
}

/* Quick Access Favorites */
.quick-access {
  margin-bottom: 12px;
  background: var(--bg-primary);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 12px;
  box-shadow: var(--shadow-md);
}

.favorites-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-top: 8px;
}

.favorite-chip {
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 10px;
  padding: 12px 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.favorite-chip:hover {
  background: #eff6ff;
  border-color: #3b82f6;
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(59, 130, 246, 0.15);
}

.chip-icon {
  font-size: 20px;
}

.chip-name {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-primary);
  text-align: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

/* Recent Searches */
.recent-searches {
  margin-bottom: 10px;
  background: var(--bg-primary);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  box-shadow: var(--shadow-md);
  overflow: hidden;
}

.recent-searches .section-title {
  padding: 10px 12px 6px;
  font-size: 10px;
  font-weight: 700;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.8px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.clear-all-btn {
  background: none;
  border: none;
  color: #ef4444;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 6px;
  transition: all 0.2s;
  text-transform: none;
}

.clear-all-btn:hover {
  background: #fee2e2;
}

.recent-item {
  padding: 8px 12px;
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  transition: all 0.2s;
  border-top: 1px solid var(--border-color);
}

.recent-item:first-child {
  border-top: none;
}

.recent-item:hover {
  background: var(--bg-secondary);
}

.recent-icon {
  font-size: 18px;
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-tertiary);
  border-radius: 6px;
  flex-shrink: 0;
}

.recent-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.recent-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.recent-time {
  font-size: 10px;
  color: var(--text-tertiary);
}

.remove-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: none;
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
  font-size: 20px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  flex-shrink: 0;
}

.remove-btn:hover {
  background: #fee2e2;
}

.search-box {
  position: relative;
  margin-bottom: 8px;
}

.origin-box {
  margin-bottom: 8px;
}

.search-label {
  font-weight: 600;
  color: var(--text-primary);
  font-size: 13px;
  margin-bottom: 6px;
  padding-left: 2px;
  background: var(--bg-primary);
  backdrop-filter: blur(8px);
  display: inline-block;
  padding: 2px 8px;
  border-radius: 6px;
}

.search-input {
  width: 100%;
  padding: 14px 40px 14px 16px;
  border: 1px solid var(--border-color);
  border-radius: 12px;
  font-size: 15px;
  background: var(--bg-primary);
  backdrop-filter: blur(12px);
  box-shadow: var(--shadow-sm);
  transition: all 0.2s;
  color: var(--text-primary);
}

.search-input::placeholder {
  color: var(--text-tertiary);
}

.search-input:focus {
  outline: none;
  border-color: #3b82f6;
  background: var(--bg-primary);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.15);
}

.clear-btn {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  background: var(--bg-tertiary);
  border: none;
  border-radius: 50%;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--text-secondary);
  font-size: 14px;
  transition: all 0.2s;
}

.clear-btn:hover {
  background: rgba(107, 114, 128, 0.2);
  color: #374151;
}

.search-results {
  margin-top: 8px;
  background: var(--bg-primary);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  box-shadow: var(--shadow-lg);
  max-height: 350px;
  overflow-y: auto;
}

.results-section {
  padding: 4px 0;
}

.results-section + .results-section {
  border-top: 1px solid var(--border-color);
}

.section-title {
  padding: 10px 14px 6px;
  font-size: 11px;
  font-weight: 700;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.8px;
}

.search-result-item {
  padding: 12px 14px;
  cursor: pointer;
  display: flex;
  justify-content: space-between;
  align-items: center;
  transition: all 0.2s;
  background: transparent;
}

.search-result-item:hover {
  background: var(--bg-secondary);
}

.result-name {
  color: var(--text-primary);
  font-size: 14px;
  flex: 1;
  font-weight: 500;
}

.result-distance {
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  margin-left: 12px;
}

.no-results, .searching {
  margin-top: 8px;
  padding: 16px;
  background: var(--bg-primary);
  backdrop-filter: blur(12px);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  box-shadow: var(--shadow-md);
  color: var(--text-secondary);
  text-align: center;
  font-size: 14px;
}

.searching {
  color: #3b82f6;
}

/* Search Routes Button */
.search-routes-btn {
  width: 100%;
  padding: 14px 20px;
  margin-top: 12px;
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  color: white;
  border: none;
  border-radius: 12px;
  font-weight: 700;
  font-size: 15px;
  cursor: pointer;
  transition: all 0.3s;
  box-shadow: 0 2px 8px rgba(16, 185, 129, 0.3);
}

.search-routes-btn:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.4);
}

.search-routes-btn:active {
  transform: translateY(0);
}

.search-routes-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

/* Scrollbar styling */
.search-results::-webkit-scrollbar {
  width: 5px;
}

.search-results::-webkit-scrollbar-track {
  background: transparent;
}

.search-results::-webkit-scrollbar-thumb {
  background: rgba(209, 213, 219, 0.8);
  border-radius: 10px;
}

.search-results::-webkit-scrollbar-thumb:hover {
  background: rgba(156, 163, 175, 0.9);
}

</style>
