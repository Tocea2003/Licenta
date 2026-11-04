<template>
  <div class="enhanced-search-container">

    <!-- Origin Search (only in trip mode) -->
    <label v-if="tripMode" class="search-box origin-box">
      <MapPin :size="18" class="search-icon" aria-hidden="true" />
      <span class="search-text">
        <span class="search-label">{{ t('departureFromColon') }}</span>
        <input
          v-model="originQuery"
          type="text"
          :placeholder="t('searchDeparturePlaceholder')"
          class="search-input"
          @input="handleOriginSearch"
          @focus="!directionsActive && (showOriginResults = true)"
        />
      </span>
      <button v-if="originQuery" type="button" @click="clearOriginSearch" class="clear-btn" :aria-label="t('close')"><X :size="16" aria-hidden="true" /></button>
    </label>

    <div v-if="tripMode && showOriginResults && !directionsActive && (geocodeOriginResults.length > 0 || filteredOriginStations.length > 0)" class="search-panel">
      <div v-if="geocodeOriginResults.length > 0" class="results-section">
        <div class="section-title">{{ t('addresses') }}</div>
        <button
          v-for="(result, index) in geocodeOriginResults"
          :key="`origin-address-${index}`"
          type="button"
          class="result-row"
          @click="selectOriginAddress(result)"
        >
          <MapPin :size="16" class="row-icon" aria-hidden="true" />
          <span class="row-text">
            <span class="row-name">{{ result.displayName }}</span>
            <span v-if="result.addressDetails.suburb || result.addressDetails.city" class="row-sub">
              {{ [result.addressDetails.suburb, result.addressDetails.city].filter(Boolean).join(', ') }}
            </span>
          </span>
        </button>
      </div>

      <div v-if="filteredOriginStations.length > 0" class="results-section">
        <div class="section-title">{{ t('stationsSection') }}</div>
        <button
          v-for="station in filteredOriginStations"
          :key="`origin-station-${station.id}`"
          type="button"
          class="result-row"
          @click="selectOriginStation(station)"
        >
          <Bus :size="16" class="row-icon" aria-hidden="true" />
          <span class="row-text"><span class="row-name">{{ station.name }}</span></span>
          <span v-if="selectedLocation" class="row-meta">{{ calculateDistance(station.latitude, station.longitude) }} km</span>
        </button>
      </div>
    </div>

    <!-- Destination Search -->
    <label class="search-box">
      <Search :size="18" class="search-icon" aria-hidden="true" />
      <span class="search-text">
        <span v-if="tripMode" class="search-label">{{ t('destinationColon') }}</span>
        <span v-else class="sr-only">{{ t('searchStationEllipsis') }}</span>
        <input
          v-model="searchQuery"
          type="search"
          :placeholder="tripMode ? t('destinationPlaceholderShort') : t('searchStationEllipsis')"
          class="search-input"
          @input="handleSearch"
          @focus="!directionsActive && (showResults = true)"
        />
      </span>
      <button v-if="searchQuery" type="button" @click="clearSearch" class="clear-btn" :aria-label="t('close')"><X :size="16" aria-hidden="true" /></button>
    </label>

    <!-- Favorites + recent searches (focused, no query) -->
    <div
      v-if="showResults && !directionsActive && !searchQuery && !originQuery && (favorites.length > 0 || latestSearches.length > 0)"
      class="search-panel"
    >
      <div v-if="favorites.length > 0" class="results-section">
        <div class="section-title">{{ t('favorites') }}</div>
        <div class="favorites-row">
          <button
            v-for="favorite in favorites.slice(0, 3)"
            :key="favorite.id"
            type="button"
            @click="selectFavorite(favorite)"
            class="favorite-chip"
          >
            <component :is="favorite.type === 'home' ? Home : favorite.type === 'work' ? Briefcase : Star" :size="16" aria-hidden="true" />
            <span class="chip-name">{{ favorite.name }}</span>
          </button>
        </div>
      </div>

      <div v-if="latestSearches.length > 0" class="results-section">
        <div class="section-title">
          {{ t('recentSearches') }}
          <button type="button" @click="clearAllSearches" class="link-btn">{{ t('clearAll') }}</button>
        </div>
        <div v-for="search in latestSearches.slice(0, 5)" :key="search.id" class="recent-row">
          <button type="button" class="result-row" @click="selectRecentSearch(search)">
            <component :is="search.type === 'station' ? Bus : History" :size="16" class="row-icon" aria-hidden="true" />
            <span class="row-text">
              <span class="row-name">{{ search.result.name }}</span>
              <span class="row-sub">{{ getRelativeTime(search.timestamp) }}</span>
            </span>
          </button>
          <button type="button" @click.stop="removeSearch(search.id)" class="clear-btn" :aria-label="t('close')"><X :size="14" aria-hidden="true" /></button>
        </div>
      </div>
    </div>

    <div v-if="showResults && !directionsActive && (geocodeResults.length > 0 || filteredStations.length > 0)" class="search-panel">
      <div v-if="filteredStations.length > 0" class="results-section">
        <div class="section-title">{{ t('stationsSection') }}</div>
        <div v-for="station in filteredStations" :key="`station-${station.id}`" class="recent-row">
          <button type="button" class="result-row" @click="selectStation(station)">
            <Bus :size="16" class="row-icon" aria-hidden="true" />
            <span class="row-text"><span class="row-name">{{ station.name }}</span></span>
            <span v-if="selectedLocation" class="row-meta">{{ calculateDistance(station.latitude, station.longitude) }} km</span>
          </button>
          <button
            type="button"
            class="clear-btn"
            :title="t('enableAlertsForStation')"
            :aria-label="t('enableAlertsForStation')"
            @click.stop="requestStationNotification(station)"
          >
            <Bell :size="16" aria-hidden="true" />
          </button>
        </div>
      </div>

      <div v-if="geocodeResults.length > 0" class="results-section">
        <div class="section-title">{{ t('addresses') }}</div>
        <button
          v-for="(result, index) in geocodeResults"
          :key="`address-${index}`"
          type="button"
          class="result-row"
          @click="selectAddress(result)"
        >
          <MapPin :size="16" class="row-icon" aria-hidden="true" />
          <span class="row-text">
            <span class="row-name">{{ result.displayName }}</span>
            <span v-if="result.addressDetails.suburb || result.addressDetails.city" class="row-sub">
              {{ [result.addressDetails.suburb, result.addressDetails.city].filter(Boolean).join(', ') }}
            </span>
          </span>
        </button>
      </div>
    </div>

    <div v-if="showResults && !directionsActive && searchQuery && geocodeResults.length === 0 && filteredStations.length === 0 && !isSearching" class="search-panel search-panel--note">
      {{ t('noResultsFound') }}
    </div>

    <div v-if="isSearching" class="search-panel search-panel--note">
      {{ t('searching') }}
    </div>

    <button
      v-if="tripMode && originLocation && destinationLocation"
      type="button"
      @click="searchRoutes"
      class="search-routes-btn"
      :disabled="isSearchingRoutes"
    >
      <Search :size="18" aria-hidden="true" />
      {{ isSearchingRoutes ? t('searching') : t('searchRoutesBtn') }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Station } from '@/services/apiService'
import { Bell, Briefcase, Bus, History, Home, MapPin, Search, Star, X } from 'lucide-vue-next'
import { useFavorites, type FavoriteLocation } from '@/composables/useFavorites'
import { useRecentSearches } from '@/composables/useRecentSearches'
import { useStatistics } from '@/composables/useStatistics'
import { searchAddresses, type GeocodingResult } from '@/services/geocodingService'
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
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1250; /* deasupra panourilor laterale */
  width: 360px;
  max-width: calc(100% - 200px);
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-family: var(--font-sans);
}

@media (max-width: 767px) {
  .enhanced-search-container {
    left: 16px;
    right: 72px;
    transform: none;
    top: 16px;
    width: auto;
    max-width: none;
  }
}

.search-box {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 52px;
  padding: 4px 6px 4px 16px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  box-shadow: var(--shadow-lg);
  cursor: text;
}

.search-box:focus-within {
  box-shadow: var(--ring-focus), var(--shadow-lg);
}

.search-icon {
  color: var(--text-secondary);
  flex-shrink: 0;
}

.search-text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.search-label {
  font-size: 12px;
  font-weight: var(--fw-semibold);
  color: var(--text-secondary);
}

.search-input {
  width: 100%;
  min-width: 0;
  padding: 0;
  border: none;
  background: transparent;
  font: inherit;
  font-size: 16px;
  color: var(--text-primary);
  outline: none;
}

.search-input::placeholder {
  color: var(--text-tertiary);
}

.search-input::-webkit-search-cancel-button {
  display: none;
}

.clear-btn {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.clear-btn:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

/* Panou derulant cu rezultate */
.search-panel {
  max-height: 60vh;
  overflow-y: auto;
  border-radius: var(--radius-lg);
  background: var(--bg-elevated);
  box-shadow: var(--shadow-xl);
  padding: 6px 0;
}

.search-panel--note {
  padding: 14px 16px;
  font-size: 14px;
  color: var(--text-secondary);
}

.results-section + .results-section {
  border-top: 1px solid var(--border-subtle);
  margin-top: 4px;
  padding-top: 4px;
}

.section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px 4px;
  font-size: 12px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.link-btn {
  min-height: 32px;
  border: none;
  background: none;
  color: var(--accent-primary);
  font: inherit;
  font-size: 12px;
  font-weight: var(--fw-bold);
  letter-spacing: normal;
  text-transform: none;
  cursor: pointer;
}

.recent-row {
  display: flex;
  align-items: center;
  padding-right: 8px;
}

.result-row {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  min-height: 48px;
  padding: 6px 16px;
  border: none;
  background: none;
  font: inherit;
  text-align: left;
  color: var(--text-primary);
  cursor: pointer;
}

.result-row:hover,
.result-row:focus-visible {
  background: var(--bg-secondary);
  outline: none;
}

.row-icon {
  color: var(--text-secondary);
  flex-shrink: 0;
}

.row-text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.row-name {
  font-size: 15px;
  font-weight: var(--fw-semibold);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.row-sub {
  font-size: 12px;
  color: var(--text-secondary);
}

.row-meta {
  font-size: 13px;
  color: var(--text-secondary);
  font-variant-numeric: tabular-nums;
}

.favorites-row {
  display: flex;
  gap: 8px;
  padding: 4px 16px 8px;
  overflow-x: auto;
  scrollbar-width: none;
}

.favorite-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-full);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  font-size: 14px;
  font-weight: var(--fw-semibold);
  white-space: nowrap;
  cursor: pointer;
}

.favorite-chip:hover {
  background: var(--bg-tertiary);
}

.chip-name {
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.search-routes-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 48px;
  border: none;
  border-radius: var(--radius-md);
  background: var(--accent-primary);
  color: var(--text-on-accent);
  font: inherit;
  font-weight: var(--fw-bold);
  cursor: pointer;
  box-shadow: var(--shadow-lg);
}

.search-routes-btn:disabled {
  opacity: 0.5;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}
</style>
