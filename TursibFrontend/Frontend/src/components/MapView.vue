<template>

  <div class="map-container">
    <!-- Buton pentru ascunderea/afișarea sidebar-ului -->
    <button 
      @click="showSidebar = !showSidebar" 
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
      @station-selected="handleStationSelected"
      @address-selected="handleAddressSelected"
      @walking-directions-requested="handleWalkingDirectionsRequested"
      @multimodal-route-requested="handleMultimodalRouteRequested"
    />
    
    <!-- Buton pentru locație -->
    <LocationButton @location-found="handleLocationFound" />
    
    <!-- Toggle pentru afișarea stațiilor -->
    <div class="stations-toggle">
      <label class="toggle-label">
        <input 
          type="checkbox" 
          v-model="showAllStations" 
          class="toggle-checkbox"
        />
        <span class="toggle-text">Arată toate stațiile</span>
      </label>
    </div>
    
    <!-- Panoul multimodal (mers pe jos + autobuz + mers pe jos) -->
    <MultimodalDirections
      v-if="showMultimodal"
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
    
    <!-- Panoul de direcții de mers pe jos (pentru căutări simple) -->
    <WalkingDirections
      v-if="!showMultimodal"
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

      <!-- Markere pentru stații selectate (când e ales un traseu) -->

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

        <l-popup>

          <div>

            <strong>{{ station.name }}</strong>

          </div>

        </l-popup>

      </l-marker>
      
      <!-- Markere pentru TOATE stațiile (când nu e selectat traseu și toggle e activ) -->

      <l-marker

        v-if="showAllStations"

        v-for="station in displayAllStations"

        :key="`all-${station.id}`"

        :lat-lng="[station.latitude, station.longitude]"

      >
        <l-icon
          :icon-size="[26, 26]"
          :icon-anchor="[13, 26]"
          icon-url="/bus-station.png"
        />

        <l-popup>

          <div>

            <strong>{{ station.name }}</strong>

          </div>

        </l-popup>

      </l-marker>



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

      <!-- Markere pentru autobuze LIVE -->
      <l-marker
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
            <small>Viteză: {{ bus.speed?.toFixed(1) }} km/h</small>
          </div>
        </l-popup>
      </l-marker>

    </l-map>

  </div>

</template>



<script setup lang="ts">

import { ref, computed, onMounted, watch } from 'vue'
import { useDatabaseObject } from 'vuefire'
import { ref as dbRef, getDatabase } from 'firebase/database'

import { LMap, LTileLayer, LMarker, LPopup, LPolyline, LIcon } from '@vue-leaflet/vue-leaflet'

import L from 'leaflet'

import 'leaflet-polylinedecorator'

import LocationButton from './LocationButton.vue'
import EnhancedSearch from './EnhancedSearch.vue'
import WalkingDirections from './WalkingDirections.vue'
import MultimodalDirections from './MultimodalDirections.vue'
import apiService, { type Station } from '@/services/apiService'

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

// State pentru afișarea tuturor stațiilor
const showAllStations = ref(false)

// State pentru afișarea/ascunderea sidebar-ului
const showSidebar = ref(true)

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

// Computed pentru a afișa toate stațiile când nu e selectat un traseu
const displayAllStations = computed(() => {
  // Dacă sunt stații selectate (traseu ales), nu afișa toate
  if (props.stations && props.stations.length > 0) {
    return []
  }
  // Altfel afișează toate stațiile
  return props.allStations || []
})

// Firebase - Ascultă autobuze live
const database = getDatabase()
const busLocationsRef = dbRef(database, 'bus_locations')
const busLocationsData = useDatabaseObject(busLocationsRef)

// Computed pentru a transforma datele Firebase în array și filtra cele mai apropiate 10
const liveBuses = computed(() => {
  if (!busLocationsData.value) {
    return []
  }
  
  const buses: BusLocation[] = []
  Object.entries(busLocationsData.value).forEach(([id, data]: [string, any]) => {
    if (data && data.latitude && data.longitude) {
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
  })
  
  // Dacă nu avem locația utilizatorului, afișăm toate autobuzele
  if (!userLocation.value?.lat || !userLocation.value?.lon) {
    return buses
  }
  
  // Calculăm distanța pentru fiecare autobuz
  const busesWithDistance = buses.map(bus => {
    const distance = calculateDistance(
      userLocation.value!.lat,
      userLocation.value!.lon,
      bus.latitude,
      bus.longitude
    )
    return { ...bus, distance }
  })
  
  // Sortăm după distanță și luăm primele 10
  const nearest = busesWithDistance
    .sort((a, b) => a.distance - b.distance)
    .slice(0, 10)
  
  return nearest
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

// Încarcă culorile traseelor la montare
onMounted(async () => {
  delete (L.Icon.Default.prototype as any)._getIconUrl
  L.Icon.Default.mergeOptions({
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
  })
  
  // Încarcă culorile traseelor din API
  try {
    const routes = await apiService.getRoutes()
    routes.forEach(route => {
      if (route.id && (route as any).color) {
        routeColors.value[route.id] = (route as any).color
      }
    })
    console.log('✅ Loaded colors for', Object.keys(routeColors.value).length, 'routes')
  } catch (error) {
    console.error('❌ Failed to load route colors:', error)
  }
  
  setTimeout(() => {
    isReady.value = true
  }, 100)
})

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
  userLoc: { lat: number; lon: number },
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
      
      // TODO: Implementează UI pentru trasee cu transfer
      // Deocamdată afișăm prima rută
      emit('routeSelected', route1.id, stations1)
      
      const routingData = await calculateBothWalkingRoutes(userLoc, startStation, transferStation, destination)
      
      if (routingData) {
        alert(`Traseu cu transfer găsit:\n\n` +
              `1️⃣ ${route1.routeNumber}: ${startStation.name} → ${transferStation.name}\n` +
              `2️⃣ ${route2.routeNumber}: ${transferStation.name} → ${endStation.name}\n\n` +
              `UI pentru transferuri în dezvoltare!`)
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
}

// Închide panoul multimodal
const closeMultimodal = () => {
  showMultimodal.value = false
  walkingPath.value = []
  secondWalkingPath.value = []
}

// Expunem metoda pentru a putea fi apelată din componenta părinte
defineExpose({
  centerMap
})
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
.stations-toggle {
  position: absolute;
  top: 80px;
  right: 16px;
  z-index: 1000;
  background: white;
  padding: 12px 16px;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  backdrop-filter: blur(10px);
}

/* Stiluri pentru butonul de toggle sidebar */
.sidebar-toggle-btn {
  position: absolute;
  top: 16px;
  left: 420px;
  z-index: 1001;
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

.toggle-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  user-select: none;
}

.toggle-checkbox {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #3b82f6;
}

.toggle-text {
  font-size: 14px;
  font-weight: 500;
  color: #1f2937;
}

.toggle-label:hover .toggle-text {
  color: #3b82f6;
}
</style>