<template>

  <div class="map-container">
    <!-- Buton pentru ascunderea/afișarea sidebar-ului -->
    <button 
      @click="showSidebar = !showSidebar; emit('sidebarToggle', showSidebar)" 
      class="sidebar-toggle-btn"
      :title="showSidebar ? 'Ascunde sidebar' : 'Arată sidebar'"
    >
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path v-if="showSidebar" d="M15 18l-6-6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        <path v-else d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>
    </button>
    
    <!-- Enhanced Search pentru stații și adrese -->
    <EnhancedSearch 
      v-if="showSidebar"
      :stations="allStations"
      :user-location="userLocation"
      :trip-mode="tripMode"
      @station-selected="handleStationSelected"
      @address-selected="handleAddressSelected"
      @walking-directions-requested="handleWalkingDirectionsRequested"
      @multimodal-route-requested="handleMultimodalRouteRequested"
      @route-search-requested="handleRouteSearchRequested"
    />
    
    <!-- Buton pentru locație -->
    <LocationButton @location-found="handleLocationFound" />
    
    <!-- Panoul multimodal (mers pe jos + autobuz + mers pe jos) -->
    <MultimodalDirections
      v-if="showMultimodal && !showTransfer"
      :visible="showMultimodal"
      :start-location="multimodalData.startLocation"
      :end-location="multimodalData.endLocation"
      :boarding-station="multimodalData.boardingStation"
      :alighting-station="multimodalData.alightingStation"
      :bus-line="multimodalData.busLine"
      :bus-color="multimodalData.busColor"
      :bus-stations-list="multimodalData.busStationsList"
      :first-walk-distance="multimodalData.firstWalkDistance"
      :first-walk-time="multimodalData.firstWalkTime"
      :second-walk-distance="multimodalData.secondWalkDistance"
      :second-walk-time="multimodalData.secondWalkTime"
      :bus-time="multimodalData.busTime"
      @close="closeMultimodal"
    />
    
    <!-- Panoul pentru trasee cu transfer -->
    <TransferRoute
      v-if="showTransfer"
      :visible="showTransfer"
      :start-name="transferData.startName"
      :end-name="transferData.endName"
      :boarding-station="transferData.boardingStation"
      :transfer-station="transferData.transferStation"
      :alighting-station="transferData.alightingStation"
      :route1-number="transferData.route1Number"
      :route1-color="transferData.route1Color"
      :route1-stations-count="transferData.route1StationsCount"
      :route2-number="transferData.route2Number"
      :route2-color="transferData.route2Color"
      :route2-stations-count="transferData.route2StationsCount"
      :first-walk-distance="transferData.firstWalkDistance"
      :first-walk-time="transferData.firstWalkTime"
      :bus-time1="transferData.busTime1"
      :bus-time2="transferData.busTime2"
      :second-walk-distance="transferData.secondWalkDistance"
      :second-walk-time="transferData.secondWalkTime"
      @close="closeTransfer"
    />
    
    <!-- Panoul de direcții de mers pe jos (pentru căutări simple) -->
    <WalkingDirections
      v-if="!showMultimodal && !showTransfer"
      :visible="showDirections"
      :start-lat="walkingStart?.lat"
      :start-lon="walkingStart?.lon"
      :end-lat="walkingEnd?.lat"
      :end-lon="walkingEnd?.lon"
      :start-name="walkingStart?.name"
      :end-name="walkingEnd?.name"
      @close="closeDirections"
      @route-calculated="handleWalkingRouteCalculated"
      @snapped-coordinates="handleSnappedCoordinates"
    />
    
    <!-- Panel pentru stații apropiate (înlocuiește markerele Leaflet) -->
    <NearbyStationsPanel
      :visible="showNearbyStations"
      :stations="nearbyStations"
      :active-notification-station-id="getNotificationSettings().enabled ? getNotificationSettings().stationId : null"
      :get-station-e-t-as="getStationETAs"
      @close="showNearbyStations = false"
      @toggleNotification="handleNotificationToggle"
    />

    <l-map

      v-if="isReady"

      ref="map"

      :zoom="zoom"

      :center="center"

      :options="mapOptions"

      style="height: 100%; width: 100%"

    >

      <!-- Layer-ul de tile-uri (harta de bază) -->

      <l-tile-layer

        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"

        :attribution="attribution"

      />

      <!-- Marker pentru locația utilizatorului -->
      <l-marker
        v-if="userLocation"
        :lat-lng="[userLocation.lat, userLocation.lon]"
      >
        <l-icon
          :icon-size="[40, 40]"
          :icon-anchor="[20, 40]"
          icon-url="/location-pin.png"
        />
        <l-popup>
          <strong>Tu ești aici</strong>
        </l-popup>
      </l-marker>
      
      <!-- Marker pentru adresa selectată -->
      <l-marker
        v-if="selectedAddress"
        :lat-lng="[selectedAddress.lat, selectedAddress.lon]"
      >
        <l-icon
          :icon-size="[40, 40]"
          :icon-anchor="[20, 40]"
          icon-url="/placeholder.png"
        />
        <l-popup>
          <strong>{{ selectedAddress.name }}</strong>
        </l-popup>
      </l-marker>
      
      <!-- Markere pentru coordonatele snapped (puncte exacte pe stradă) -->
      <l-marker
        v-if="snappedStart && showDirections"
        :lat-lng="[snappedStart.lat, snappedStart.lon]"
      >
        <l-icon
          :icon-size="[20, 20]"
          :icon-anchor="[10, 10]"
        >
          <div class="snapped-marker start">
            🟢
          </div>
        </l-icon>
        <l-popup>
          <strong>Start traseu</strong><br>
          <small>Punct pe stradă</small>
        </l-popup>
      </l-marker>
      
      <l-marker
        v-if="snappedEnd && showDirections"
        :lat-lng="[snappedEnd.lat, snappedEnd.lon]"
      >
        <l-icon
          :icon-size="[20, 20]"
          :icon-anchor="[10, 10]"
        >
          <div class="snapped-marker end">
            🔴
          </div>
        </l-icon>
        <l-popup>
          <strong>Final traseu</strong><br>
          <small>Punct pe stradă</small>
        </l-popup>
      </l-marker>

      <!-- Markere pentru stații selectate (când e ales un traseu) - DOAR ICOANE SIMPLE -->
      <template v-if="stations && stations.length > 0">
        <l-marker
          v-for="station in stations"
          :key="`route-${station.id}`"
          :lat-lng="[station.latitude, station.longitude]"
        >
          <l-icon
            :icon-size="[32, 32]"
            :icon-anchor="[16, 32]"
            icon-url="/bus-station.png"
          />
        </l-marker>
      </template>

      <!-- Linia traseului cu săgeți pentru sens (dacă există stații selectate) -->
      <l-polyline
        v-if="routePath.length > 0 && !showMultimodal"
        ref="routePolylineRef"
        :lat-lngs="routePath"
        :color="routeColor"
        :weight="5"
        :opacity="0.7"
      />
      
      <!-- Linia pentru segmentul de autobuz în modul multimodal (doar între stațiile de urcare și coborâre) -->
      <l-polyline
        v-if="multimodalBusPath.length > 0 && showMultimodal"
        :lat-lngs="multimodalBusPath"
        :color="multimodalData.busColor"
        :weight="5"
        :opacity="0.7"
      />
      
      <!-- Linia punctată pentru traseu de mers pe jos (la stația de urcare) -->
      <l-polyline
        v-if="walkingPath.length > 0"
        :lat-lngs="walkingPath"
        color="#10b981"
        :weight="4"
        :opacity="0.8"
        dashArray="10, 10"
      />
      
      <!-- Linia punctată pentru al doilea traseu de mers pe jos (de la stația de coborâre la destinație) -->
      <l-polyline
        v-if="secondWalkingPath.length > 0"
        :lat-lngs="secondWalkingPath"
        color="#f59e0b"
        :weight="4"
        :opacity="0.8"
        dashArray="10, 10"
      />

      <!-- Markere pentru autobuze LIVE - ascunse când e afișată o rută -->
      <l-marker
        v-if="!showMultimodal && !showTransfer"
        v-for="bus in liveBuses"
        :key="bus.id"
        :lat-lng="[bus.latitude, bus.longitude]"
      >
        <l-icon
          :icon-size="[20, 20]"
          :icon-anchor="[10, 10]"
          icon-url="/front-of-bus.png"
        />
        <l-popup>
          <div class="bus-popup">
            <strong :style="{ color: getBusColor(bus.routeId) }">Autobuz {{ bus.id }}</strong><br>
            <small>Traseu: {{ bus.routeId }}</small><br>
            <small>Viteză: {{ bus.speed?.toFixed(1) }} km/h</small><br>
            <div class="occupancy-indicator" :class="getOccupancyClass(bus.occupancy)">
              <span class="occupancy-icon">👥</span>
              <span class="occupancy-text">{{ getOccupancyLabel(bus.occupancy) }}</span>
              <div class="occupancy-bar">
                <div class="occupancy-fill" :style="{ width: bus.occupancy + '%' }"></div>
              </div>
            </div>
          </div>
        </l-popup>
      </l-marker>

    </l-map>

  </div>

</template>



<script setup lang="ts">

import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { useDatabaseObject } from 'vuefire'
import { ref as dbRef, getDatabase } from 'firebase/database'

import { LMap, LTileLayer, LMarker, LPopup, LPolyline, LIcon } from '@vue-leaflet/vue-leaflet'

import L from 'leaflet'

import 'leaflet-polylinedecorator'

import LocationButton from './LocationButton.vue'
import EnhancedSearch from './EnhancedSearch.vue'
import WalkingDirections from './WalkingDirections.vue'
import MultimodalDirections from './MultimodalDirections.vue'
import TransferRoute from './TransferRoute.vue'
import NearbyStationsPanel from './NearbyStationsPanel.vue'
import apiService, { type Station } from '@/services/apiService'
import { useNotifications, checkBusNotifications } from '@/composables/useNotifications'

// Notifications
const { 
  enableNotifications, 
  disableNotifications, 
  getNotificationSettings,
  checkNotificationSupport
} = useNotifications()

// Run diagnostics on mount
onMounted(() => {
  const diagnostics = checkNotificationSupport()
  if (!diagnostics.supported) {
    console.warn('⚠️ Notificările nu sunt suportate de acest browser')
  }
})

// Am scos 'leaflet/dist/leaflet.css' de aici, deoarece este deja în main.ts

// Interface pentru datele autobuzelor live
interface BusLocation {
  id: string
  latitude: number
  longitude: number
  routeId: number
  timestamp: number
  speed: number
  heading: number
  occupancy?: number // Grad de ocupare 0-100
}



// Props

interface Props {

  stations?: Station[]
  
  allStations?: Station[]

  routeColor?: string

}



const props = withDefaults(defineProps<Props>(), {

  stations: () => [],
  
  allStations: () => [],

  routeColor: '#2563eb' // Albastru

})

// Emit events
const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  sidebarToggle: [visible: boolean]
}>()

// State pentru a verifica dacă componenta este gata

const isReady = ref(false)

// State pentru traseul calculat pe străzi

const routePath = ref<[number, number][]>([])

const isLoadingRoute = ref(false)

// Ref pentru polyline pentru a adăuga săgeți

const routePolylineRef = ref<any>(null)

const map = ref<any>(null)

// State pentru locația utilizatorului
const userLocation = ref<{lat: number, lon: number} | null>(null)

// State pentru adresa selectată
const selectedAddress = ref<{lat: number, lon: number, name: string} | null>(null)

// State pentru direcții de mers pe jos (primul traseu: de la user la stația de urcare)
const showDirections = ref(false)
const walkingStart = ref<{lat: number, lon: number, name: string} | null>(null)
const walkingEnd = ref<{lat: number, lon: number, name: string} | null>(null)
const walkingPath = ref<[number, number][]>([])
const snappedStart = ref<{lat: number, lon: number} | null>(null)
const snappedEnd = ref<{lat: number, lon: number} | null>(null)

// State pentru al doilea traseu de mers pe jos (de la stația de coborâre la destinație)
const showSecondWalking = ref(false)
const secondWalkingStart = ref<{lat: number, lon: number, name: string} | null>(null)
const secondWalkingEnd = ref<{lat: number, lon: number, name: string} | null>(null)
const secondWalkingPath = ref<[number, number][]>([])

// State pentru segmentul de traseu multimodal (doar între stațiile de urcare și coborâre)
const multimodalBusPath = ref<[number, number][]>([])

// State pentru afișarea panelului cu stații apropiate
const showNearbyStations = ref(false)

// State pentru afișarea/ascunderea sidebar-ului
const showSidebar = ref(true)
const tripMode = ref(false)

// State pentru panoul multimodal
const showMultimodal = ref(false)
const multimodalData = ref({
  startLocation: '',
  endLocation: '',
  boardingStation: '',
  alightingStation: '',
  busLine: '',
  busColor: '#3b82f6',
  busStationsList: [] as string[],
  firstWalkDistance: 0,
  firstWalkTime: 0,
  secondWalkDistance: 0,
  secondWalkTime: 0,
  busTime: 0
})

// State pentru panoul de traseu cu transfer
const showTransfer = ref(false)
const transferData = ref({
  startName: '',
  endName: '',
  boardingStation: null as Station | null,
  transferStation: null as Station | null,
  alightingStation: null as Station | null,
  route1Number: '',
  route1Color: '#3b82f6',
  route1StationsCount: 0,
  route2Number: '',
  route2Color: '#3b82f6',
  route2StationsCount: 0,
  firstWalkDistance: 0,
  firstWalkTime: 0,
  busTime1: 0,
  busTime2: 0,
  secondWalkDistance: 0,
  secondWalkTime: 0
})

// Cache pentru stații apropiate
let nearbyStationsCache: any[] = []
let nearbyStationsCacheKey = ''

// Computed pentru a afișa cele mai apropiate 10 stații când utilizatorul își găsește locația
// Optimizat cu cache
const nearbyStations = computed(() => {
  // Doar dacă utilizatorul a activat afișarea și nu e selectat un traseu
  if (!showNearbyStations.value || (props.stations && props.stations.length > 0)) {
    return []
  }
  
  // Trebuie să avem locația utilizatorului
  if (!userLocation.value?.lat || !userLocation.value?.lon) {
    return []
  }
  
  // Cache key bazat pe locația utilizatorului (rotunjit la 4 decimale)
  const cacheKey = `${userLocation.value.lat.toFixed(4)}-${userLocation.value.lon.toFixed(4)}`
  if (cacheKey === nearbyStationsCacheKey && nearbyStationsCache.length > 0) {
    return nearbyStationsCache
  }
  
  const userLat = userLocation.value.lat
  const userLon = userLocation.value.lon
  const allStations = props.allStations || []
  
  // Calculare optimizată - evităm map și folosim for loop
  const stationsWithDistance: Array<typeof allStations[0] & { distance: number }> = []
  
  for (let i = 0; i < allStations.length; i++) {
    const station = allStations[i]
    if (!station || typeof station.latitude !== 'number' || typeof station.longitude !== 'number') {
      continue
    }
    const distance = calculateDistance(userLat, userLon, station.latitude, station.longitude)
    stationsWithDistance.push({ ...station, distance })
  }
  
  // Sortăm și luăm primele 10
  stationsWithDistance.sort((a, b) => a.distance - b.distance)
  const result = stationsWithDistance.slice(0, 10)
  
  // Salvăm în cache
  nearbyStationsCache = result
  nearbyStationsCacheKey = cacheKey
  
  return result
})

// Firebase - Ascultă autobuze live
const database = getDatabase()
const busLocationsRef = dbRef(database, 'bus_locations')
const busLocationsData = useDatabaseObject(busLocationsRef)

// Cache pentru distanțe calculate recent
const distanceCache = new Map<string, { distance: number, timestamp: number }>()
const DISTANCE_CACHE_TTL = 5000 // 5 secunde

// Helper pentru calcularea distanței cu cache
const getCachedDistance = (busId: string, busLat: number, busLon: number, userLat: number, userLon: number): number => {
  const cacheKey = `${busId}-${userLat.toFixed(4)}-${userLon.toFixed(4)}`
  const cached = distanceCache.get(cacheKey)
  
  if (cached && Date.now() - cached.timestamp < DISTANCE_CACHE_TTL) {
    return cached.distance
  }
  
  const distance = calculateDistance(userLat, userLon, busLat, busLon)
  distanceCache.set(cacheKey, { distance, timestamp: Date.now() })
  
  // Curăță cache-ul vechi
  if (distanceCache.size > 200) {
    const now = Date.now()
    for (const [key, value] of distanceCache.entries()) {
      if (now - value.timestamp > DISTANCE_CACHE_TTL) {
        distanceCache.delete(key)
      }
    }
  }
  
  return distance
}

// Computed pentru a transforma datele Firebase în array și filtra cele mai apropiate autobuze
// Optimizat cu cache și limitare inteligentă
const liveBuses = computed(() => {
  if (!busLocationsData.value) {
    return []
  }
  
  const buses: BusLocation[] = []
  const entries = Object.entries(busLocationsData.value)
  
  // Procesare optimizată - evită spread operator
  for (let i = 0; i < entries.length; i++) {
    const [id, data] = entries[i] as [string, any]
    if (data?.latitude && data?.longitude) {
      buses.push({
        id,
        latitude: data.latitude,
        longitude: data.longitude,
        routeId: data.routeId,
        timestamp: data.timestamp,
        speed: data.speed,
        heading: data.heading
      })
    }
  }
  
  // Dacă nu avem locația utilizatorului, returnăm primele 50 (mai eficient decât toate)
  if (!userLocation.value?.lat || !userLocation.value?.lon) {
    return buses.slice(0, 50)
  }
  
  const userLat = userLocation.value.lat
  const userLon = userLocation.value.lon
  
  // Folosim un radius pentru filtrare rapidă (30km)
  const MAX_DISTANCE = 30
  const busesNearby: (BusLocation & { distance: number })[] = []
  
  for (let i = 0; i < buses.length; i++) {
    const bus = buses[i]
    if (!bus || typeof bus.latitude !== 'number' || typeof bus.longitude !== 'number') {
      continue
    }
    const distance = getCachedDistance(bus.id, bus.latitude, bus.longitude, userLat, userLon)
    
    if (distance <= MAX_DISTANCE) {
      busesNearby.push({ ...bus, distance })
    }
  }
  
  // Sortare eficientă - top 50 cele mai apropiate
  busesNearby.sort((a, b) => a.distance - b.distance)
  return busesNearby.slice(0, 50)
})

// Funcție pentru calcularea distanței dintre două puncte GPS (Haversine formula)
const calculateDistance = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371 // Raza Pământului în km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a = 
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c // Distanța în km
}

// Centrul hărții: Sibiu (Piața Mare)

const center = ref<[number, number]>([45.7970, 24.1523])

const zoom = ref(13)

// Mapare culori pentru fiecare traseu (încărcat dinamic din API)
const routeColors = ref<Record<number, string>>({})

// Încarcă culorile traseelor la montare cu cache în localStorage
onMounted(async () => {
  delete (L.Icon.Default.prototype as any)._getIconUrl
  L.Icon.Default.mergeOptions({
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  })
  
  // Încarcă culorile traseelor din cache sau API
  const loadRouteColors = async () => {
    try {
      // Încearcă să încărci din localStorage
      const cachedColors = localStorage.getItem('routeColors')
      const cachedTimestamp = localStorage.getItem('routeColorsTimestamp')
      
      // Cache valid 24 ore
      if (cachedColors && cachedTimestamp && Date.now() - parseInt(cachedTimestamp) < 86400000) {
        routeColors.value = JSON.parse(cachedColors)
        console.log('✅ Loaded', Object.keys(routeColors.value).length, 'route colors from cache')
        return
      }
      
      // Încarcă din API
      const routes = await apiService.getRoutes()
      const colors: Record<number, string> = {}
      
      for (let i = 0; i < routes.length; i++) {
        const route = routes[i]
        if (route && route.id && (route as any).color) {
          colors[route.id] = (route as any).color
        }
      }
      
      routeColors.value = colors
      
      // Salvează în cache
      localStorage.setItem('routeColors', JSON.stringify(colors))
      localStorage.setItem('routeColorsTimestamp', Date.now().toString())
      
      console.log('✅ Loaded', Object.keys(colors).length, 'route colors from API')
    } catch (error) {
      console.error('❌ Failed to load route colors:', error)
    }
  }
  
  // Încarcă culorile async
  loadRouteColors()
  
  // Hartă ready imediat
  setTimeout(() => {
    isReady.value = true
  }, 50)
})

// Watch pentru monitorizarea autobuzelor și trimiterea notificărilor
// Optimizat cu debounce și cache pentru stația monitorizată
let notificationCheckTimeout: number | null = null
let cachedMonitoredStation: Station | null = null
let cachedMonitoredStationId: number | null = null

watch(liveBuses, (buses) => {
  if (notificationCheckTimeout) {
    clearTimeout(notificationCheckTimeout)
  }
  
  notificationCheckTimeout = setTimeout(() => {
    const notificationSettings = getNotificationSettings()
    
    if (!notificationSettings.enabled || !notificationSettings.stationId) {
      cachedMonitoredStation = null
      cachedMonitoredStationId = null
      return
    }
    
    // Cache pentru stația monitorizată
    if (cachedMonitoredStationId !== notificationSettings.stationId) {
      cachedMonitoredStation = props.allStations?.find(
        s => s.id === notificationSettings.stationId
      ) || null
      cachedMonitoredStationId = notificationSettings.stationId
    }
    
    if (cachedMonitoredStation && buses.length > 0) {
      // Conversie eficientă - reutilizăm obiectele dacă sunt deja în format corect
      const busLocations = buses.map(bus => ({
        id: bus.id,
        latitude: bus.latitude,
        longitude: bus.longitude,
        routeId: bus.routeId,
        speed: bus.speed,
        occupancy: (bus as any).occupancy
      }))
      
      checkBusNotifications(busLocations, cachedMonitoredStation)
    }
  }, 3000) // Verifică la 3 secunde pentru a reduce load-ul
}, { deep: false })

// Funcție pentru a obține culoarea unui autobuz în funcție de routeId
const getBusColor = (routeId: number): string => {
  return routeColors.value[routeId] || '#2563eb'
}



const mapOptions = {

  zoomControl: true,

  attributionControl: true

}



const attribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'



// Funcție pentru a calcula traseul pe străzi folosind GTFS shapes
const calculateStreetRoute = async (stations: Station[]) => {
  if (stations.length < 2) {
    routePath.value = []
    return
  }

  isLoadingRoute.value = true
  
  try {
    // Obține primul station pentru a determina route ID-ul
    const firstStation = stations[0]
    
    // Găsește route ID-ul curent (din props sau din context)
    const currentRouteId = props.stations[0]?.id ? await findRouteIdForStations(stations) : null
    
    if (currentRouteId) {
      // Folosește GTFS shapes pentru traseu exact
      const shapeData = await apiService.getRouteShape(currentRouteId)
      
      if (shapeData && shapeData.points && shapeData.points.length > 0) {
        routePath.value = shapeData.points.map(point => 
          [point.latitude, point.longitude] as [number, number]
        )
        console.log('✅ Traseu GTFS încărcat:', routePath.value.length, 'puncte')
        isLoadingRoute.value = false
        return
      }
    }
    
    // Fallback: OSRM API
    console.log('⚠️ Nu s-a găsit GTFS shape, folosim OSRM fallback')
    const coordinates = stations
      .map(s => `${s.longitude},${s.latitude}`)
      .join(';')
    
    const url = `https://router.project-osrm.org/route/v1/driving/${coordinates}?overview=full&geometries=geojson`
    
    const response = await fetch(url)
    const data = await response.json()
    
    if (data.code === 'Ok' && data.routes && data.routes.length > 0) {
      const geometry = data.routes[0].geometry.coordinates
      routePath.value = geometry.map((coord: number[]) => [coord[1], coord[0]] as [number, number])
      console.log('✅ Traseu OSRM calculat:', routePath.value.length, 'puncte')
    } else {
      console.error('❌ OSRM API error:', data)
      routePath.value = stations.map(s => [s.latitude, s.longitude] as [number, number])
    }
  } catch (error) {
    console.error('❌ Eroare la calcularea traseului:', error)
    routePath.value = stations.map(s => [s.latitude, s.longitude] as [number, number])
  } finally {
    isLoadingRoute.value = false
  }
}

// Helper function pentru a găsi route ID
const findRouteIdForStations = async (stations: Station[]): Promise<number | null> => {
  // Această funcție ar trebui să caute în toate traseele pentru a găsi care conține aceste stații
  // Pentru simplitate, returnăm null și lăsăm fallback-ul OSRM
  // Într-o implementare completă, ar trebui să verifici fiecare traseu
  return null
}



// Watch pentru modificări ale stațiilor - recalculează traseul

watch(() => props.stations, (newStations) => {

  if (newStations && newStations.length > 0) {

    calculateStreetRoute(newStations)

  } else {

    routePath.value = []

  }

}, { immediate: true })



// Watch pentru a adăuga săgeți când traseul se schimbă

watch(routePath, async () => {

  // Așteaptă ca polyline-ul să fie randat

  await new Promise(resolve => setTimeout(resolve, 100))

  

  if (routePolylineRef.value && routePath.value.length > 0) {

    const polylineInstance = routePolylineRef.value.leafletObject

    

    if (polylineInstance) {

      // Șterge decorațiile vechi dacă există

      if ((polylineInstance as any)._decorators) {

        (polylineInstance as any)._decorators.forEach((d: any) => d.remove())

      }

      

      // Adaugă săgeți pe traseu pentru a arăta direcția

      const LExtended = L as any

      const arrowSymbol = LExtended.Symbol.arrowHead({

        pixelSize: 12,

        polygon: false,

        pathOptions: { 

          stroke: true, 

          color: props.routeColor,

          weight: 3,

          opacity: 0.9

        }

      })

      

      const decorator: any = LExtended.polylineDecorator(polylineInstance, {

        patterns: [

          { 

            offset: '10%', 

            repeat: 100, // O săgeată la fiecare 100 pixeli

            symbol: arrowSymbol 

          }

        ]

      })

       if (map.value?.leafletObject) {

        decorator.addTo(map.value.leafletObject)

      }

      // Salvează decorator-ul pentru ștergere ulterioară

      (polylineInstance as any)._decorators = [decorator]

      

      // Adaugă decorator-ul pe hartă

     

    }

  }

})



// Metodă pentru a centra harta pe o anumită locație

const centerMap = (lat: number, lon: number, newZoom: number = 15) => {

  center.value = [lat, lon]

  zoom.value = newZoom

}

// Handler pentru locația găsită de LocationButton
const handleLocationFound = (lat: number, lon: number) => {
  userLocation.value = { lat, lon }
  centerMap(lat, lon, 15)
  // Activează automat panelul cu stații apropiate
  showNearbyStations.value = true
  console.log('✅ Locație găsită, deschid panelul cu 10 stații apropiate')
}

// Handler pentru stația selectată din EnhancedSearch
const handleStationSelected = (station: Station) => {
  centerMap(station.latitude, station.longitude, 16)
}

// Handler pentru adresa selectată
const handleAddressSelected = (location: { lat: number; lon: number; name: string }) => {
  selectedAddress.value = location
  centerMap(location.lat, location.lon, 15)
}

// Handler pentru cerere direcții de mers pe jos
const handleWalkingDirectionsRequested = (
  start: { lat: number; lon: number; name: string },
  end: Station
) => {
  walkingStart.value = start
  walkingEnd.value = {
    lat: end.latitude,
    lon: end.longitude,
    name: end.name
  }
  showDirections.value = true
  
  // Centrează harta să includă ambele puncte
  const bounds = L.latLngBounds(
    [start.lat, start.lon],
    [end.latitude, end.longitude]
  )
  
  if (map.value?.leafletObject) {
    map.value.leafletObject.fitBounds(bounds, { padding: [50, 50] })
  }
}

// Handler când traseul de mers pe jos este calculat
const handleWalkingRouteCalculated = (geometry: [number, number][]) => {
  walkingPath.value = geometry
}

// Handler pentru coordonatele snapped
const handleSnappedCoordinates = (
  start: {lat: number, lon: number},
  end: {lat: number, lon: number}
) => {
  snappedStart.value = start
  snappedEnd.value = end
  console.log('📌 Coordonate snapped primite:', { start, end })
}

// Calculează distanța Haversine
const getDistance = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371 // km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a = 
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}

// Găsește cea mai apropiată stație de un punct
const findNearestStation = (lat: number, lon: number, stations: typeof props.allStations): typeof props.allStations[0] | null => {
  if (!stations || stations.length === 0) return null
  
  let nearest = stations[0]
  if (!nearest) return null
  
  let minDistance = getDistance(lat, lon, nearest.latitude, nearest.longitude)
  
  for (const station of stations) {
    const distance = getDistance(lat, lon, station.latitude, station.longitude)
    if (distance < minDistance) {
      minDistance = distance
      nearest = station
    }
  }
  
  return nearest
}

// Calculează ambele trasee de mers pe jos (la stația de urcare și de la stația de coborâre)
const calculateBothWalkingRoutes = async (
  userLoc: { lat: number; lon: number },
  startStation: typeof props.allStations[0],
  endStation: typeof props.allStations[0],
  destination: { lat: number; lon: number; name: string }
) => {
  try {
    // Primul traseu: de la user la stația de urcare
    const route1Url = `https://router.project-osrm.org/route/v1/foot/${userLoc.lon},${userLoc.lat};${startStation.longitude},${startStation.latitude}?overview=full&geometries=geojson`
    const response1 = await fetch(route1Url)
    const data1 = await response1.json()
    
    let firstDistance = 0
    let firstTime = 0
    
    if (data1.code === 'Ok' && data1.routes && data1.routes.length > 0) {
      const geometry1 = data1.routes[0].geometry.coordinates.map((coord: number[]) => 
        [coord[1], coord[0]] as [number, number]
      )
      walkingPath.value = geometry1
      firstDistance = data1.routes[0].distance
      firstTime = Math.ceil(data1.routes[0].duration / 60)
      console.log('✅ Primul traseu calculat (user → stație urcare)')
    }
    
    // Al doilea traseu: de la stația de coborâre la destinație
    const route2Url = `https://router.project-osrm.org/route/v1/foot/${endStation.longitude},${endStation.latitude};${destination.lon},${destination.lat}?overview=full&geometries=geojson`
    const response2 = await fetch(route2Url)
    const data2 = await response2.json()
    
    let secondDistance = 0
    let secondTime = 0
    
    if (data2.code === 'Ok' && data2.routes && data2.routes.length > 0) {
      const geometry2 = data2.routes[0].geometry.coordinates.map((coord: number[]) => 
        [coord[1], coord[0]] as [number, number]
      )
      secondWalkingPath.value = geometry2
      secondDistance = data2.routes[0].distance
      secondTime = Math.ceil(data2.routes[0].duration / 60)
      console.log('✅ Al doilea traseu calculat (stație coborâre → destinație)')
    }
    
    return {
      firstDistance,
      firstTime,
      secondDistance,
      secondTime
    }
  } catch (error) {
    console.error('❌ Eroare la calcularea traseelor:', error)
    return null
  }
}

// Găsește un traseu de autobuz care conectează două stații
const findConnectingRoute = async (startStation: typeof props.allStations[0], endStation: typeof props.allStations[0]) => {
  try {
    // Obține toate traseele
    const routes = await apiService.getRoutes()
    
    console.log(`🔍 Caut printre ${routes.length} rute...`)
    console.log(`📌 Stație start: "${startStation.name}" (ID: ${startStation.id})`)
    console.log(`📌 Stație end: "${endStation.name}" (ID: ${endStation.id})`)
    
    // Cache pentru stațiile fiecărei rute
    const routeStationsCache: Map<number, any[]> = new Map()
    
    // 1. DIRECT ROUTE: verifică dacă există rută directă
    for (const route of routes) {
      const stations = await apiService.getRouteStations(route.id)
      routeStationsCache.set(route.id, stations)
      
      const startIndex = stations.findIndex(s => s.id === startStation.id)
      const endIndex = stations.findIndex(s => s.id === endStation.id)
      
      if (startIndex !== -1 && endIndex !== -1 && startIndex !== endIndex) {
        console.log(`✅ Rută directă: ${route.routeNumber} (stație ${startIndex} → ${endIndex})`)
        return { type: 'direct', route1: route, stations1: stations }
      }
    }
    
    console.log('⚠️ Nicio rută directă, caut cu transfer...')
    
    // 2. TRANSFER ROUTE: găsim rute cu transfer
    const routesWithStart = routes.filter(r => {
      const stations = routeStationsCache.get(r.id) || []
      return stations.some(s => s.id === startStation.id)
    })
    
    const routesWithEnd = routes.filter(r => {
      const stations = routeStationsCache.get(r.id) || []
      return stations.some(s => s.id === endStation.id)
    })
    
    console.log(`🚏 ${routesWithStart.length} rute cu start: ${routesWithStart.map(r => r.routeNumber).join(', ')}`)
    console.log(`🚏 ${routesWithEnd.length} rute cu end: ${routesWithEnd.map(r => r.routeNumber).join(', ')}`)
    
    // Căutăm stații comune (transfer points)
    for (const route1 of routesWithStart) {
      const stations1 = routeStationsCache.get(route1.id) || []
      
      for (const route2 of routesWithEnd) {
        if (route1.id === route2.id) continue
        
        const stations2 = routeStationsCache.get(route2.id) || []
        
        // Găsim stații comune
        const commonStations = stations1.filter(s1 => 
          stations2.some(s2 => s2.id === s1.id)
        )
        
        if (commonStations.length > 0) {
          const transferStation = commonStations[0]
          console.log(`✅ Transfer: ${route1.routeNumber} → ${transferStation.name} → ${route2.routeNumber}`)
          
          return {
            type: 'transfer',
            route1: route1,
            route2: route2,
            transferStation: transferStation,
            stations1: stations1,
            stations2: stations2
          }
        }
      }
    }
    
    console.log('❌ Nicio rută (nici cu transfer) nu conectează aceste stații')
    return null
  } catch (error) {
    console.error('❌ Eroare la găsirea traseului:', error)
    return null
  }
}

// Handler pentru traseu multimodal (mers pe jos + autobuz + mers pe jos)
const handleMultimodalRouteRequested = async (
  userLoc: { lat: number; lon: number; name?: string },
  destination: { lat: number; lon: number; name: string }
) => {
  console.log('🚀 Calculez traseu multimodal...')
  console.log('📍 De la:', userLoc)
  console.log('📍 La:', destination)
  
  // 1. Găsește stația cea mai apropiată de utilizator
  const startStation = findNearestStation(userLoc.lat, userLoc.lon, props.allStations)
  if (!startStation) {
    console.error('❌ Nu s-a găsit stație apropiată de locația ta')
    return
  }
  
  // 2. Găsește stația cea mai apropiată de destinație
  const endStation = findNearestStation(destination.lat, destination.lon, props.allStations)
  if (!endStation) {
    console.error('❌ Nu s-a găsit stație apropiată de destinație')
    return
  }
  
  console.log(`🚏 Stație start: ${startStation.name}`)
  console.log(`🚏 Stație destinație: ${endStation.name}`)
  
  // 3. Găsește traseul de autobuz care conectează cele două stații
  const busRoute = await findConnectingRoute(startStation, endStation)
  
  if (busRoute) {
    console.log(`🚌 Traseu găsit: ${busRoute.type === 'direct' ? 'Direct' : 'Cu transfer'}`)
    
    if (busRoute.type === 'direct') {
      // DIRECT ROUTE
      const route = busRoute.route1
      const routeStations = busRoute.stations1
      
      console.log(`🚌 Linia ${route.routeNumber} (ID: ${route.id})`)
      console.log(`📍 Traseu ${route.routeNumber} are ${routeStations.length} stații`)
      
      const startIndex = routeStations.findIndex(s => s.id === startStation.id)
      const endIndex = routeStations.findIndex(s => s.id === endStation.id)
      console.log(`📊 Start index: ${startIndex}, End index: ${endIndex}`)
      
      emit('routeSelected', route.id, routeStations)
      
      const routingData = await calculateBothWalkingRoutes(userLoc, startStation, endStation, destination)
      
      if (routingData) {
        const stationsBetween = Math.abs(endIndex - startIndex)
        const relevantStations = startIndex < endIndex 
          ? routeStations.slice(startIndex, endIndex + 1)
          : routeStations.slice(endIndex, startIndex + 1).reverse()
        
        try {
          const shapeData = await apiService.getRouteSegment(route.id, startStation.id, endStation.id)
          if (shapeData && shapeData.points && shapeData.points.length > 0) {
            multimodalBusPath.value = shapeData.points.map(point => 
              [point.latitude, point.longitude] as [number, number]
            )
            console.log('✅ GTFS segment încărcat:', multimodalBusPath.value.length, 'puncte')
          } else {
            await calculateStreetRoute(relevantStations)
            multimodalBusPath.value = routePath.value
          }
        } catch (error) {
          console.error('❌ Eroare GTFS, folosim OSRM:', error)
          await calculateStreetRoute(relevantStations)
          multimodalBusPath.value = routePath.value
        }
        
        multimodalData.value = {
          startLocation: 'Locația ta',
          endLocation: destination.name,
          boardingStation: startStation.name,
          alightingStation: endStation.name,
          busLine: route.routeNumber,
          busColor: routeColors.value[route.id] || '#3b82f6',
          busStationsList: relevantStations.map(s => s.name),
          firstWalkDistance: routingData.firstDistance,
          firstWalkTime: routingData.firstTime,
          secondWalkDistance: routingData.secondDistance,
          secondWalkTime: routingData.secondTime,
          busTime: Math.max(stationsBetween * 2, 2)
        }
        
        showMultimodal.value = true
        showDirections.value = false
      }
    } else if (busRoute.type === 'transfer') {
      // TRANSFER ROUTE
      const route1 = busRoute.route1
      const route2 = busRoute.route2
      const transferStation = busRoute.transferStation
      const stations1 = busRoute.stations1
      const stations2 = busRoute.stations2
      
      if (!route2 || !transferStation || !stations2) {
        console.error('❌ Date incomplete pentru traseu cu transfer')
        return
      }
      
      console.log(`🔄 Transfer: ${route1.routeNumber} → ${transferStation.name} → ${route2.routeNumber}`)
      
      // Calculăm rutele de mers pe jos (start → prima stație, ultima stație → destinație)
      const routingData = await calculateBothWalkingRoutes(userLoc, startStation, endStation, destination)
      
      if (routingData) {
        // Calculăm numărul de stații pentru fiecare segment
        const startIndex1 = stations1.findIndex(s => s.id === startStation.id)
        const transferIndex1 = stations1.findIndex(s => s.id === transferStation.id)
        const route1StationsCount = Math.abs(transferIndex1 - startIndex1)
        
        const transferIndex2 = stations2.findIndex(s => s.id === transferStation.id)
        const endIndex2 = stations2.findIndex(s => s.id === endStation.id)
        const route2StationsCount = Math.abs(endIndex2 - transferIndex2)
        
        console.log(`📊 Segment 1: ${route1StationsCount} stații, Segment 2: ${route2StationsCount} stații`)
        
        // Calculăm și afișăm traseele de autobuz pe hartă
        try {
          // Primul segment de autobuz (start → transfer)
          const segment1 = await apiService.getRouteSegment(route1.id, startStation.id, transferStation.id)
          if (segment1 && segment1.points && segment1.points.length > 0) {
            walkingPath.value = segment1.points.map(point => 
              [point.latitude, point.longitude] as [number, number]
            )
            console.log('✅ Segment 1 GTFS încărcat')
          }
          
          // Al doilea segment de autobuz (transfer → end)
          const segment2 = await apiService.getRouteSegment(route2.id, transferStation.id, endStation.id)
          if (segment2 && segment2.points && segment2.points.length > 0) {
            secondWalkingPath.value = segment2.points.map(point => 
              [point.latitude, point.longitude] as [number, number]
            )
            console.log('✅ Segment 2 GTFS încărcat')
          }
        } catch (error) {
          console.error('❌ Eroare la încărcarea segmentelor GTFS:', error)
        }
        
        // Populăm datele pentru panoul de transfer
        transferData.value = {
          startName: userLoc.name || 'Locația ta',
          endName: destination.name || 'Destinația',
          boardingStation: startStation,
          transferStation: transferStation,
          alightingStation: endStation,
          route1Number: route1.routeNumber,
          route1Color: routeColors.value[route1.id] || '#3b82f6',
          route1StationsCount: route1StationsCount,
          route2Number: route2.routeNumber,
          route2Color: routeColors.value[route2.id] || '#10b981',
          route2StationsCount: route2StationsCount,
          firstWalkDistance: routingData.firstDistance,
          firstWalkTime: routingData.firstTime,
          busTime1: calculateBusTime(route1StationsCount),
          busTime2: calculateBusTime(route2StationsCount),
          secondWalkDistance: routingData.secondDistance,
          secondWalkTime: routingData.secondTime
        }
        
        console.log('✅ Deschid panoul de transfer cu datele:', transferData.value)
        
        // Afișăm panoul
        showTransfer.value = true
        showMultimodal.value = false
        showDirections.value = false
      }
    }
    
    // Centrează harta
    const bounds = L.latLngBounds(
      [userLoc.lat, userLoc.lon],
      [startStation.latitude, startStation.longitude]
    )
    bounds.extend([endStation.latitude, endStation.longitude])
    bounds.extend([destination.lat, destination.lon])
    
    if (map.value?.leafletObject) {
      map.value.leafletObject.fitBounds(bounds, { padding: [100, 100] })
    }
  } else {
    console.error('❌ Nu s-a găsit traseu de autobuz între stații')
    alert('❌ Nu există niciun autobuz care să ducă la această destinație.\n\nÎncearcă să selectezi o destinație mai apropiată de rețeaua de transport public.')
  }
}

// Închide panoul de direcții
const closeDirections = () => {
  showDirections.value = false
  walkingPath.value = []
  snappedStart.value = null
  snappedEnd.value = null
  walkingStart.value = null
  walkingEnd.value = null
  selectedAddress.value = null
  console.log('✅ Direcții închise - totul resetat')
}

// Închide panoul multimodal
const closeMultimodal = () => {
  showMultimodal.value = false
  walkingPath.value = []
  secondWalkingPath.value = []
  multimodalBusPath.value = []
  routePath.value = []
  walkingStart.value = null
  walkingEnd.value = null
  secondWalkingStart.value = null
  secondWalkingEnd.value = null
  snappedStart.value = null
  snappedEnd.value = null
  selectedAddress.value = null
  // Reset multimodal data
  multimodalData.value = {
    startLocation: '',
    endLocation: '',
    boardingStation: '',
    alightingStation: '',
    busLine: '',
    busColor: '#3b82f6',
    busStationsList: [],
    firstWalkDistance: 0,
    firstWalkTime: 0,
    secondWalkDistance: 0,
    secondWalkTime: 0,
    busTime: 0
  }
  console.log('✅ Rută multimodală închisă - totul resetat')
}

// Închide panoul de transfer
const closeTransfer = () => {
  showTransfer.value = false
  walkingPath.value = []
  secondWalkingPath.value = []
  multimodalBusPath.value = []
  routePath.value = []
  walkingStart.value = null
  walkingEnd.value = null
  secondWalkingStart.value = null
  secondWalkingEnd.value = null
  snappedStart.value = null
  snappedEnd.value = null
  selectedAddress.value = null
  // Reset transfer data
  transferData.value = {
    startName: '',
    endName: '',
    boardingStation: null,
    transferStation: null,
    alightingStation: null,
    route1Number: '',
    route1Color: '#3b82f6',
    route1StationsCount: 0,
    route2Number: '',
    route2Color: '#3b82f6',
    route2StationsCount: 0,
    firstWalkDistance: 0,
    firstWalkTime: 0,
    busTime1: 0,
    busTime2: 0,
    secondWalkDistance: 0,
    secondWalkTime: 0
  }
  console.log('✅ Rută cu transfer închisă - totul resetat')
}

// Helper pentru a calcula timpul estimat de autobuz (2 minute per stație)
const calculateBusTime = (stationsCount: number): number => {
  return stationsCount * 2 // 2 minute per stație
}

// Set trip mode
const setTripMode = (enabled: boolean) => {
  tripMode.value = enabled
}

// Handle route search request from EnhancedSearch
const handleRouteSearchRequested = async (
  origin: { lat: number; lon: number; name: string },
  destination: { lat: number; lon: number; name: string }
) => {
  console.log('🚀 Căutare traseu între:', origin.name, '→', destination.name)
  
  // Use multimodal route handler
  await handleMultimodalRouteRequested(
    { lat: origin.lat, lon: origin.lon },
    destination
  )
}

// Expunem metoda pentru a putea fi apelată din componenta părinte
defineExpose({
  centerMap,
  setTripMode
})

// Helper pentru gradul de ocupare
const getOccupancyLabel = (occupancy: number | undefined): string => {
  if (!occupancy) return 'Necunoscut'
  if (occupancy < 30) return 'Scăzut'
  if (occupancy < 70) return 'Mediu'
  return 'Ridicat'
}

const getOccupancyClass = (occupancy: number | undefined): string => {
  if (!occupancy) return 'occupancy-unknown'
  if (occupancy < 30) return 'occupancy-low'
  if (occupancy < 70) return 'occupancy-medium'
  return 'occupancy-high'
}

// Handler pentru notificări
const handleNotificationToggle = async (stationId: number) => {
  try {
    const notificationSettings = getNotificationSettings()
    
    if (notificationSettings.enabled && notificationSettings.stationId === stationId) {
      // Dezactivează notificările
      disableNotifications()
      console.log('🔕 Dezactivare notificări pentru stația', stationId)
      alert('Notificările au fost dezactivate pentru această stație')
    } else {
      // Verifică mai întâi suportul pentru notificări
      if (!('Notification' in window)) {
        alert('⚠️ Browserul tău nu suportă notificări. Încearcă un browser modern (Chrome, Firefox, Edge).')
        return
      }
      
      // Activează notificări
      console.log('🔔 Activare notificări pentru stația', stationId)
      const success = await enableNotifications(stationId)
      
      if (success) {
        alert('✅ Notificările au fost activate! Vei primi o alertă când autobuzul se apropie (la 2 minute).')
      } else {
        alert('❌ Nu s-au putut activa notificările. Verifică dacă ai permis notificările în browser (bifează \"Allow\" în prompt-ul browserului).')
      }
    }
  } catch (error) {
    console.error('❌ Eroare la gestionarea notificărilor:', error)
    alert('❌ Eroare la activarea notificărilor. Asigură-te că ai permis notificările în browser și că folosești HTTPS sau localhost.')
  }
}

// Calcul ETA pentru autobuze care vin către o stație
// Optimizat cu memoization și cache persistent
const stationETACache = new Map<string, Array<{ busId: string, routeNumber: string, eta: string, color: string }>>()
let etaCacheTimeout: number | null = null
const ETA_CACHE_DURATION = 5000 // 5 secunde

const getStationETAs = (stationId: number) => {
  // Verifică cache
  const now = Date.now()
  const cacheKey = `${stationId}-${now - (now % ETA_CACHE_DURATION)}` // Cache bucket de 5s
  
  if (stationETACache.has(cacheKey)) {
    return stationETACache.get(cacheKey)!
  }
  
  const etas: Array<{ busId: string, routeNumber: string, eta: string, color: string }> = []
  
  const allStations = props.allStations
  if (!allStations || allStations.length === 0) return etas
  
  const station = allStations.find(s => s.id === stationId)
  if (!station) return etas
  
  const stationLat = station.latitude
  const stationLon = station.longitude
  const buses = liveBuses.value
  
  // Procesare optimizată - evităm forEach
  for (let i = 0; i < buses.length; i++) {
    const bus = buses[i]
    if (!bus || typeof bus.latitude !== 'number' || typeof bus.longitude !== 'number') {
      continue
    }
    
    const distance = getDistance(bus.latitude, bus.longitude, stationLat, stationLon)
    
    // Dacă autobuzul e la mai puțin de 5km de stație
    if (distance < 5) {
      const avgSpeed = bus.speed || 35 // km/h
      const etaMinutes = Math.round((distance / avgSpeed) * 60)
      
      if (etaMinutes <= 30) { // Afișăm doar dacă e sub 30 min
        etas.push({
          busId: bus.id,
          routeNumber: `Linia ${bus.routeId}`,
          eta: etaMinutes <= 1 ? '<1 min' : `${etaMinutes} min`,
          color: routeColors.value[bus.routeId] || '#2563eb'
        })
      }
    }
  }
  
  // Sortează după ETA
  etas.sort((a, b) => {
    const etaA = a.eta === '<1 min' ? 0 : parseInt(a.eta)
    const etaB = b.eta === '<1 min' ? 0 : parseInt(b.eta)
    return etaA - etaB
  })
  
  // Salvează în cache
  stationETACache.set(cacheKey, etas)
  
  // Curăță cache-ul vechi periodic
  if (stationETACache.size > 20) {
    const oldestKey = stationETACache.keys().next().value
    if (oldestKey) stationETACache.delete(oldestKey)
  }
  
  return etas
}
</script>

<style scoped>
.map-container {
  height: 100vh;
  width: 100%;
  position: relative;
  background: #f8fafc;
}

/* Asigură-te că Leaflet își încarcă corect iconițele */
:deep(.leaflet-container) {
  font-family: 'Inter', system-ui, -apple-system, sans-serif;
  height: 100% !important;
  width: 100% !important;
}

/* Tile layer mai clar */
:deep(.leaflet-tile-pane) {
  filter: brightness(1.05) contrast(0.95);
}

/* Control buttons mai clare */
:deep(.leaflet-control-zoom a) {
  background: white !important;
  color: #1f2937 !important;
  border: 1px solid #e5e7eb !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1) !important;
  transition: all 0.2s;
}

:deep(.leaflet-control-zoom a:hover) {
  background: #f9fafb !important;
  border-color: #3b82f6 !important;
}

/* Stiluri pentru markerul de autobuz */
.bus-marker {
  font-size: 28px;
  text-align: center;
  line-height: 32px;
  filter: drop-shadow(0 4px 6px rgba(0, 0, 0, 0.2));
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.15);
  }
}

.bus-popup {
  text-align: center;
  min-width: 140px;
  padding: 4px;
}

.bus-popup strong {
  color: #2563eb;
  font-size: 16px;
  font-weight: 600;
}

.bus-popup small {
  color: #6b7280;
  font-size: 13px;
}

/* Stiluri pentru markerul utilizatorului */
.user-marker {
  font-size: 24px;
  text-align: center;
  line-height: 24px;
  filter: drop-shadow(0 4px 8px rgba(59, 130, 246, 0.5));
  animation: bounce 2s infinite;
}

@keyframes bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-8px);
  }
}

/* Stiluri pentru markerul adresei */
.address-marker {
  font-size: 32px;
  text-align: center;
  line-height: 32px;
  filter: drop-shadow(0 4px 8px rgba(239, 68, 68, 0.4));
}

/* Stiluri pentru toate stațiile (când nu e selectat traseu) */
.all-station-marker {
  font-size: 18px;
  text-align: center;
  line-height: 20px;
  opacity: 0.8;
  transition: all 0.3s;
  filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.2));
}

.all-station-marker:hover {
  opacity: 1;
  transform: scale(1.2);
}

/* Stiluri pentru markerele snapped (puncte pe stradă) */
.snapped-marker {
  font-size: 20px;
  text-align: center;
  line-height: 20px;
  filter: drop-shadow(0 3px 6px rgba(0, 0, 0, 0.3));
  animation: pulse-snapped 2s infinite;
}

@keyframes pulse-snapped {
  0%, 100% {
    transform: scale(1);
    opacity: 1;
  }
  50% {
    transform: scale(1.3);
    opacity: 0.7;
  }
}

.snapped-marker.start {
  filter: drop-shadow(0 3px 8px rgba(34, 197, 94, 0.7));
}

.snapped-marker.end {
  filter: drop-shadow(0 3px 8px rgba(239, 68, 68, 0.7));
}

/* Stiluri pentru toggle-ul stațiilor */
/* Stiluri pentru butonul de toggle sidebar */
.sidebar-toggle-btn {
  position: absolute;
  top: 16px;
  left: 420px;
  z-index: 600;
  background: white;
  border: none;
  border-radius: 8px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  transition: all 0.2s;
}

.sidebar-toggle-btn:hover {
  background: #f3f4f6;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.sidebar-toggle-btn svg {
  color: #374151;
}

/* Stiluri pentru gradul de ocupare */
.occupancy-indicator {
  margin-top: 8px;
  padding: 6px 8px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.occupancy-low {
  background: #d1fae5;
  color: #065f46;
}

.occupancy-medium {
  background: #fef3c7;
  color: #92400e;
}

.occupancy-high {
  background: #fee2e2;
  color: #991b1b;
}

.occupancy-unknown {
  background: #f3f4f6;
  color: #6b7280;
}

.occupancy-icon {
  font-size: 14px;
}

.occupancy-text {
  flex: 1;
}

.occupancy-bar {
  width: 100%;
  height: 4px;
  background: rgba(0, 0, 0, 0.1);
  border-radius: 2px;
  overflow: hidden;
}

.occupancy-fill {
  height: 100%;
  background: currentColor;
  transition: width 0.3s ease;
}

/* Stiluri pentru ETA în stații */
:deep(.station-popup) {
  min-width: 200px;
}

:deep(.station-etas) {
  margin-top: 12px;
  padding-top: 8px;
  border-top: 1px solid #e5e7eb;
}

:deep(.eta-title) {
  font-size: 11px;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 6px;
}

:deep(.eta-item) {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 0;
  font-size: 13px;
}

:deep(.eta-bus) {
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 4px;
}

:deep(.eta-time) {
  font-weight: 700;
  color: #059669;
  background: rgba(5, 150, 105, 0.1);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
}

:deep(.notification-btn) {
  width: 100%;
  margin-top: 10px;
  padding: 10px 14px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
  border: none !important;
  border-radius: 8px;
  color: white !important;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.4);
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
}

:deep(.notification-btn:hover) {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.6);
  background: linear-gradient(135deg, #764ba2 0%, #667eea 100%) !important;
}

:deep(.notification-btn.active) {
  background: linear-gradient(135deg, #10b981 0%, #059669 100%) !important;
  box-shadow: 0 2px 8px rgba(16, 185, 129, 0.4);
}

:deep(.notification-btn.active:hover) {
  background: linear-gradient(135deg, #059669 0%, #10b981 100%) !important;
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.6);
}
</style>