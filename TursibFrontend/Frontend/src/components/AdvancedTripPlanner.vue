<template>
  <div class="trip-planner">
    <div class="planner-header">
      <h2>🗓️ Planificare Călătorie</h2>
      <p>Găsește cea mai bună rută și orar pentru călătoria ta</p>
    </div>

    <div class="search-form">
      <!-- Punct de plecare -->
      <div class="form-group">
        <label>📍 Plecare din:</label>
        <div class="location-input-group">
          <input
            ref="fromAddressInput"
            v-model="searchFrom"
            @input="handleFromInput"
            placeholder="Caută adresă sau stație..."
            class="location-input"
          />
          <button @click="useMyLocation" class="btn-location" title="Locația mea">
            📍
          </button>
        </div>
        <div v-if="showFromSuggestions && (addressFromSuggestions.length || fromSuggestions.length)" class="suggestions">
          <div
            v-for="(address, idx) in addressFromSuggestions"
            :key="'addr-' + idx"
            @click="selectFromAddress(address)"
            class="suggestion-item address-suggestion"
          >
            📍 {{ address.display_name }}
          </div>
          <div
            v-for="station in fromSuggestions"
            :key="'station-' + station.id"
            @click="selectFromStation(station)"
            class="suggestion-item"
          >
            🚏 {{ station.name }}
          </div>
        </div>
      </div>

      <!-- Punct de sosire -->
      <div class="form-group">
        <label>🎯 Destinație:</label>
        <input
          ref="toAddressInput"
          v-model="searchTo"
          @input="handleToInput"
          placeholder="Caută adresă sau stație..."
          class="location-input"
        />
        <div v-if="showToSuggestions && (addressToSuggestions.length || toSuggestions.length)" class="suggestions">
          <div
            v-for="(address, idx) in addressToSuggestions"
            :key="'addr-' + idx"
            @click="selectToAddress(address)"
            class="suggestion-item address-suggestion"
          >
            📍 {{ address.display_name }}
          </div>
          <div
            v-for="station in toSuggestions"
            :key="'station-' + station.id"
            @click="selectToStation(station)"
            class="suggestion-item"
          >
            🚏 {{ station.name }}
          </div>
        </div>
      </div>

      <!-- Mod căutare: Pleacă acum / Pleacă la ora / Ajunge la ora -->
      <div class="form-row">
        <div class="form-group">
          <label>🕐 Când:</label>
          <select v-model="searchMode" class="time-input">
            <option value="leaveNow">Pleacă acum</option>
            <option value="departAt">Pleacă la ora</option>
            <option value="arriveBy">Ajunge la ora</option>
          </select>
        </div>
        <div v-if="searchMode !== 'leaveNow'" class="form-group">
          <label>📅 Data:</label>
          <input
            v-model="selectedDate"
            type="date"
            :min="today"
            class="date-input"
          />
        </div>
        <div v-if="searchMode !== 'leaveNow'" class="form-group">
          <label>{{ searchMode === 'arriveBy' ? '🏁 Ora sosirii:' : '🕐 Ora plecării:' }}</label>
          <input
            v-model="selectedTime"
            type="time"
            class="time-input"
          />
        </div>
      </div>

      <!-- Preferințe -->
      <div class="preferences">
        <label class="checkbox-label">
          <input v-model="preferences.minTransfers" type="checkbox" />
          Preferă trasee cu mai puține schimbări
        </label>
        <label class="checkbox-label">
          <input v-model="preferences.maxWalkingDistance" type="checkbox" />
          Limitează distanța de mers pe jos (max 500m)
        </label>
      </div>

      <button @click="searchTrips" class="btn-search" :disabled="!canSearch">
        🔍 Caută Trasee
      </button>
    </div>

    <!-- Rezultate -->
    <div v-if="isSearching" class="loading">
      <div class="spinner"></div>
      <p>Căutare trasee disponibile...</p>
    </div>

    <div v-if="!isSearching && searchResults.length" class="results">
      <h3>📋 Trasee găsite ({{ searchResults.length }})</h3>
      <div
        v-for="(trip, index) in searchResults"
        :key="index"
        class="trip-card"
        :class="{ 'recommended': index === 0 }"
      >
        <div v-if="index === 0" class="recommended-badge">⭐ Recomandat</div>
        
        <div class="trip-header">
          <div class="trip-time">
            <span class="time-large">{{ trip.departureTime }}</span>
            <span class="arrow">→</span>
            <span class="time-large">{{ trip.arrivalTime }}</span>
          </div>
          <div class="trip-duration">
            <span class="duration">{{ trip.totalDuration }} min</span>
          </div>
        </div>

        <div class="trip-segments">
          <div
            v-for="(segment, segIndex) in trip.segments"
            :key="segIndex"
            class="segment"
          >
            <!-- Walking segment -->
            <div v-if="segment.type === 'walk'" class="segment-walk">
              <div class="segment-icon">🚶</div>
              <div class="segment-details">
                <div class="segment-title">Mers pe jos</div>
                <div class="segment-info">
                  {{ segment.distance }}m · {{ segment.duration }} min
                </div>
                <div class="segment-route">
                  {{ segment.from }} → {{ segment.to }}
                </div>
              </div>
            </div>

            <!-- Bus segment -->
            <div v-if="segment.type === 'bus'" class="segment-bus">
              <div class="segment-icon" :style="{ backgroundColor: segment.routeColor }">
                🚌 {{ segment.routeNumber }}
              </div>
              <div class="segment-details">
                <div class="segment-title">
                  Autobuz {{ segment.routeNumber }} - {{ segment.routeName }}
                </div>
                <div class="segment-info">
                  {{ segment.stopsCount }} stații · {{ segment.duration }} min
                </div>
                <div class="segment-route">
                  <strong>{{ segment.boardingTime }}</strong> {{ segment.from }}
                  <br />
                  <strong>{{ segment.alightingTime }}</strong> {{ segment.to }}
                </div>
              </div>
            </div>

            <!-- Transfer indicator -->
            <div v-if="segIndex < trip.segments.length - 1" class="transfer-indicator">
              ⤵ Schimbare
            </div>
          </div>
        </div>

        <div class="trip-footer">
          <div class="trip-stats">
            <span>🚌 {{ trip.transfersCount }} schimbări</span>
            <span>🚶 {{ trip.totalWalkingDistance }}m mers pe jos</span>
          </div>
          <button @click="selectTrip(trip)" class="btn-select">
            Selectează
          </button>
        </div>
      </div>
    </div>

    <div v-if="!isSearching && searchAttempted && searchResults.length === 0" class="no-results">
      <div class="no-results-icon">🚫</div>
      <h3>Nu s-au găsit trasee</h3>
      <p>Încearcă să modifici criteriile de căutare sau intervalul orar.</p>
    </div>

    <!-- Hartă cu traseu -->
    <div v-if="showMap && selectedTrip" class="map-overlay">
      <div class="map-container">
        <div class="map-header">
          <h3>🗺️ Traseu pe hartă</h3>
          <button @click="closeMap" class="close-map-btn">✕</button>
        </div>
        <div class="map-content">
          <iframe
            :src="getGoogleMapsUrl(selectedTrip)"
            width="100%"
            height="500"
            style="border:0;"
            allowfullscreen
            loading="lazy"
            referrerpolicy="no-referrer-when-downgrade"
          ></iframe>
        </div>
        <div class="map-footer">
          <p>💡 Traseul afișat include călătoria completă de la {{fromLocation?.name}} la {{toLocation?.name}}</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { Station } from '@/services/apiService'
import apiService from '@/services/apiService'

interface TripSegment {
  type: 'walk' | 'bus'
  from: string
  to: string
  duration: number
  distance?: number
  routeNumber?: string
  routeName?: string
  routeColor?: string
  boardingTime?: string
  alightingTime?: string
  stopsCount?: number
  fromCoords?: { lat: number; lon: number }
  toCoords?: { lat: number; lon: number }
}

interface TripOption {
  departureTime: string
  arrivalTime: string
  totalDuration: number
  transfersCount: number
  totalWalkingDistance: number
  segments: TripSegment[]
}

interface GeocodeResult {
  lat: string
  lon: string
  display_name: string
}

const emit = defineEmits<{
  tripSelected: [trip: TripOption]
}>()

// Form state
const searchFrom = ref('')
const searchTo = ref('')
const fromLocation = ref<{ lat: number; lon: number; name: string } | null>(null)
const toLocation = ref<{ lat: number; lon: number; name: string } | null>(null)
const selectedDate = ref('')
const selectedTime = ref('')
const searchMode = ref<'leaveNow' | 'departAt' | 'arriveBy'>('leaveNow')
const userLocation = ref<{ lat: number; lon: number } | null>(null)

// Suggestions
const showFromSuggestions = ref(false)
const showToSuggestions = ref(false)
const fromSuggestions = ref<Station[]>([])
const toSuggestions = ref<Station[]>([])
const addressFromSuggestions = ref<GeocodeResult[]>([])
const addressToSuggestions = ref<GeocodeResult[]>([])
const allStations = ref<Station[]>([])

// Preferences
const preferences = ref({
  minTransfers: true,
  maxWalkingDistance: false
})

// Results
const isSearching = ref(false)
const searchAttempted = ref(false)
const searchResults = ref<TripOption[]>([])
const selectedTrip = ref<TripOption | null>(null)
const showMap = ref(false)

// Debounce timers
let fromSearchTimeout: number | null = null
let toSearchTimeout: number | null = null

// Computed
const today = computed(() => {
  const now = new Date()
  return now.toISOString().split('T')[0]
})

const canSearch = computed(() => {
  if (!fromLocation.value || !toLocation.value) return false
  if (searchMode.value === 'leaveNow') return true
  return !!selectedDate.value && !!selectedTime.value
})

// Methods
const loadAllStations = async () => {
  try {
    allStations.value = await apiService.getStations()
  } catch (error) {
    console.error('Error loading stations:', error)
  }
}

// Geocoding cu Nominatim (același sistem ca EnhancedSearch)
const searchAddress = async (query: string, isFrom: boolean) => {
  if (query.length < 3) {
    if (isFrom) {
      addressFromSuggestions.value = []
    } else {
      addressToSuggestions.value = []
    }
    return
  }
  
  try {
    const searchQuery = `${query}, Sibiu, Romania`
    const params = new URLSearchParams({
      format: 'json',
      q: searchQuery,
      limit: '5',
      addressdetails: '1',
      extratags: '1',
      namedetails: '1',
      'accept-language': 'ro'
    })
    
    const response = await fetch(
      `https://nominatim.openstreetmap.org/search?${params.toString()}`
    )
    const data = await response.json()
    
    // Filtrează rezultate prea imprecise
    const filtered = data.filter((result: any) => {
      if (result.boundingbox) {
        const bbox = result.boundingbox.map((coord: string) => parseFloat(coord))
        const latDiff = Math.abs(bbox[1] - bbox[0])
        const lonDiff = Math.abs(bbox[3] - bbox[2])
        if (latDiff > 0.01 || lonDiff > 0.01) {
          return false
        }
      }
      return true
    })
    
    if (isFrom) {
      addressFromSuggestions.value = filtered
    } else {
      addressToSuggestions.value = filtered
    }
  } catch (error) {
    console.error('Eroare geocoding:', error)
  }
}

const handleFromInput = () => {
  showFromSuggestions.value = true
  
  if (searchFrom.value.length < 2) {
    fromSuggestions.value = []
    addressFromSuggestions.value = []
    return
  }
  
  // Caută stații
  const query = searchFrom.value.toLowerCase()
  fromSuggestions.value = allStations.value
    .filter(s => s.name.toLowerCase().includes(query))
    .slice(0, 5)
  
  // Caută adrese cu debounce
  if (fromSearchTimeout) {
    clearTimeout(fromSearchTimeout)
  }
  fromSearchTimeout = setTimeout(() => {
    searchAddress(searchFrom.value, true)
  }, 500)
}

const handleToInput = () => {
  showToSuggestions.value = true
  
  if (searchTo.value.length < 2) {
    toSuggestions.value = []
    addressToSuggestions.value = []
    return
  }
  
  // Caută stații
  const query = searchTo.value.toLowerCase()
  toSuggestions.value = allStations.value
    .filter(s => s.name.toLowerCase().includes(query))
    .slice(0, 5)
  
  // Caută adrese cu debounce
  if (toSearchTimeout) {
    clearTimeout(toSearchTimeout)
  }
  toSearchTimeout = setTimeout(() => {
    searchAddress(searchTo.value, false)
  }, 500)
}

const selectFromAddress = (address: GeocodeResult) => {
  fromLocation.value = {
    lat: parseFloat(address.lat),
    lon: parseFloat(address.lon),
    name: address.display_name
  }
  searchFrom.value = address.display_name
  showFromSuggestions.value = false
  addressFromSuggestions.value = []
  fromSuggestions.value = []
}

const selectToAddress = (address: GeocodeResult) => {
  toLocation.value = {
    lat: parseFloat(address.lat),
    lon: parseFloat(address.lon),
    name: address.display_name
  }
  searchTo.value = address.display_name
  showToSuggestions.value = false
  addressToSuggestions.value = []
  toSuggestions.value = []
}

const selectFromStation = (station: Station) => {
  fromLocation.value = {
    lat: station.latitude,
    lon: station.longitude,
    name: station.name
  }
  searchFrom.value = station.name
  showFromSuggestions.value = false
  addressFromSuggestions.value = []
  fromSuggestions.value = []
}

const selectToStation = (station: Station) => {
  toLocation.value = {
    lat: station.latitude,
    lon: station.longitude,
    name: station.name
  }
  searchTo.value = station.name
  showToSuggestions.value = false
  addressToSuggestions.value = []
  toSuggestions.value = []
}

const useMyLocation = () => {
  if ('geolocation' in navigator) {
    navigator.geolocation.getCurrentPosition(
      (position) => {
        fromLocation.value = {
          lat: position.coords.latitude,
          lon: position.coords.longitude,
          name: '📍 Locația mea'
        }
        searchFrom.value = '📍 Locația mea'
        showFromSuggestions.value = false
        addressFromSuggestions.value = []
        fromSuggestions.value = []
      },
      (error) => {
        console.error('Error getting location:', error)
        alert('Nu s-a putut obține locația. Verifică permisiunile browser-ului.')
      }
    )
  } else {
    alert('Geolocation nu este suportat de browser-ul tău.')
  }
}

const calculateDistance = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371e3 // Earth radius in meters
  const φ1 = lat1 * Math.PI / 180
  const φ2 = lat2 * Math.PI / 180
  const Δφ = (lat2 - lat1) * Math.PI / 180
  const Δλ = (lon2 - lon1) * Math.PI / 180

  const a = Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
    Math.cos(φ1) * Math.cos(φ2) *
    Math.sin(Δλ / 2) * Math.sin(Δλ / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))

  return Math.round(R * c)
}

const searchTrips = async () => {
  if (!canSearch.value) return

  isSearching.value = true
  searchAttempted.value = true
  searchResults.value = []

  try {
    const nearestToStart = findNearestStation(fromLocation.value!)
    const nearestToEnd = findNearestStation(toLocation.value!)

    if (!nearestToStart || !nearestToEnd) {
      alert('Nu s-au găsit stații apropiate de locațiile selectate')
      return
    }

    // Pregătește payload-ul pentru backend în funcție de modul de căutare
    let apiOptions: { mode: 'departAt' | 'arriveBy'; dateTime: string } | undefined
    if (searchMode.value !== 'leaveNow') {
      apiOptions = {
        mode: searchMode.value,
        dateTime: `${selectedDate.value}T${selectedTime.value}:00`
      }
    }

    const backendRoutes = await apiService.calculateRouteAlternatives(
      nearestToStart.id,
      nearestToEnd.id,
      apiOptions
    )

    if (!backendRoutes || backendRoutes.length === 0) {
      searchResults.value = []
      return
    }

    // Distanțele de walking între start/end și cele mai apropiate stații
    const walkToStartDist = calculateDistance(
      fromLocation.value!.lat, fromLocation.value!.lon,
      nearestToStart.latitude, nearestToStart.longitude
    )
    const walkFromEndDist = calculateDistance(
      nearestToEnd.latitude, nearestToEnd.longitude,
      toLocation.value!.lat, toLocation.value!.lon
    )
    // Aproximare: 5 km/h mers pe jos ≈ 12 min/km ≈ 1 min/83m
    const walkToStartDuration = Math.max(1, Math.round(walkToStartDist / 83))
    const walkFromEndDuration = Math.max(1, Math.round(walkFromEndDist / 83))

    searchResults.value = backendRoutes.map((route: any) => {
      const segments: TripSegment[] = []

      // Segment inițial de walking (dacă utilizatorul nu e deja la stație)
      if (walkToStartDist > 20) {
        segments.push({
          type: 'walk',
          from: fromLocation.value!.name,
          to: nearestToStart.name,
          duration: walkToStartDuration,
          distance: walkToStartDist,
          fromCoords: { lat: fromLocation.value!.lat, lon: fromLocation.value!.lon },
          toCoords: { lat: nearestToStart.latitude, lon: nearestToStart.longitude }
        })
      }

      // Mapează segmentele din backend
      for (const seg of (route.segments ?? [])) {
        const startStation = seg.startStation
        const endStation = seg.endStation
        if (seg.type === 'bus') {
          segments.push({
            type: 'bus',
            from: startStation?.name ?? '?',
            to: endStation?.name ?? '?',
            duration: seg.duration,
            routeNumber: seg.routeNumber ?? '',
            routeName: seg.routeName ?? '',
            routeColor: seg.color ?? '#888',
            boardingTime: formatBackendTime(seg.startTime),
            alightingTime: formatBackendTime(seg.endTime),
            stopsCount: seg.stationCount ?? 0,
            fromCoords: startStation ? { lat: startStation.latitude, lon: startStation.longitude } : undefined,
            toCoords: endStation ? { lat: endStation.latitude, lon: endStation.longitude } : undefined
          })
        } else {
          // walk (între stații)
          segments.push({
            type: 'walk',
            from: startStation?.name ?? '?',
            to: endStation?.name ?? '?',
            duration: seg.duration,
            distance: Math.round((seg.distance ?? 0) * 1000),
            fromCoords: startStation ? { lat: startStation.latitude, lon: startStation.longitude } : undefined,
            toCoords: endStation ? { lat: endStation.latitude, lon: endStation.longitude } : undefined
          })
        }
      }

      // Segment final de walking (de la ultima stație la destinație)
      if (walkFromEndDist > 20) {
        segments.push({
          type: 'walk',
          from: nearestToEnd.name,
          to: toLocation.value!.name,
          duration: walkFromEndDuration,
          distance: walkFromEndDist,
          fromCoords: { lat: nearestToEnd.latitude, lon: nearestToEnd.longitude },
          toCoords: { lat: toLocation.value!.lat, lon: toLocation.value!.lon }
        })
      }

      const busSegments = segments.filter(s => s.type === 'bus')
      const walkSegments = segments.filter(s => s.type === 'walk')
      const totalWalkingDistance = walkSegments.reduce((acc, s) => acc + (s.distance ?? 0), 0)
      const extraWalkDuration = (walkToStartDist > 20 ? walkToStartDuration : 0) + (walkFromEndDist > 20 ? walkFromEndDuration : 0)
      const totalDuration = (route.totalDuration ?? 0) + extraWalkDuration

      const routeDeparture = formatBackendTime(route.departureTime)
      const routeArrival = addMinutes(routeDeparture, totalDuration)

      return {
        departureTime: routeDeparture,
        arrivalTime: routeArrival,
        totalDuration,
        transfersCount: Math.max(0, busSegments.length - 1),
        totalWalkingDistance,
        segments
      } as TripOption
    })

  } catch (error) {
    console.error('Error searching trips:', error)
    alert('Eroare la căutarea traseelor. Verifică dacă backend-ul rulează.')
  } finally {
    isSearching.value = false
  }
}

const findNearestStation = (location: { lat: number; lon: number }): Station | null => {
  if (allStations.value.length === 0) return null
  
  const first = allStations.value[0]
  if (!first) return null
  
  let nearest: Station = first
  let minDistance = calculateDistance(location.lat, location.lon, nearest.latitude, nearest.longitude)
  
  for (const station of allStations.value) {
    const distance = calculateDistance(location.lat, location.lon, station.latitude, station.longitude)
    if (distance < minDistance) {
      minDistance = distance
      nearest = station
    }
  }
  
  return nearest
}

const formatBackendTime = (iso?: string): string => {
  if (!iso) return ''
  // Backend returnează DateTime ISO (ex: "2026-04-21T09:25:00"). Extragem HH:MM local.
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

const addMinutes = (time: string, minutes: number): string => {
  const parts = time.split(':').map(Number)
  const hours = parts[0] || 0
  const mins = parts[1] || 0
  const totalMinutes = hours * 60 + mins + minutes
  const newHours = Math.floor(totalMinutes / 60) % 24
  const newMins = totalMinutes % 60
  return `${String(newHours).padStart(2, '0')}:${String(newMins).padStart(2, '0')}`
}

const selectTrip = (trip: TripOption) => {
  selectedTrip.value = trip
  showMap.value = true
  emit('tripSelected', trip)
}

const closeMap = () => {
  showMap.value = false
  selectedTrip.value = null
}

const getGoogleMapsUrl = (trip: TripOption) => {
  if (!fromLocation.value || !toLocation.value) return ''
  
  // Construiește URL-ul pentru Google Maps Directions
  // Format: origin=lat,lng&destination=lat,lng&waypoints=lat,lng|lat,lng
  const origin = `${fromLocation.value.lat},${fromLocation.value.lon}`
  const destination = `${toLocation.value.lat},${toLocation.value.lon}`
  
  // Adaugă waypoints pentru fiecare stație intermediară
  const waypoints: string[] = []
  trip.segments.forEach(segment => {
    if (segment.type === 'bus' && segment.fromCoords) {
      waypoints.push(`${segment.fromCoords.lat},${segment.fromCoords.lon}`)
    }
    if (segment.type === 'bus' && segment.toCoords) {
      waypoints.push(`${segment.toCoords.lat},${segment.toCoords.lon}`)
    }
  })
  
  const waypointsParam = waypoints.length > 0 ? `&waypoints=${waypoints.join('|')}` : ''
  
  return `https://www.google.com/maps/embed/v1/directions?key=AIzaSyBFw0Qbyq9zTFTd-tUY6dZWTgaQzuU17R8&origin=${origin}&destination=${destination}${waypointsParam}&mode=transit`
}

// Initialize
onMounted(() => {
  loadAllStations()
  
  // Set default date to today
  const todayValue = today.value
  if (todayValue) {
    selectedDate.value = todayValue
  }
  
  // Set default time to current time
  const now = new Date()
  selectedTime.value = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
})

// Close suggestions when clicking outside
document.addEventListener('click', (e) => {
  const target = e.target as HTMLElement
  if (!target.closest('.form-group')) {
    showFromSuggestions.value = false
    showToSuggestions.value = false
  }
})
</script>

<style scoped>
.trip-planner {
  background: white;
  border-radius: 16px;
  padding: 24px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
}

.planner-header {
  margin-bottom: 24px;
  text-align: center;
}

.planner-header h2 {
  font-size: 28px;
  font-weight: 800;
  color: #000;
  margin: 0 0 8px 0;
}

.planner-header p {
  color: #6b7280;
  margin: 0;
}

.search-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  position: relative;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  font-weight: 600;
  color: #000;
  font-size: 14px;
}

.location-input-group {
  display: flex;
  gap: 8px;
}

.location-input,
.date-input,
.time-input {
  flex: 1;
  padding: 12px 16px;
  border: 2px solid #e5e7eb;
  border-radius: 10px;
  font-size: 15px;
  transition: all 0.2s;
}

.location-input:focus,
.date-input:focus,
.time-input:focus {
  outline: none;
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.btn-location {
  padding: 12px 16px;
  background: #3b82f6;
  color: white;
  border: none;
  border-radius: 10px;
  font-size: 18px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-location:hover {
  background: #2563eb;
}

.suggestions {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: white;
  border: 2px solid #e5e7eb;
  border-radius: 10px;
  margin-top: 4px;
  max-height: 200px;
  overflow-y: auto;
  z-index: 100;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.suggestion-item {
  padding: 12px 16px;
  cursor: pointer;
  transition: background 0.2s;
  color: #000;
}

.suggestion-item:hover {
  background: #f3f4f6;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.preferences {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: #f9fafb;
  border-radius: 10px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  color: #374151;
  font-size: 14px;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.btn-search {
  padding: 14px 24px;
  background: #3b82f6;
  color: white;
  border: none;
  border-radius: 10px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-search:hover:not(:disabled) {
  background: #2563eb;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3);
}

.btn-search:disabled {
  background: #9ca3af;
  cursor: not-allowed;
}

.loading {
  text-align: center;
  padding: 40px;
  color: #6b7280;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid #e5e7eb;
  border-top-color: #3b82f6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin: 0 auto 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.results {
  margin-top: 24px;
}

.results h3 {
  font-size: 20px;
  font-weight: 700;
  color: #000;
  margin: 0 0 16px 0;
}

.trip-card {
  background: white;
  border: 2px solid #e5e7eb;
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 16px;
  transition: all 0.2s;
  position: relative;
}

.trip-card:hover {
  border-color: #3b82f6;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.trip-card.recommended {
  border-color: #fbbf24;
  background: linear-gradient(135deg, #fffbeb 0%, #ffffff 100%);
}

.recommended-badge {
  position: absolute;
  top: -12px;
  right: 20px;
  background: #fbbf24;
  color: #000;
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 700;
}

.trip-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  padding-bottom: 16px;
  border-bottom: 2px solid #f3f4f6;
}

.trip-time {
  display: flex;
  align-items: center;
  gap: 16px;
}

.time-large {
  font-size: 24px;
  font-weight: 700;
  color: #000;
}

.arrow {
  font-size: 20px;
  color: #9ca3af;
}

.trip-duration .duration {
  font-size: 18px;
  font-weight: 600;
  color: #3b82f6;
}

.trip-segments {
  margin-bottom: 16px;
}

.segment {
  margin-bottom: 12px;
}

.segment-walk,
.segment-bus {
  display: flex;
  gap: 16px;
  padding: 12px;
  background: #f9fafb;
  border-radius: 10px;
}

.segment-icon {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 10px;
  font-size: 20px;
  flex-shrink: 0;
}

.segment-walk .segment-icon {
  background: #e0f2fe;
}

.segment-bus .segment-icon {
  color: white;
  font-weight: 700;
  font-size: 14px;
}

.segment-details {
  flex: 1;
}

.segment-title {
  font-weight: 600;
  color: #000;
  margin-bottom: 4px;
}

.segment-info {
  font-size: 13px;
  color: #6b7280;
  margin-bottom: 4px;
}

.segment-route {
  font-size: 14px;
  color: #374151;
  line-height: 1.5;
}

.transfer-indicator {
  text-align: center;
  padding: 8px;
  color: #9ca3af;
  font-size: 14px;
  font-weight: 600;
}

.trip-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 16px;
  border-top: 2px solid #f3f4f6;
}

.trip-stats {
  display: flex;
  gap: 16px;
  font-size: 14px;
  color: #6b7280;
}

.btn-select {
  padding: 10px 20px;
  background: #3b82f6;
  color: white;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-select:hover {
  background: #2563eb;
}

.no-results {
  text-align: center;
  padding: 60px 20px;
  color: #6b7280;
}

.no-results-icon {
  font-size: 64px;
  margin-bottom: 16px;
}

.no-results h3 {
  font-size: 24px;
  font-weight: 700;
  color: #000;
  margin: 0 0 8px 0;
}

/* Map overlay */
.map-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10000;
  padding: 20px;
}

.map-container {
  background: white;
  border-radius: 16px;
  width: 100%;
  max-width: 900px;
  max-height: 90vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.map-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 2px solid #e5e7eb;
}

.map-header h3 {
  margin: 0;
  font-size: 22px;
  font-weight: 700;
  color: #000;
}

.close-map-btn {
  width: 36px;
  height: 36px;
  border: none;
  background: #f3f4f6;
  border-radius: 8px;
  font-size: 20px;
  cursor: pointer;
  transition: all 0.2s;
  color: #6b7280;
}

.close-map-btn:hover {
  background: #e5e7eb;
  color: #000;
}

.map-content {
  flex: 1;
  overflow: hidden;
}

.map-footer {
  padding: 16px 24px;
  background: #f9fafb;
  border-top: 2px solid #e5e7eb;
}

.map-footer p {
  margin: 0;
  font-size: 14px;
  color: #6b7280;
  text-align: center;
}

/* Address suggestions styling */
.address-suggestion {
  background: linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%);
  border-left: 3px solid #3b82f6;
}

.address-suggestion:hover {
  background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%);
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
  
  .trip-header {
    flex-direction: column;
    gap: 12px;
  }
  
  .trip-footer {
    flex-direction: column;
    gap: 12px;
  }
  
  .map-overlay {
    padding: 10px;
  }
  
  .map-container {
    max-height: 95vh;
  }
}
</style>
