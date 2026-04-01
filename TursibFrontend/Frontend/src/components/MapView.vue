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
    
    <!-- Butoane din dreapta sus -->
    <div class="top-right-buttons">
      <!-- Buton pentru favorite -->
      <button @click="goToFavorites" class="action-btn" title="Favorite">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M20.84 4.61C20.3292 4.099 19.7228 3.69364 19.0554 3.41708C18.3879 3.14052 17.6725 2.99817 16.95 2.99817C16.2275 2.99817 15.5121 3.14052 14.8446 3.41708C14.1772 3.69364 13.5708 4.099 13.06 4.61L12 5.67L10.94 4.61C9.9083 3.57831 8.50903 2.99871 7.05 2.99871C5.59096 2.99871 4.19169 3.57831 3.16 4.61C2.1283 5.64169 1.54871 7.04097 1.54871 8.5C1.54871 9.95903 2.1283 11.3583 3.16 12.39L4.22 13.45L12 21.23L19.78 13.45L20.84 12.39C21.351 11.8792 21.7563 11.2728 22.0329 10.6053C22.3095 9.93789 22.4518 9.22248 22.4518 8.5C22.4518 7.77752 22.3095 7.06211 22.0329 6.39469C21.7563 5.72728 21.351 5.12084 20.84 4.61V4.61Z" 
            stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      
      <!-- Buton pentru statistici -->
      <button @click="goToStatistics" class="action-btn" title="Statistici">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M3 3v18h18" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <path d="M18 17V9M13 17V5M8 17v-3" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      
      <!-- Buton pentru istoric călătorii -->
      <button @click="showTripHistory = !showTripHistory" class="action-btn" title="Istoric călătorii" :class="{ 'active': showTripHistory }">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      
      <!-- Buton pentru dark mode -->
      <button @click="toggleDarkMode" class="action-btn" title="Dark Mode">
        <svg v-if="!isDarkMode" width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        <svg v-else width="20" height="20" viewBox="0 0 24 24" fill="none">
          <circle cx="12" cy="12" r="5" stroke="currentColor" stroke-width="2"/>
          <path d="M12 1v2m0 18v2M4.22 4.22l1.42 1.42m12.72 12.72l1.42 1.42M1 12h2m18 0h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
      </button>
      
      <!-- Buton pentru admin (doar dacă e admin) -->
      <button 
        v-if="isAdmin" 
        @click="goToAdmin" 
        class="action-btn admin-btn" 
        title="Admin Panel"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <rect x="3" y="3" width="7" height="7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <rect x="14" y="3" width="7" height="7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <rect x="14" y="14" width="7" height="7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <rect x="3" y="14" width="7" height="7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      
      <!-- Buton pentru login/logout -->
      <button 
        v-if="!isAuthenticated" 
        @click="router.push('/login')" 
        class="action-btn" 
        title="Login"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <polyline points="10 17 15 12 10 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <line x1="15" y1="12" x2="3" y2="12" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      <button 
        v-else 
        @click="handleLogout" 
        class="action-btn logout-btn" 
        title="Logout"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <polyline points="16 17 21 12 16 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <line x1="21" y1="12" x2="9" y2="12" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      
      <!-- Buton pentru locație -->
      <LocationButton @location-found="handleLocationFound" />
    </div>
    
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
    
    <!-- Panoul pentru istoric călătorii -->
    <TripHistory
      :visible="showTripHistory"
      @close="showTripHistory = false"
      @select-trip="handleTripSelected"
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
    
    <!-- Alternative Routes Panel -->
    <AlternativeRoutesPanel
      v-if="showAlternatives"
      :routes="alternativeRoutes"
      @close="showAlternatives = false"
      @select-route="handleAlternativeRouteSelected"
    />

    <l-map

      v-if="isReady"

      ref="map"

      :zoom="zoom"

      :center="center"

      :options="mapOptions"

      style="height: 100%; width: 100%"

      @ready="onMapReady"

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

      <!-- Marker ROȘU pentru stația de transfer (schimbare autobuz) -->
      <l-marker
        v-if="showTransfer && transferData.transferStation"
        :lat-lng="[transferData.transferStation.latitude, transferData.transferStation.longitude]"
      >
        <l-icon
          :icon-size="[50, 50]"
          :icon-anchor="[25, 50]"
        >
          <div style="font-size: 50px; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.3));">
            📍
          </div>
        </l-icon>
        <l-popup>
          <div style="text-align: center; font-weight: bold; color: #ef4444;">
            🔄 TRANSFER AICI<br>
            {{ transferData.transferStation.name }}
          </div>
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
      
      <!-- Linia VERDE - traseul complet al primului autobuz -->
      <l-polyline
        v-if="completeBusRoute1.length > 0"
        :lat-lngs="completeBusRoute1"
        color="#10b981"
        :weight="4"
        :opacity="0.6"
      />
      
      <!-- Linia ALBASTRĂ - traseul complet al celui de-al doilea autobuz (dacă e transfer) -->
      <l-polyline
        v-if="completeBusRoute2.length > 0"
        :lat-lngs="completeBusRoute2"
        color="#3b82f6"
        :weight="4"
        :opacity="0.6"
      />

      <!-- Mers pe jos: origine → stație urcare (portocaliu, punctat) -->
      <l-polyline
        v-if="actualWalkingPath.length > 0 && (showMultimodal || showTransfer)"
        :lat-lngs="actualWalkingPath"
        color="#f97316"
        :weight="4"
        :opacity="0.9"
        :dash-array="'8, 8'"
      />

      <!-- Segment autobuz 1 -->
      <l-polyline
        v-if="walkingPath.length > 0 && (showMultimodal || showTransfer)"
        :lat-lngs="walkingPath"
        :color="showTransfer ? transferData.route1Color : multimodalData.busColor"
        :weight="5"
        :opacity="0.85"
      />

      <!-- Segment autobuz 2 (transfer) -->
      <l-polyline
        v-if="secondWalkingPath.length > 0 && showTransfer"
        :lat-lngs="secondWalkingPath"
        :color="transferData.route2Color"
        :weight="5"
        :opacity="0.85"
      />

      <!-- Mers pe jos: stație coborâre → destinație (portocaliu, punctat) -->
      <l-polyline
        v-if="actualSecondWalkingPath.length > 0 && (showMultimodal || showTransfer)"
        :lat-lngs="actualSecondWalkingPath"
        color="#f97316"
        :weight="4"
        :opacity="0.9"
        :dash-array="'8, 8'"
      />

      <!-- Marker origine plan (adresă sau locație) -->
      <l-marker
        v-if="(showMultimodal || showTransfer) && savedUserLocation"
        :lat-lng="[savedUserLocation.lat, savedUserLocation.lon]"
      >
        <l-icon :icon-size="[36, 36]" :icon-anchor="[18, 36]" icon-url="/placeholder.png" />
        <l-popup>
          <strong>{{ multimodalData.startLocation || transferData.startName || 'Origine' }}</strong>
        </l-popup>
      </l-marker>

      <!-- Marker destinație plan -->
      <l-marker
        v-if="(showMultimodal || showTransfer) && savedDestination"
        :lat-lng="[savedDestination.lat, savedDestination.lon]"
      >
        <l-icon :icon-size="[36, 36]" :icon-anchor="[18, 36]" icon-url="/placeholder.png" />
        <l-popup>
          <strong>{{ savedDestination.name }}</strong>
        </l-popup>
      </l-marker>

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

import { ref, computed, onMounted, onActivated, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
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
import AlternativeRoutesPanel from './AlternativeRoutesPanel.vue'
import TripHistory from './TripHistory.vue'
import apiService, { type Station } from '@/services/apiService'
import type { PlanResult } from './Sidebar.vue'
import { useNotifications, checkBusNotifications } from '@/composables/useNotifications'
import { authService } from '@/services/adminService'
import { useDarkMode } from '@/composables/useDarkMode'
import { tripHistoryService, type TripHistoryItem } from '@/services/tripHistoryService'

const router = useRouter()

// Dark mode
const { isDarkMode, toggleDarkMode } = useDarkMode()

// Navigation functions
const goToFavorites = () => {
  router.push('/favorites')
}

const goToStatistics = () => {
  router.push('/statistics')
}

const goToAdmin = () => {
  router.push('/admin/routes')
}

// User authentication state
const isAuthenticated = ref(false)
const currentUser = ref<{ username: string; role: string } | null>(null)
const showUserMenu = ref(false)

// Check if user is admin
const isAdmin = computed(() => {
  const user = currentUser.value
  const hasAdminRole = user?.role?.toLowerCase() === 'admin'
  return isAuthenticated.value && hasAdminRole
})

const checkAuthStatus = () => {
  isAuthenticated.value = authService.isAuthenticated()
  currentUser.value = authService.getUser()
}

const handleLogout = () => {
  authService.logout()
  isAuthenticated.value = false
  currentUser.value = null
  showUserMenu.value = false
}

// Notifications
const { 
  enableNotifications, 
  disableNotifications, 
  getNotificationSettings,
  checkNotificationSupport
} = useNotifications()

// Check authentication on mount and periodically
onMounted(() => {
  // Initial auth check
  checkAuthStatus()
  
  // Check notifications support
  const diagnostics = checkNotificationSupport()
  if (!diagnostics.supported) {
  }
  
  // Check auth status every 2 seconds to detect login changes
  setInterval(() => {
    const wasAuthenticated = isAuthenticated.value
    checkAuthStatus()
    if (!wasAuthenticated && isAuthenticated.value) {
    }
  }, 2000)
})

// Also check when component becomes active (after navigation, keep-alive)
onActivated(() => {
  checkAuthStatus()
  // Recalculează dimensiunea hărții după reactivare (keep-alive)
  nextTick(() => {
    if (map.value && map.value.leafletObject) {
      map.value.leafletObject.invalidateSize()
    }
  })
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

// API Base URL
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

// Props

interface Props {

  stations?: Station[]

  allStations?: Station[]

  routeColor?: string

  tripPlan?: PlanResult | null

}



const props = withDefaults(defineProps<Props>(), {

  stations: () => [],

  allStations: () => [],

  routeColor: '#2563eb', // Albastru

  tripPlan: null

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

// State pentru trasee reale de mers pe jos (roșu)
const actualWalkingPath = ref<[number, number][]>([]) // De la user la prima stație
const actualSecondWalkingPath = ref<[number, number][]>([]) // De la ultima stație la destinație

// State pentru traseul complet (ROȘU) - tot drumul de la start la destinație
const completeRoutePath = ref<[number, number][]>([])

// State pentru traseele COMPLETE ale autobuzelor (pentru afișare în verde și albastru)
const completeBusRoute1 = ref<[number, number][]>([]) // Traseul complet al primului autobuz (VERDE)
const completeBusRoute2 = ref<[number, number][]>([]) // Traseul complet al celui de-al doilea autobuz (ALBASTRU)

// Salvăm locația utilizatorului și destinația pentru calculul traseelor
const savedUserLocation = ref<{ lat: number; lon: number } | null>(null)
const savedDestination = ref<{ lat: number; lon: number; name: string } | null>(null)

// State pentru afișarea panelului cu stații apropiate
const showNearbyStations = ref(false)

// State pentru rute alternative
const alternativeRoutes = ref<any[]>([])
const showAlternatives = ref(false)
const selectedAlternative = ref<any | null>(null)

// State pentru istoric călătorii
const showTripHistory = ref(false)

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

// ========================
// MAP READY + CLICK
// ========================

// Apelat când harta Leaflet e complet inițializată
const onMapReady = (_mapInstance: any) => {
  // rezervat pentru extensii viitoare
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
      
    } catch (error) {
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
    
    // Fallback: OSRM API
    const coordinates = stations
      .map(s => `${s.longitude},${s.latitude}`)
      .join(';')
    
    const url = `https://router.project-osrm.org/route/v1/driving/${coordinates}?overview=full&geometries=geojson`
    
    const response = await fetch(url)
    const data = await response.json()
    
    if (data.code === 'Ok' && data.routes && data.routes.length > 0) {
      const geometry = data.routes[0].geometry.coordinates
      routePath.value = geometry.map((coord: number[]) => [coord[1], coord[0]] as [number, number])
    } else {
      routePath.value = stations.map(s => [s.latitude, s.longitude] as [number, number])
    }
  } catch (error) {
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
  // Activează automat panelul cu stații apropiate
  showNearbyStations.value = true
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
}

// Găsește cea mai apropiată stație de un punct
const findNearestStation = (lat: number, lon: number, stations: typeof props.allStations): typeof props.allStations[0] | null => {
  if (!stations || stations.length === 0) return null

  let nearest = stations[0]
  if (!nearest) return null

  let minDistance = calculateDistance(lat, lon, nearest.latitude, nearest.longitude)
  
  for (const station of stations) {
    const distance = calculateDistance(lat, lon, station.latitude, station.longitude)
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
    }
    
    return {
      firstDistance,
      firstTime,
      secondDistance,
      secondTime
    }
  } catch (error) {
    return null
  }
}

// Găsește un traseu de autobuz care conectează două stații
const findConnectingRoute = async (startStation: typeof props.allStations[0], endStation: typeof props.allStations[0]) => {
  try {
    // Obține toate traseele
    const routes = await apiService.getRoutes()
    
    
    // Cache pentru stațiile fiecărei rute
    const routeStationsCache: Map<number, any[]> = new Map()
    
    // 1. DIRECT ROUTE: verifică dacă există rută directă
    for (const route of routes) {
      const stations = await apiService.getRouteStations(route.id)
      routeStationsCache.set(route.id, stations)
      
      const startIndex = stations.findIndex(s => s.id === startStation.id)
      const endIndex = stations.findIndex(s => s.id === endStation.id)
      
      if (startIndex !== -1 && endIndex !== -1 && startIndex !== endIndex) {
        return { type: 'direct', route1: route, stations1: stations }
      }
    }
    
    
    // 2. TRANSFER ROUTE: găsim rute cu transfer
    const routesWithStart = routes.filter(r => {
      const stations = routeStationsCache.get(r.id) || []
      return stations.some(s => s.id === startStation.id)
    })
    
    const routesWithEnd = routes.filter(r => {
      const stations = routeStationsCache.get(r.id) || []
      return stations.some(s => s.id === endStation.id)
    })
    
    
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
    
    return null
  } catch (error) {
    return null
  }
}

// Fetch alternative routes - CALCULARE LOCALĂ (nu depinde de backend)
const fetchAlternativeRoutes = async (startStationId: number, endStationId: number) => {
  try {
    
    const routes = await apiService.getRoutes()
    const alternatives: any[] = []
    
    // Cache pentru stațiile fiecărei rute
    const routeStationsCache: Map<number, any[]> = new Map()
    
    // Preîncarcă toate stațiile pentru fiecare rută
    for (const route of routes) {
      const stations = await apiService.getRouteStations(route.id)
      routeStationsCache.set(route.id, stations)
    }
    
    // 1. Găsește toate rutele DIRECTE
    for (const route of routes) {
      const stations = routeStationsCache.get(route.id) || []
      const startIndex = stations.findIndex((s: any) => s.id === startStationId)
      const endIndex = stations.findIndex((s: any) => s.id === endStationId)
      
      if (startIndex !== -1 && endIndex !== -1 && startIndex !== endIndex) {
        const stationCount = Math.abs(endIndex - startIndex)
        const estimatedTime = stationCount * 3 // 3 min per station
        
        alternatives.push({
          routeId: route.id,
          routeNumber: route.routeNumber,
          routeType: 'direct',
          totalDuration: estimatedTime,
          segments: [{
            type: 'bus',
            routeNumber: route.routeNumber,
            color: route.color,
            startStation: stations[startIndex],
            endStation: stations[endIndex],
            stationCount: stationCount,
            duration: estimatedTime
          }]
        })
      }
    }
    
    // 2. Găsește rute cu TRANSFER (doar primele 2 pentru performanță)
    const routesWithStart = routes.filter(r => {
      const stations = routeStationsCache.get(r.id) || []
      return stations.some((s: any) => s.id === startStationId)
    })
    
    const routesWithEnd = routes.filter(r => {
      const stations = routeStationsCache.get(r.id) || []
      return stations.some((s: any) => s.id === endStationId)
    })
    
    let transferCount = 0
    for (const route1 of routesWithStart) {
      if (transferCount >= 2) break
      
      const stations1 = routeStationsCache.get(route1.id) || []
      
      for (const route2 of routesWithEnd) {
        if (route1.id === route2.id || transferCount >= 2) continue
        
        const stations2 = routeStationsCache.get(route2.id) || []
        const commonStations = stations1.filter((s1: any) => 
          stations2.some((s2: any) => s2.id === s1.id)
        )
        
        if (commonStations.length > 0) {
          const transferStation = commonStations[0]
          const startIdx1 = stations1.findIndex((s: any) => s.id === startStationId)
          const transferIdx1 = stations1.findIndex((s: any) => s.id === transferStation.id)
          const transferIdx2 = stations2.findIndex((s: any) => s.id === transferStation.id)
          const endIdx2 = stations2.findIndex((s: any) => s.id === endStationId)
          
          const count1 = Math.abs(transferIdx1 - startIdx1)
          const count2 = Math.abs(endIdx2 - transferIdx2)
          const time1 = count1 * 3
          const time2 = count2 * 3
          const totalTime = time1 + time2 + 5 // +5 min transfer
          
          alternatives.push({
            routeId: `${route1.id}-${route2.id}`,
            routeNumber: `${route1.routeNumber} → ${route2.routeNumber}`,
            routeType: 'transfer',
            totalDuration: totalTime,
            segments: [
              {
                type: 'bus',
                routeNumber: route1.routeNumber,
                color: route1.color,
                startStation: stations1[startIdx1],
                endStation: transferStation,
                stationCount: count1,
                duration: time1
              },
              {
                type: 'transfer',
                station: transferStation,
                duration: 5
              },
              {
                type: 'bus',
                routeNumber: route2.routeNumber,
                color: route2.color,
                startStation: transferStation,
                endStation: stations2[endIdx2],
                stationCount: count2,
                duration: time2
              }
            ]
          })
          
          transferCount++
        }
      }
    }
    
    // 3. Sortează după durată și atribuie categorii
    alternatives.sort((a, b) => a.totalDuration - b.totalDuration)
    
    if (alternatives.length > 0) {
      alternatives[0].routeCategory = 'fastest'
      if (alternatives.length > 1) {
        alternatives[alternatives.length - 1].routeCategory = 'scenic'
      }
    }
    
    // Atribuie rank final
    alternatives.forEach((alt, idx) => {
      alt.routeRank = idx + 1
    })
    
    return alternatives.slice(0, 3) // Max 3 rute
  } catch (error) {
    return []
  }
}

// Handler pentru selectarea unei rute alternative
const handleAlternativeRouteSelected = async (route: any) => {
  selectedAlternative.value = route
  showAlternatives.value = false
  
  // Salvează călătoria în istoric
  saveTripToHistory(route)
  
  // Curățăm traseele anterioare
  walkingPath.value = []
  secondWalkingPath.value = []
  multimodalBusPath.value = []
  actualWalkingPath.value = []
  actualSecondWalkingPath.value = []
  completeRoutePath.value = []
  completeBusRoute1.value = []
  completeBusRoute2.value = []
  
  if (!route.segments || route.segments.length === 0) {
    return
  }
  
  const busSegments = route.segments.filter((s: any) => s.type === 'bus')
  
  if (busSegments.length === 0) {
    return
  }
  
  // Verificăm dacă e rută directă sau cu transfer
  if (busSegments.length === 1) {
    // RUTĂ DIRECTĂ
    const segment = busSegments[0]
    
    try {
      // Găsim ID-ul rutei din backend
      const routes = await apiService.getRoutes()
      const foundRoute = routes.find(r => r.routeNumber === segment.routeNumber)
      
      if (!foundRoute) {
        return
      }
      
      // Încărcăm segmentul GTFS de autobuz (doar partea pe care o parcurg)
      const gtfsSegment = await apiService.getRouteSegment(
        foundRoute.id, 
        segment.startStation.id, 
        segment.endStation.id
      )
      
      let busPart: [number, number][] = gtfsSegment?.points?.map(point => 
        [point.latitude, point.longitude] as [number, number]
      ) || []
      
      
      // Dacă GTFS nu returnează suficiente puncte, calculăm cu OSRM
      if (busPart.length < 2) {
        const coords = `${segment.startStation.longitude},${segment.startStation.latitude};${segment.endStation.longitude},${segment.endStation.latitude}`
        const url = `https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson`
        try {
          const resp = await fetch(url)
          const data = await resp.json()
          if (data.code === 'Ok' && data.routes?.[0]) {
            busPart = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {
        }
      }
      
      // Calculăm traseele de mers pe jos dacă avem locația salvată
      if (savedUserLocation.value && savedDestination.value) {
        // 1. Traseu de la user la prima stație
        try {
          const url1 = `https://router.project-osrm.org/route/v1/foot/${savedUserLocation.value.lon},${savedUserLocation.value.lat};${segment.startStation.longitude},${segment.startStation.latitude}?overview=full&geometries=geojson`
          const resp1 = await fetch(url1)
          const data1 = await resp1.json()
          if (data1.code === 'Ok' && data1.routes?.[0]) {
            actualWalkingPath.value = data1.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {
        }
        
        // 2. Traseu de la ultima stație la destinație
        try {
          const url2 = `https://router.project-osrm.org/route/v1/foot/${segment.endStation.longitude},${segment.endStation.latitude};${savedDestination.value.lon},${savedDestination.value.lat}?overview=full&geometries=geojson`
          const resp2 = await fetch(url2)
          const data2 = await resp2.json()
          if (data2.code === 'Ok' && data2.routes?.[0]) {
            actualSecondWalkingPath.value = data2.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {
        }
      }
      
      // Construiește traseul COMPLET (ROȘU): mers pe jos + autobuz + mers pe jos
      completeRoutePath.value = [
        ...actualWalkingPath.value,
        ...busPart,
        ...actualSecondWalkingPath.value
      ]
      
      // Încarcă traseul COMPLET al autobuzului pentru afișare în VERDE
      try {
        const shapeData = await apiService.getRouteShape(foundRoute.id)
        if (shapeData && shapeData.points && shapeData.points.length > 0) {
          completeBusRoute1.value = shapeData.points.map((p: any) => [p.latitude, p.longitude] as [number, number])
        }
      } catch (e) {
      }
      
      // Afișăm panelul cu detalii
      multimodalData.value = {
        startLocation: 'Locația ta',
        endLocation: 'Destinația',
        boardingStation: segment.startStation.name,
        alightingStation: segment.endStation.name,
        busLine: segment.routeNumber,
        busColor: segment.color || '#3b82f6',
        busStationsList: [segment.startStation.name, segment.endStation.name],
        firstWalkDistance: 0,
        firstWalkTime: 0,
        secondWalkDistance: 0,
        secondWalkTime: 0,
        busTime: segment.duration || 0
      }
      
      showMultimodal.value = true
      
      // Centrează harta pe traseu complet
      if (completeRoutePath.value.length > 0) {
        const bounds = L.latLngBounds(completeRoutePath.value)
        map.value?.leafletObject?.fitBounds(bounds, { padding: [50, 50] })
      }
      
    } catch (error) {
    }
    
  } else if (busSegments.length === 2) {
    // RUTĂ CU TRANSFER
    const segment1 = busSegments[0]
    const segment2 = busSegments[1]
    const transferSegment = route.segments.find((s: any) => s.type === 'transfer')
    
    
    try {
      // Găsim ID-urile rutelor
      const routes = await apiService.getRoutes()
      const route1 = routes.find(r => r.routeNumber === segment1.routeNumber)
      const route2 = routes.find(r => r.routeNumber === segment2.routeNumber)
      
      if (!route1 || !route2) {
        return
      }
      
      // Încărcăm primul segment GTFS
      const gtfsSegment1 = await apiService.getRouteSegment(
        route1.id,
        segment1.startStation.id,
        segment1.endStation.id
      )
      
      // Încărcăm al doilea segment GTFS
      const gtfsSegment2 = await apiService.getRouteSegment(
        route2.id,
        segment2.startStation.id,
        segment2.endStation.id
      )
      
      let busPart1: [number, number][] = gtfsSegment1?.points?.map(point => 
        [point.latitude, point.longitude] as [number, number]
      ) || []
      
      let busPart2: [number, number][] = gtfsSegment2?.points?.map(point => 
        [point.latitude, point.longitude] as [number, number]
      ) || []
      
      
      // Calculăm cu OSRM dacă GTFS nu returnează suficiente puncte
      if (busPart1.length < 2) {
        const coords = `${segment1.startStation.longitude},${segment1.startStation.latitude};${segment1.endStation.longitude},${segment1.endStation.latitude}`
        const url = `https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson`
        try {
          const resp = await fetch(url)
          const data = await resp.json()
          if (data.code === 'Ok' && data.routes?.[0]) {
            busPart1 = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {
        }
      }
      
      if (busPart2.length < 2) {
        const coords = `${segment2.startStation.longitude},${segment2.startStation.latitude};${segment2.endStation.longitude},${segment2.endStation.latitude}`
        const url = `https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson`
        try {
          const resp = await fetch(url)
          const data = await resp.json()
          if (data.code === 'Ok' && data.routes?.[0]) {
            busPart2 = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {
        }
      }
      
      // Calculăm traseele de mers pe jos
      if (savedUserLocation.value && savedDestination.value) {
        try {
          const url1 = `https://router.project-osrm.org/route/v1/foot/${savedUserLocation.value.lon},${savedUserLocation.value.lat};${segment1.startStation.longitude},${segment1.startStation.latitude}?overview=full&geometries=geojson`
          const resp1 = await fetch(url1)
          const data1 = await resp1.json()
          if (data1.code === 'Ok' && data1.routes?.[0]) {
            actualWalkingPath.value = data1.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {}
        
        try {
          const url2 = `https://router.project-osrm.org/route/v1/foot/${segment2.endStation.longitude},${segment2.endStation.latitude};${savedDestination.value.lon},${savedDestination.value.lat}?overview=full&geometries=geojson`
          const resp2 = await fetch(url2)
          const data2 = await resp2.json()
          if (data2.code === 'Ok' && data2.routes?.[0]) {
            actualSecondWalkingPath.value = data2.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch (e) {}
      }
      
      // Construiește traseul COMPLET (ROȘU): mers pe jos + autobuz1 + autobuz2 + mers pe jos
      completeRoutePath.value = [
        ...actualWalkingPath.value,
        ...busPart1,
        ...busPart2,
        ...actualSecondWalkingPath.value
      ]
      
      // Încarcă traseele COMPLETE pentru ambele autobuze
      try {
        const shapes1 = await apiService.getRouteShape(route1.id)
        if (shapes1 && shapes1.points && shapes1.points.length > 0) {
          completeBusRoute1.value = shapes1.points.map((p: any) => [p.latitude, p.longitude] as [number, number])
        }
      } catch (e) {
      }
      
      try {
        const shapes2 = await apiService.getRouteShape(route2.id)
        if (shapes2 && shapes2.points && shapes2.points.length > 0) {
          completeBusRoute2.value = shapes2.points.map((p: any) => [p.latitude, p.longitude] as [number, number])
        }
      } catch (e) {
      }
      
      // Afișăm panelul de transfer
      transferData.value = {
        startName: 'Locația ta',
        endName: 'Destinația',
        boardingStation: segment1.startStation,
        transferStation: transferSegment?.station || segment1.endStation,
        alightingStation: segment2.endStation,
        route1Number: segment1.routeNumber,
        route1Color: segment1.color || '#3b82f6',
        route1StationsCount: segment1.stationCount || 0,
        route2Number: segment2.routeNumber,
        route2Color: segment2.color || '#10b981',
        route2StationsCount: segment2.stationCount || 0,
        firstWalkDistance: 0,
        firstWalkTime: 0,
        busTime1: segment1.duration || 0,
        busTime2: segment2.duration || 0,
        secondWalkDistance: 0,
        secondWalkTime: 0
      }
      
      showTransfer.value = true
      showMultimodal.value = false
      
      // Centrează harta pe traseu complet
      if (completeRoutePath.value.length > 0) {
        const bounds = L.latLngBounds(completeRoutePath.value)
        map.value?.leafletObject?.fitBounds(bounds, { padding: [50, 50] })
      }
      
    } catch (error) {
    }
  }
}

// Handler pentru traseu multimodal (mers pe jos + autobuz + mers pe jos)
const handleMultimodalRouteRequested = async (
  userLoc: { lat: number; lon: number; name?: string },
  destination: { lat: number; lon: number; name: string }
) => {
  
  // Salvăm locația și destinația pentru calculul ulterior
  savedUserLocation.value = userLoc
  savedDestination.value = destination
  
  // 1. Găsește stația cea mai apropiată de utilizator
  const startStation = findNearestStation(userLoc.lat, userLoc.lon, props.allStations)
  if (!startStation) {
    return
  }
  
  // 2. Găsește stația cea mai apropiată de destinație
  const endStation = findNearestStation(destination.lat, destination.lon, props.allStations)
  if (!endStation) {
    return
  }
  
  
  // 2.5. Fetch alternative routes - ÎNTOTDEAUNA
  const alternatives = await fetchAlternativeRoutes(startStation.id, endStation.id)
  if (alternatives.length > 0) {
    alternativeRoutes.value = alternatives
    showAlternatives.value = true
    // OPREȘTE AICI - utilizatorul trebuie să selecteze o rută
    return
  }
  
  // 3. Găsește traseul de autobuz care conectează cele două stații
  const busRoute = await findConnectingRoute(startStation, endStation)
  
  if (busRoute) {
    
    if (busRoute.type === 'direct') {
      // DIRECT ROUTE
      const route = busRoute.route1
      const routeStations = busRoute.stations1
      
      
      const startIndex = routeStations.findIndex(s => s.id === startStation.id)
      const endIndex = routeStations.findIndex(s => s.id === endStation.id)
      
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
          } else {
            await calculateStreetRoute(relevantStations)
            multimodalBusPath.value = routePath.value
          }
        } catch (error) {
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
        return
      }
      
      
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
        
        
        // Calculăm și afișăm traseele de autobuz pe hartă
        try {
          // Primul segment de autobuz (start → transfer)
          const segment1 = await apiService.getRouteSegment(route1.id, startStation.id, transferStation.id)
          if (segment1 && segment1.points && segment1.points.length > 0) {
            walkingPath.value = segment1.points.map(point => 
              [point.latitude, point.longitude] as [number, number]
            )
          }
          
          // Al doilea segment de autobuz (transfer → end)
          const segment2 = await apiService.getRouteSegment(route2.id, transferStation.id, endStation.id)
          if (segment2 && segment2.points && segment2.points.length > 0) {
            secondWalkingPath.value = segment2.points.map(point => 
              [point.latitude, point.longitude] as [number, number]
            )
          }
        } catch (error) {
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
}

// Închide panoul multimodal
const closeMultimodal = () => {
  showMultimodal.value = false
  walkingPath.value = []
  secondWalkingPath.value = []
  multimodalBusPath.value = []
  routePath.value = []
  actualWalkingPath.value = []
  actualSecondWalkingPath.value = []
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
}

// Handler pentru selectarea unei călătorii din istoric
const handleTripSelected = async (trip: TripHistoryItem) => {
  
  // Setăm locațiile
  savedUserLocation.value = trip.startCoords
  savedDestination.value = {
    lat: trip.endCoords.lat,
    lon: trip.endCoords.lon,
    name: trip.endLocation
  }
  
  // Găsim stațiile apropiate
  const startStation = findNearestStation(trip.startCoords.lat, trip.startCoords.lon, props.allStations)
  const endStation = findNearestStation(trip.endCoords.lat, trip.endCoords.lon, props.allStations)
  
  if (!startStation || !endStation) {
    return
  }
  
  // Fetch alternative routes
  const alternatives = await fetchAlternativeRoutes(startStation.id, endStation.id)
  if (alternatives.length > 0) {
    alternativeRoutes.value = alternatives
    showAlternatives.value = true
    showTripHistory.value = false
  }
}

// Salvează călătoria în istoric când se selectează o rută
const saveTripToHistory = (route: any) => {
  if (!savedUserLocation.value || !savedDestination.value) {
    return
  }
  
  const busSegments = route.segments?.filter((s: any) => s.type === 'bus') || []
  const busLines = busSegments.map((s: any) => s.routeNumber)
  
  tripHistoryService.saveTrip({
    startLocation: 'Locația ta',
    endLocation: savedDestination.value.name,
    startCoords: savedUserLocation.value,
    endCoords: { lat: savedDestination.value.lat, lon: savedDestination.value.lon },
    routeType: busSegments.length === 1 ? 'direct' : 'transfer',
    routeDetails: {
      busLines,
      totalDuration: route.totalDuration || 0,
      totalStations: route.totalStations || 0,
      transferStation: busSegments.length > 1 ? busSegments[0].endStation?.name : undefined
    }
  })
}

// Închide panoul de transfer
const closeTransfer = () => {
  showTransfer.value = false
  walkingPath.value = []
  secondWalkingPath.value = []
  multimodalBusPath.value = []
  routePath.value = []
  actualWalkingPath.value = []
  actualSecondWalkingPath.value = []
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
}

// Helper pentru a calcula timpul estimat de autobuz (2 minute per stație)
const calculateBusTime = (stationsCount: number): number => {
  return stationsCount * 2 // 2 minute per stație
}

// Watch pentru planul de călătorie selectat din Sidebar (Planificare tab)
watch(() => props.tripPlan, async (plan) => {
  if (!plan) return

  // Resetăm starea anterioară
  walkingPath.value = []
  secondWalkingPath.value = []
  multimodalBusPath.value = []
  actualWalkingPath.value = []
  actualSecondWalkingPath.value = []
  completeRoutePath.value = []
  completeBusRoute1.value = []
  completeBusRoute2.value = []
  showMultimodal.value = false
  showTransfer.value = false
  showDirections.value = false

  const boarding = plan.boardingStation
  const alighting = plan.alightingStation
  const transfer = plan.transferStation ?? null

  // Colectăm toate căile pentru a calcula bounds
  const allPoints: [number, number][] = []

  // 1. Mers pe jos: origin → stație urcare (dacă originea e adresă, nu stație)
  if (plan.walkToStartMinutes && plan.walkToStartMinutes > 0) {
    try {
      const resp = await fetch(
        `https://router.project-osrm.org/route/v1/foot/${plan.originLon},${plan.originLat};${boarding.longitude},${boarding.latitude}?overview=full&geometries=geojson`
      )
      const data = await resp.json()
      if (data.code === 'Ok' && data.routes?.[0]) {
        actualWalkingPath.value = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
        allPoints.push(...actualWalkingPath.value)
      }
    } catch {}
  } else {
    allPoints.push([plan.originLat, plan.originLon])
  }

  // 2. Segment autobuz 1: boarding → transfer (sau alighting dacă direct)
  const busEndStation = transfer ?? alighting
  try {
    const seg1 = await apiService.getRouteSegment(plan.route1Id, boarding.id, busEndStation.id)
    if (seg1?.points?.length) {
      walkingPath.value = seg1.points.map(p => [p.latitude, p.longitude] as [number, number])
      allPoints.push(...walkingPath.value)
    }
  } catch {}

  // 3. Segment autobuz 2 (dacă e transfer): transfer → alighting
  if (plan.type === 'transfer' && transfer && plan.route2Id) {
    try {
      const seg2 = await apiService.getRouteSegment(plan.route2Id, transfer.id, alighting.id)
      if (seg2?.points?.length) {
        secondWalkingPath.value = seg2.points.map(p => [p.latitude, p.longitude] as [number, number])
        allPoints.push(...secondWalkingPath.value)
      }
    } catch {}
  }

  // 4. Mers pe jos: stație coborâre → destinație
  if (plan.walkToEndMinutes && plan.walkToEndMinutes > 0) {
    try {
      const resp = await fetch(
        `https://router.project-osrm.org/route/v1/foot/${alighting.longitude},${alighting.latitude};${plan.destLon},${plan.destLat}?overview=full&geometries=geojson`
      )
      const data = await resp.json()
      if (data.code === 'Ok' && data.routes?.[0]) {
        actualSecondWalkingPath.value = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
        allPoints.push(...actualSecondWalkingPath.value)
      }
    } catch {}
  } else {
    allPoints.push([plan.destLat, plan.destLon])
  }

  // Salvăm pentru panoul de detalii
  savedUserLocation.value = { lat: plan.originLat, lon: plan.originLon }
  savedDestination.value = { lat: plan.destLat, lon: plan.destLon, name: plan.destName }

  if (plan.type === 'direct') {
    multimodalData.value = {
      startLocation: plan.originName,
      endLocation: plan.destName,
      boardingStation: boarding.name,
      alightingStation: alighting.name,
      busLine: plan.route1Number,
      busColor: plan.route1Color || '#3b82f6',
      busStationsList: [boarding.name, alighting.name],
      firstWalkDistance: 0,
      firstWalkTime: plan.walkToStartMinutes ?? 0,
      secondWalkDistance: 0,
      secondWalkTime: plan.walkToEndMinutes ?? 0,
      busTime: plan.stationsBetween * 2
    }
    showMultimodal.value = true
  } else if (plan.type === 'transfer' && transfer) {
    transferData.value = {
      startName: plan.originName,
      endName: plan.destName,
      boardingStation: boarding,
      transferStation: transfer,
      alightingStation: alighting,
      route1Number: plan.route1Number,
      route1Color: plan.route1Color || '#3b82f6',
      route1StationsCount: plan.route1StationsCount ?? 0,
      route2Number: plan.route2Number ?? '',
      route2Color: plan.route2Color || '#10b981',
      route2StationsCount: plan.route2StationsCount ?? 0,
      firstWalkDistance: 0,
      firstWalkTime: plan.walkToStartMinutes ?? 0,
      busTime1: (plan.route1StationsCount ?? 0) * 2,
      busTime2: (plan.route2StationsCount ?? 0) * 2,
      secondWalkDistance: 0,
      secondWalkTime: plan.walkToEndMinutes ?? 0
    }
    showTransfer.value = true
  }

  // Fit bounds
  if (allPoints.length > 0 && map.value?.leafletObject) {
    const bounds = L.latLngBounds(allPoints)
    map.value.leafletObject.fitBounds(bounds, { padding: [60, 60] })
  }
}, { deep: true })

// Set trip mode
const setTripMode = (enabled: boolean) => {
  tripMode.value = enabled
}

// Handle route search request from EnhancedSearch
const handleRouteSearchRequested = async (
  origin: { lat: number; lon: number; name: string },
  destination: { lat: number; lon: number; name: string }
) => {
  
  // Use multimodal route handler
  await handleMultimodalRouteRequested(
    { lat: origin.lat, lon: origin.lon },
    destination
  )
}

// Expunem metoda pentru a putea fi apelată din componenta părinte
defineExpose({
  centerMap,
  setTripMode,
  invalidateSize: () => {
    // Forțează recalcularea dimensiunii hărții
    // map.value este componenta <l-map>, leafletObject este instanța Leaflet reală
    if (map.value && map.value.leafletObject) {
      map.value.leafletObject.invalidateSize()
    }
  }
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
      alert('Notificările au fost dezactivate pentru această stație')
    } else {
      // Verifică mai întâi suportul pentru notificări
      if (!('Notification' in window)) {
        alert('⚠️ Browserul tău nu suportă notificări. Încearcă un browser modern (Chrome, Firefox, Edge).')
        return
      }
      
      // Activează notificări
      const success = await enableNotifications(stationId)
      
      if (success) {
        alert('✅ Notificările au fost activate! Vei primi o alertă când autobuzul se apropie (la 2 minute).')
      } else {
        alert('❌ Nu s-au putut activa notificările. Verifică dacă ai permis notificările în browser (bifează \"Allow\" în prompt-ul browserului).')
      }
    }
  } catch (error) {
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
    
    const distance = calculateDistance(bus.latitude, bus.longitude, stationLat, stationLon)
    
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
  position: fixed;
  top: 16px;
  left: 16px;
  z-index: 1100;
  background: var(--bg-primary);
  border: none;
  border-radius: 8px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: var(--shadow-md);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.sidebar-toggle-btn:hover {
  background: var(--bg-secondary);
  box-shadow: var(--shadow-lg);
  transform: scale(1.05);
}

.sidebar-toggle-btn svg {
  color: var(--text-primary);
}

/* Grup de butoane din dreapta sus */
.top-right-buttons {
  position: fixed;
  top: 16px;
  right: 16px;
  z-index: 1100;
  display: flex;
  gap: 8px;
  align-items: center;
}

.action-btn {
  background: var(--bg-primary);
  border: none;
  border-radius: 8px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: var(--shadow-md);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  color: var(--text-primary);
}

.action-btn:hover {
  background: var(--bg-secondary);
  box-shadow: var(--shadow-lg);
  transform: scale(1.05);
}

.action-btn.active {
  background: #667eea;
  color: white;
}

.action-btn.active:hover {
  background: #5568d3;
}

.action-btn svg {
  color: currentColor;
}

.action-btn.logout-btn {
  color: #ef4444;
}

.action-btn.logout-btn:hover {
  background: #fef2f2;
  color: #dc2626;
}

.action-btn.admin-btn {
  color: #8b5cf6;
}

.action-btn.admin-btn:hover {
  background: #f5f3ff;
  color: #7c3aed;
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