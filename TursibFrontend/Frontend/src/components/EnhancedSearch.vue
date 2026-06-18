<template>
  <div class="enhanced-search-container">
    
    <!-- Origin Search (only in trip mode) -->
    <div v-if="tripMode" class="search-box origin-box">
      <div class="search-label">📍 {{ t('departureFromColon') }}</div>
      <input
        v-model="originQuery"
        type="text"
        :placeholder="`🔍 ${t('searchDeparturePlaceholder')}`"
        class="search-input"
        @input="handleOriginSearch"
        @focus="!directionsActive && (showOriginResults = true)"
      />
      <button v-if="originQuery" @click="clearOriginSearch" class="clear-btn">✕</button>
    </div>
    
    <div v-if="tripMode && showOriginResults && !directionsActive && (geocodeOriginResults.length > 0 || filteredOriginStations.length > 0)" class="search-results">
      <!-- Rezultate geocoding (adrese) -->
      <div v-if="geocodeOriginResults.length > 0" class="results-section">
        <div class="section-title">📍 {{ t('addresses') }}</div>
        <div
          v-for="(result, index) in geocodeOriginResults"
          :key="`origin-address-${index}`"
          class="search-result-item address-result"
          @click="selectOriginAddress(result)"
        >
          <span class="result-type-icon">{{ getTypeIcon(result.type) }}</span>
          <div class="result-address-info">
            <span class="result-name">{{ result.displayName }}</span>
            <span v-if="result.addressDetails.suburb || result.addressDetails.city" class="result-address-sub">
              {{ [result.addressDetails.suburb, result.addressDetails.city].filter(Boolean).join(', ') }}
            </span>
          </div>
        </div>
      </div>
      
      <!-- Stații -->
      <div v-if="filteredOriginStations.length > 0" class="results-section">
        <div class="section-title">🚏 {{ t('stationsSection') }}</div>
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
      <div v-if="tripMode" class="search-label">🎯 {{ t('destinationColon') }}</div>
      <input
        v-model="searchQuery"
        type="text"
        :placeholder="tripMode ? `🔍 ${t('destinationPlaceholderShort')}` : `🔍 ${t('searchStationEllipsis')}`"
        class="search-input"
        @input="handleSearch"
        @focus="!directionsActive && (showResults = true)"
      />
      <button v-if="searchQuery" @click="clearSearch" class="clear-btn">✕</button>
    </div>

    <!-- Quick Access Favorites (dropdown when focused, no query) -->
    <div v-if="showResults && !directionsActive && !searchQuery && !originQuery && favorites.length > 0" class="quick-access">
      <div class="section-title">⭐ {{ t('favorites') }}</div>
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

    <!-- Recent Searches (dropdown when focused, no query) -->
    <div v-if="showResults && !directionsActive && !searchQuery && !originQuery && latestSearches.length > 0" class="recent-searches">
      <div class="section-title">
        🕒 {{ t('recentSearches') }}
        <button @click="clearAllSearches" class="clear-all-btn">{{ t('clearAll') }}</button>
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

    <div v-if="showResults && !directionsActive && (geocodeResults.length > 0 || filteredStations.length > 0)" class="search-results">
      <!-- Rezultate geocoding (adrese) -->
      <div v-if="geocodeResults.length > 0" class="results-section">
        <div class="section-title">📍 {{ t('addresses') }}</div>
        <div
          v-for="(result, index) in geocodeResults"
          :key="`address-${index}`"
          class="search-result-item address-result"
          @click="selectAddress(result)"
        >
          <span class="result-type-icon">{{ getTypeIcon(result.type) }}</span>
          <div class="result-address-info">
            <span class="result-name">{{ result.displayName }}</span>
            <span v-if="result.addressDetails.suburb || result.addressDetails.city" class="result-address-sub">
              {{ [result.addressDetails.suburb, result.addressDetails.city].filter(Boolean).join(', ') }}
            </span>
          </div>
        </div>
      </div>
      
      <!-- Stații -->
      <div v-if="filteredStations.length > 0" class="results-section">
        <div class="section-title">🚏 {{ t('stationsSection') }}</div>
        <div
          v-for="station in filteredStations"
          :key="`station-${station.id}`"
          class="search-result-item"
          @click="selectStation(station)"
        >
          <div class="result-main">
            <span class="result-name">{{ station.name }}</span>
            <span v-if="selectedLocation" class="result-distance">
              {{ calculateDistance(station.latitude, station.longitude) }} km
            </span>
          </div>
          <button
            class="result-notification-btn"
            :title="t('enableAlertsForStation')"
            @click.stop="requestStationNotification(station)"
          >
            🔔
          </button>
        </div>
      </div>
    </div>
    
    <div v-if="showResults && !directionsActive && searchQuery && geocodeResults.length === 0 && filteredStations.length === 0 && !isSearching" class="no-results">
      {{ t('noResultsFound') }}
    </div>
    
    <div v-if="isSearching" class="searching">
      {{ t('searching') }}
    </div>

    <!-- Search Routes Button (only in trip mode) -->
    <button 
      v-if="tripMode && originLocation && destinationLocation" 
      @click="searchRoutes" 
      class="search-routes-btn"
      :disabled="isSearchingRoutes"
    >
      {{ isSearchingRoutes ? `🔍 ${t('searching')}` : `🚌 ${t('searchRoutesBtn')}` }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Station, Route } from '@/services/apiService'
import apiService from '@/services/apiService'
import { useFavorites, type FavoriteLocation } from '@/composables/useFavorites'
import { useRecentSearches } from '@/composables/useRecentSearches'
import { useStatistics } from '@/composables/useStatistics'
import { searchAddresses, getTypeIcon, type GeocodingResult } from '@/services/geocodingService'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

interface Props {
  stations: Station[]
  userLocation?: { lat: number; lon: number } | null
  tripMode?: boolean
  directionsActive?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  stations: () => [],
  userLocation: null,
  tripMode: false,
  directionsActive: false
})

const emit = defineEmits<{
  stationSelected: [station: Station]
  stationNotificationRequested: [station: Station]
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
const geocodeResults = ref<GeocodingResult[]>([])
const isSearching = ref(false)
const selectedLocation = ref<{ lat: number; lon: number } | null>(null)

// Origin search state
const originQuery = ref('')
const showOriginResults = ref(false)
const geocodeOriginResults = ref<GeocodingResult[]>([])
const filteredOriginStations = ref<Station[]>([])
const originLocation = ref<{ lat: number; lon: number; name: string } | null>(null)
const destinationLocation = ref<{ lat: number; lon: number; name: string } | null>(null)
const isSearchingRoutes = ref(false)

watch(() => props.directionsActive, (active) => {
  if (active) {
    showResults.value = false
    showOriginResults.value = false
  }
})


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
  if (query.length < 2) {
    geocodeResults.value = []
    return
  }
  isSearching.value = true
  try {
    geocodeResults.value = await searchAddresses(query)
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
    if (originQuery.value.length < 2) {
      geocodeOriginResults.value = []
      return
    }
    geocodeOriginResults.value = await searchAddresses(originQuery.value)
  }, 400)
}

const selectOriginAddress = (result: GeocodingResult) => {
  originLocation.value = {
    lat: result.lat,
    lon: result.lon,
    name: result.displayName
  }
  originQuery.value = result.displayName
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

const selectAddress = (result: GeocodingResult) => {
  const destinationLat  = result.lat
  const destinationLon  = result.lon
  const destinationName = result.displayName

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
    destinationLocation.value = { lat: destinationLat, lon: destinationLon, name: destinationName }
    searchQuery.value = destinationName
    showResults.value = false
    geocodeResults.value = []
    return
  }

  // Non-trip mode: multimodal route or walking fallback
  if (props.userLocation) {
    selectedLocation.value = { lat: destinationLat, lon: destinationLon }
    emit('multimodalRouteRequested',
      props.userLocation,
      { lat: destinationLat, lon: destinationLon, name: destinationName }
    )
    emit('addressSelected', { lat: destinationLat, lon: destinationLon, name: destinationName })
  } else {
    selectedLocation.value = { lat: destinationLat, lon: destinationLon }
    const nearestStation = findNearestStation(destinationLat, destinationLon)
    if (nearestStation) {
      const startLocation = { lat: destinationLat, lon: destinationLon, name: destinationName }
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

const requestStationNotification = (station: Station) => {
  emit('stationNotificationRequested', station)
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
    const fakeResult: GeocodingResult = {
      displayName:    search.result.name,
      fullAddress:    search.result.name,
      lat:            search.result.lat,
      lon:            search.result.lon,
      type:           'other',
      addressDetails: {}
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

@media (min-width: 768px) and (max-width: 900px) {
  .enhanced-search-container {
    max-width: calc(100vw - 230px);
  }
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
  display: flex;
  flex-direction: row;
  gap: 8px;
  margin-top: 8px;
  overflow-x: auto;
  padding-bottom: 2px;
  scrollbar-width: none;
}
.favorites-grid::-webkit-scrollbar { display: none; }

.favorite-chip {
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 10px;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  transition: all 0.2s;
  flex-shrink: 0;
  min-width: 64px;
  max-width: 90px;
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

.result-main {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.address-result {
  align-items: flex-start;
  gap: 10px;
}

.result-type-icon {
  font-size: 16px;
  flex-shrink: 0;
  margin-top: 1px;
}

.result-address-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
  min-width: 0;
}

.result-address-sub {
  font-size: 11px;
  color: var(--text-tertiary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.result-name {
  color: var(--text-primary);
  font-size: 14px;
  flex: 1;
  font-weight: 500;
}

.result-notification-btn {
  border: 1px solid #93c5fd;
  background: #eff6ff;
  color: #1d4ed8;
  width: 30px;
  height: 30px;
  border-radius: 50%;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.result-notification-btn:hover {
  background: #dbeafe;
  transform: translateY(-1px);
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
