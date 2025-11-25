<template>

  <div class="map-container">
    <!-- Enhanced Search pentru stații și adrese -->
    <EnhancedSearch 
      :stations="allStations"
      :user-location="userLocation"
      @station-selected="handleStationSelected"
      @address-selected="handleAddressSelected"
      @walking-directions-requested="handleWalkingDirectionsRequested"
      @multimodal-route-requested="handleMultimodalRouteRequested"
    />
    
    <!-- Buton pentru locație -->
    <LocationButton @location-found="handleLocationFound" />
    
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
        >
          <svg width="40" height="40" viewBox="0 0 40 40" xmlns="http://www.w3.org/2000/svg">
            <circle cx="20" cy="20" r="18" fill="#3B82F6" stroke="white" stroke-width="3"/>
            <circle cx="20" cy="20" r="8" fill="white"/>
            <circle cx="20" cy="20" r="4" fill="#3B82F6"/>
          </svg>
        </l-icon>
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
          :icon-size="[36, 46]"
          :icon-anchor="[18, 46]"
        >
          <svg width="36" height="46" viewBox="0 0 36 46" xmlns="http://www.w3.org/2000/svg">
            <path d="M18 0C8.06 0 0 8.06 0 18c0 13.5 18 28 18 28s18-14.5 18-28c0-9.94-8.06-18-18-18z" fill="#EF4444" stroke="white" stroke-width="2"/>
            <circle cx="18" cy="18" r="8" fill="white"/>
          </svg>
        </l-icon>
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
          :icon-anchor="[16, 16]"
        >
          <svg width="32" height="32" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">
            <rect x="8" y="6" width="16" height="20" rx="2" fill="#10B981" stroke="white" stroke-width="2"/>
            <circle cx="16" cy="13" r="3" fill="white"/>
            <rect x="13" y="18" width="6" height="4" rx="1" fill="white"/>
          </svg>
        </l-icon>

        <l-popup>

          <div>

            <strong>{{ station.name }}</strong>

          </div>

        </l-popup>

      </l-marker>
      
      <!-- Markere pentru TOATE stațiile (când nu e selectat traseu) -->

      <l-marker

        v-for="station in displayAllStations"

        :key="`all-${station.id}`"

        :lat-lng="[station.latitude, station.longitude]"

      >
        <l-icon
          :icon-size="[26, 26]"
          :icon-anchor="[13, 13]"
        >
          <svg width="26" height="26" viewBox="0 0 26 26" xmlns="http://www.w3.org/2000/svg">
            <rect x="6" y="4" width="14" height="18" rx="2" fill="#6B7280" stroke="white" stroke-width="2"/>
            <circle cx="13" cy="11" r="2.5" fill="white"/>
            <rect x="10" y="16" width="6" height="3" rx="1" fill="white"/>
          </svg>
        </l-icon>

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
          :icon-size="[44, 44]"
          :icon-anchor="[22, 22]"
        >
          <svg width="44" height="44" viewBox="0 0 44 44" xmlns="http://www.w3.org/2000/svg">
            <rect x="8" y="10" width="28" height="24" rx="4" :fill="getBusColor(bus.routeId)" stroke="white" stroke-width="3"/>
            <rect x="11" y="14" width="10" height="8" rx="1" fill="white" opacity="0.9"/>
            <rect x="23" y="14" width="10" height="8" rx="1" fill="white" opacity="0.9"/>
            <circle cx="15" cy="31" r="3" fill="#1F2937" stroke="white" stroke-width="1.5"/>
            <circle cx="29" cy="31" r="3" fill="#1F2937" stroke="white" stroke-width="1.5"/>
            <rect x="18" y="25" width="8" height="3" rx="1" fill="white"/>
          </svg>
        </l-icon>
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

// Computed pentru a transforma datele Firebase în array
const liveBuses = computed(() => {
  if (!busLocationsData.value) return []
  
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
  return buses
})



// Centrul hărții: Sibiu (Piața Mare)

const center = ref<[number, number]>([45.7970, 24.1523])

const zoom = ref(13)

// Mapare culori pentru fiecare traseu
const routeColors: Record<number, string> = {
  1: '#FF0000',  // Linia 1 - Roșu
  2: '#0000FF',  // Linia 11 - Albastru
  3: '#00AA00'   // Linia 2 - Verde
}

// Funcție pentru a obține culoarea unui autobuz în funcție de routeId
const getBusColor = (routeId: number): string => {
  return routeColors[routeId] || '#2563eb'
}



const mapOptions = {

  zoomControl: true,

  attributionControl: true

}



const attribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'



// Fix pentru iconițele Leaflet (problema cunoscută cu Vite/Webpack)

onMounted(() => {

  delete (L.Icon.Default.prototype as any)._getIconUrl

  L.Icon.Default.mergeOptions({

    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',

    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',

    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',

  })

  

  // Setăm că harta este gata după un mic delay pentru a asigura că DOM-ul este complet încărcat

  setTimeout(() => {

    isReady.value = true

  }, 100)

})



// Funcție pentru a calcula traseul pe străzi folosind OSRM API

const calculateStreetRoute = async (stations: Station[]) => {

  if (stations.length < 2) {

    routePath.value = []

    return

  }



  isLoadingRoute.value = true

  

  try {

    // Construiește URL-ul pentru OSRM API cu parametri avansați

    // Format: lon,lat;lon,lat;lon,lat...

    const coordinates = stations

      .map(s => `${s.longitude},${s.latitude}`)

      .join(';')

    

    // Parametri avansați pentru traseu mai precis:
    // - overview=full: traseu complet detaliat
    // - geometries=geojson: format coordonate
    // - steps=true: instrucțiuni pas cu pas
    // - continue_straight=false: evită U-turn-uri nelogice
    // - annotations=true: informații suplimentare despre segmente
    const url = `https://router.project-osrm.org/route/v1/driving/${coordinates}?overview=full&geometries=geojson&steps=true&continue_straight=false&annotations=true`

    

    const response = await fetch(url)

    const data = await response.json()

    

    if (data.code === 'Ok' && data.routes && data.routes.length > 0) {

      // Extrage coordonatele din GeoJSON (format: [lon, lat])

      const geometry = data.routes[0].geometry.coordinates

      // Convertește [lon, lat] în [lat, lon] pentru Leaflet

      routePath.value = geometry.map((coord: number[]) => [coord[1], coord[0]] as [number, number])

      

      console.log('✅ Traseu pe străzi calculat:', routePath.value.length, 'puncte')

    } else {

      console.error('❌ OSRM API error:', data)

      // Fallback: linie dreaptă între stații

      routePath.value = stations.map(s => [s.latitude, s.longitude] as [number, number])

    }

  } catch (error) {

    console.error('❌ Eroare la calcularea traseului:', error)

    // Fallback: linie dreaptă între stații

    routePath.value = stations.map(s => [s.latitude, s.longitude] as [number, number])

  } finally {

    isLoadingRoute.value = false

  }

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
    console.log(`🚌 Traseu găsit: Linia ${busRoute.routeNumber}`)
    
    // 4. Obține stațiile traseului și emite eveniment pentru a le afișa
    const routeStations = await apiService.getRouteStations(busRoute.id)
    console.log(`📍 Afișez traseul cu ${routeStations.length} stații`)
    
    // Emite eveniment către App.vue să selecteze traseul
    emit('routeSelected', busRoute.id, routeStations)
    
    // 5. Calculează ambele trasee folosind API de routing
    const routingData = await calculateBothWalkingRoutes(userLoc, startStation, endStation, destination)
    
    if (routingData) {
      // Calculează numărul de stații între boarding și alighting
      const startIndex = routeStations.findIndex(s => s.id === startStation.id)
      const endIndex = routeStations.findIndex(s => s.id === endStation.id)
      const stationsBetween = Math.abs(endIndex - startIndex)
      
      // Extrage doar stațiile între boarding și alighting
      const relevantStations = startIndex < endIndex 
        ? routeStations.slice(startIndex, endIndex + 1)
        : routeStations.slice(endIndex, startIndex + 1).reverse()
      
      // Calculează traseul pe străzi DOAR pentru segmentul dintre stațiile relevante
      await calculateStreetRoute(relevantStations)
      multimodalBusPath.value = routePath.value
      
      // 6. Pregătește datele pentru panoul multimodal
      multimodalData.value = {
        startLocation: 'Locația ta',
        endLocation: destination.name,
        boardingStation: startStation.name,
        alightingStation: endStation.name,
        busLine: busRoute.routeNumber,
        busColor: routeColors[busRoute.id] || '#3b82f6',
        busStationsList: relevantStations.map(s => s.name),
        firstWalkDistance: routingData.firstDistance,
        firstWalkTime: routingData.firstTime,
        secondWalkDistance: routingData.secondDistance,
        secondWalkTime: routingData.secondTime,
        busTime: Math.max(stationsBetween * 2, 2) // 2 min per stație, minim 2 min
      }
      
      // Afișează panoul multimodal în loc de walking directions
      showMultimodal.value = true
      showDirections.value = false
    }
    
    // Centrează harta să includă locația, stația de start și destinația
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
  }
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

// Găsește un traseu de autobuz care conectează două stații
const findConnectingRoute = async (startStation: typeof props.allStations[0], endStation: typeof props.allStations[0]) => {
  try {
    // Obține toate traseele
    const routes = await apiService.getRoutes()
    
    // Pentru fiecare traseu, verifică dacă conține ambele stații
    for (const route of routes) {
      const stations = await apiService.getRouteStations(route.id)
      
      const startIndex = stations.findIndex(s => s.id === startStation.id)
      const endIndex = stations.findIndex(s => s.id === endStation.id)
      
      // Dacă ambele stații există și end vine după start
      if (startIndex !== -1 && endIndex !== -1 && startIndex < endIndex) {
        return route
      }
    }
    
    return null
  } catch (error) {
    console.error('❌ Eroare la găsirea traseului:', error)
    return null
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
</style>