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
      v-if="showSidebar || isMobile"
      :stations="allStations"
      :user-location="userLocation"
      :trip-mode="tripMode"
      :directions-active="showDirections || showMultimodal || showTransfer"
      @station-selected="handleStationSelected"
      @station-notification-requested="handleStationNotificationRequested"
      @address-selected="handleAddressSelected"
      @walking-directions-requested="handleWalkingDirectionsRequested"
      @multimodal-route-requested="handleMultimodalRouteRequested"
      @route-search-requested="handleRouteSearchRequested"
    />
    
    <!-- Butoane principale dreapta sus -->
    <div
      class="top-right-buttons"
      :class="{ 'top-right-buttons--docked': showNearbyStations }"
    >
      <!-- Butoane principale (mereu vizibile) -->
      <button @click="toggleLanguage" class="action-btn language-btn" :title="t('language')">
        {{ currentLanguage.toUpperCase() }}
      </button>

      <button @click="toggleDarkMode" class="action-btn" :title="t('darkMode')">
        <svg v-if="!isDarkMode" width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        <svg v-else width="20" height="20" viewBox="0 0 24 24" fill="none">
          <circle cx="12" cy="12" r="5" stroke="currentColor" stroke-width="2"/>
          <path d="M12 1v2m0 18v2M4.22 4.22l1.42 1.42m12.72 12.72l1.42 1.42M1 12h2m18 0h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
      </button>

      <button @click="showAllBuses = !showAllBuses" class="action-btn" :class="{ active: showAllBuses }" :title="showAllBuses ? 'Arată 30 autobuze' : 'Arată toate autobuzele'">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
      </button>

      <LocationButton @location-found="handleLocationFound" />

      <!-- Meniu expandabil pentru butoane secundare -->
      <button @click="showMoreMenu = !showMoreMenu" class="action-btn more-btn" :title="t('more') || 'Mai mult'">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
          <circle cx="12" cy="5" r="1.5" fill="currentColor"/>
          <circle cx="12" cy="12" r="1.5" fill="currentColor"/>
          <circle cx="12" cy="19" r="1.5" fill="currentColor"/>
        </svg>
      </button>

      <!-- Dropdown meniu secundar -->
      <transition name="fade">
        <div v-if="showMoreMenu" class="more-menu" @click="showMoreMenu = false">
          <button @click="goToFavorites" class="more-menu-item">
            <span class="more-menu-icon">&#x2764;</span>
            <span>{{ t('favorites') }}</span>
          </button>
          <button v-if="isAuthenticated" @click="goToTickets" class="more-menu-item">
            <span class="more-menu-icon">&#x1F3AB;</span>
            <span>{{ t('tickets') || 'Bilete' }}</span>
          </button>
          <button @click="goToStatistics" class="more-menu-item">
            <span class="more-menu-icon">&#x1F4CA;</span>
            <span>{{ t('statistics') }}</span>
          </button>
          <button @click="showTripHistory = !showTripHistory" class="more-menu-item">
            <span class="more-menu-icon">&#x1F553;</span>
            <span>{{ t('tripHistory') || 'Istoric' }}</span>
          </button>
          <div class="more-menu-divider"></div>
          <button v-if="isAdmin" @click="goToAdmin" class="more-menu-item">
            <span class="more-menu-icon">&#x2699;</span>
            <span>{{ t('adminPanel') }}</span>
          </button>
          <button v-if="!isAuthenticated" @click="router.push('/login')" class="more-menu-item">
            <span class="more-menu-icon">&#x1F511;</span>
            <span>Login</span>
          </button>
          <button v-else @click="handleLogout" class="more-menu-item more-menu-item--danger">
            <span class="more-menu-icon">&#x1F6AA;</span>
            <span>{{ t('logout') }}</span>
          </button>
        </div>
      </transition>
    </div>

    <!-- Toast Notifications -->
    <transition name="toast">
      <div v-if="toastMessage" class="toast-notification" :class="'toast--' + toastType">
        {{ toastMessage }}
      </div>
    </transition>

    <!-- Buton închidere vizualizare traseu -->
    <transition name="fade">
      <button
        v-if="routePath.length > 0 && !showMultimodal && !showTransfer"
        @click="closeRouteView"
        class="close-route-btn"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
          <path d="M18 6L6 18M6 6l12 12"/>
        </svg>
        {{ t('closeRoute') || 'Închide traseu' }}
      </button>
    </transition>

    <div class="map-aura"></div>

    <div class="map-hud" :class="{ 'map-hud--sidebar-open': showSidebar && !isMobile }">
      <div class="hud-chip hud-chip-primary">
        <span class="hud-dot live"></span>
        {{ currentLanguage.toUpperCase() }} • {{ liveBuses.length }} autobuze live
      </div>
      <div class="hud-chip">
        <span class="hud-dot nearby"></span>
        {{ nearbyStations.length }} stații apropiate
      </div>
      <div class="hud-chip" v-if="showTransfer || showMultimodal">
        <span class="hud-dot route"></span>
        Traseu activ
      </div>
    </div>

    <div
      v-if="notificationFeedback"
      class="notification-feedback"
      :class="notificationFeedback.type"
      role="status"
      aria-live="polite"
    >
      {{ notificationFeedback.message }}
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
      :bus-stations-count="multimodalData.busStationsCount"
      :bus-start-time="multimodalData.busStartTime"
      :bus-end-time="multimodalData.busEndTime"
      :departure-time="multimodalData.departureTime"
      :arrival-time="multimodalData.arrivalTime"
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
      :transfer-station-2="transferData.transferStation2"
      :alighting-station="transferData.alightingStation"
      :route1-number="transferData.route1Number"
      :route1-color="transferData.route1Color"
      :route1-stations-count="transferData.route1StationsCount"
      :route2-number="transferData.route2Number"
      :route2-color="transferData.route2Color"
      :route2-stations-count="transferData.route2StationsCount"
      :route3-number="transferData.route3Number"
      :route3-color="transferData.route3Color"
      :route3-stations-count="transferData.route3StationsCount"
      :first-walk-distance="transferData.firstWalkDistance"
      :first-walk-time="transferData.firstWalkTime"
      :bus-time1="transferData.busTime1"
      :bus-time2="transferData.busTime2"
      :bus-time3="transferData.busTime3"
      :second-walk-distance="transferData.secondWalkDistance"
      :second-walk-time="transferData.secondWalkTime"
      :bus1-start-time="transferData.bus1StartTime"
      :bus1-end-time="transferData.bus1EndTime"
      :bus2-start-time="transferData.bus2StartTime"
      :bus2-end-time="transferData.bus2EndTime"
      :bus3-start-time="transferData.bus3StartTime"
      :bus3-end-time="transferData.bus3EndTime"
      :departure-time="transferData.departureTime"
      :arrival-time="transferData.arrivalTime"
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
      :active-notification-route-id="getNotificationSettings().enabled ? getNotificationSettings().routeId : null"
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
      
      <!-- Pin plasat de utilizator pe hartă -->
      <l-marker
        v-if="pinnedLocation"
        :lat-lng="[pinnedLocation.lat, pinnedLocation.lon]"
      >
        <l-icon
          :icon-size="[30, 45]"
          :icon-anchor="[15, 45]"
          :popup-anchor="[0, -40]"
          icon-url="https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png"
          shadow-url="https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png"
        />
        <l-popup :options="{ minWidth: 200, maxWidth: 280 }">
          <div class="pin-popup">
            <div class="pin-popup-address">
              <strong>{{ pinnedLocation.name }}</strong>
            </div>
            <div class="pin-popup-actions">
              <button class="pin-action-btn" @click="pinNavigateHere">
                🧭 Navighează aici
              </button>
              <button class="pin-action-btn" @click="pinSearchFrom">
                📍 Plecare de aici
              </button>
              <button class="pin-action-btn" @click="pinAddFavorite">
                ⭐ Adaugă la favorite
              </button>
              <button class="pin-action-btn pin-action-btn--close" @click="pinClose">
                ✕ Închide pinul
              </button>
            </div>
          </div>
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
      
      <!-- Linia VERDE - traseul complet al primului autobuz (doar dacă nu e mod plan) -->
      <l-polyline
        v-if="completeBusRoute1.length > 0 && !showMultimodal && !showTransfer"
        :lat-lngs="completeBusRoute1"
        color="#10b981"
        :weight="4"
        :opacity="0.6"
      />

      <!-- Linia ALBASTRĂ - traseul complet al celui de-al doilea autobuz (doar dacă nu e mod plan) -->
      <l-polyline
        v-if="completeBusRoute2.length > 0 && !showMultimodal && !showTransfer"
        :lat-lngs="completeBusRoute2"
        color="#3b82f6"
        :weight="4"
        :opacity="0.6"
      />

      <!-- Mers pe jos: origine → stație urcare (roșu) -->
      <l-polyline
        v-if="actualWalkingPath.length > 0 && (showMultimodal || showTransfer)"
        :lat-lngs="actualWalkingPath"
        color="#ef4444"
        :weight="4"
        :opacity="0.9"
        :dash-array="'10, 6'"
      />

      <!-- Segment autobuz 1 (albastru) -->
      <l-polyline
        v-if="walkingPath.length > 0 && (showMultimodal || showTransfer)"
        :lat-lngs="walkingPath"
        color="#3b82f6"
        :weight="6"
        :opacity="0.9"
      />

      <!-- Segment autobuz 2 (verde) -->
      <l-polyline
        v-if="secondWalkingPath.length > 0 && showTransfer"
        :lat-lngs="secondWalkingPath"
        color="#10b981"
        :weight="6"
        :opacity="0.9"
      />

      <!-- Segment autobuz 3 (roșu) -->
      <l-polyline
        v-if="thirdBusPath.length > 0 && showTransfer"
        :lat-lngs="thirdBusPath"
        color="#dc2626"
        :weight="6"
        :opacity="0.9"
      />

      <!-- Mers pe jos: stație coborâre → destinație (roșu) -->
      <l-polyline
        v-if="actualSecondWalkingPath.length > 0 && (showMultimodal || showTransfer)"
        :lat-lngs="actualSecondWalkingPath"
        color="#ef4444"
        :weight="4"
        :opacity="0.9"
        :dash-array="'10, 6'"
      />

      <!-- Marker origine plan -->
      <l-marker
        v-if="(showMultimodal || showTransfer) && savedUserLocation"
        :lat-lng="[savedUserLocation.lat, savedUserLocation.lon]"
      >
        <l-icon :icon-size="[28, 28]" :icon-anchor="[14, 14]">
          <div class="trip-pin origin-pin">
            <div class="pin-bubble">
              <svg viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="12" height="12">
                <circle cx="12" cy="5" r="2.5"/>
                <path d="M9 22V12l-2-4h10l-2 4v10"/>
                <path d="M9 16h6"/>
              </svg>
            </div>
          </div>
        </l-icon>
        <l-popup><strong>{{ multimodalData.startLocation || transferData.startName || 'Plecare' }}</strong></l-popup>
      </l-marker>

      <!-- Marker stație îmbarcare (albastru) -->
      <l-marker
        v-if="(showMultimodal || showTransfer) && (showTransfer ? transferData.boardingStation : multimodalData.boardingStation)"
        :lat-lng="showTransfer
          ? [transferData.boardingStation!.latitude, transferData.boardingStation!.longitude]
          : [props.allStations.find(s => s.name === multimodalData.boardingStation)?.latitude ?? 0,
             props.allStations.find(s => s.name === multimodalData.boardingStation)?.longitude ?? 0]"
      >
        <l-icon :icon-size="[26, 35]" :icon-anchor="[13, 35]">
          <div class="trip-pin boarding-pin">
            <div class="pin-bubble">
              <svg viewBox="0 0 24 24" fill="white" width="12" height="12">
                <rect x="3" y="7" width="18" height="10" rx="2"/>
                <path d="M3 11h18" stroke="#3b82f6" stroke-width="1.2" fill="none"/>
                <circle cx="7.5" cy="19" r="1.5" fill="white"/>
                <circle cx="16.5" cy="19" r="1.5" fill="white"/>
                <rect x="7" y="17" width="10" height="4" fill="white" rx="1"/>
                <rect x="7" y="4" width="2" height="3" rx="1" fill="white"/>
                <rect x="15" y="4" width="2" height="3" rx="1" fill="white"/>
              </svg>
            </div>
            <div class="pin-tail boarding-tail"></div>
          </div>
        </l-icon>
        <l-popup>
          <strong style="color:#3b82f6">Urcare</strong><br>
          {{ showTransfer ? transferData.boardingStation?.name : multimodalData.boardingStation }}
        </l-popup>
      </l-marker>

      <!-- Marker stație transfer (distinct, portocaliu) -->
      <l-marker
        v-if="showTransfer && transferData.transferStation"
        :lat-lng="[transferData.transferStation.latitude, transferData.transferStation.longitude]"
      >
        <l-icon :icon-size="[36, 36]" :icon-anchor="[18, 18]">
          <div class="trip-pin transfer-pin">
            <div class="pin-bubble">
              <svg viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14">
                <path d="M7 16V4l-4 4M17 8v12l4-4"/>
              </svg>
            </div>
          </div>
        </l-icon>
        <l-popup>
          <strong style="color:#f97316">Transfer</strong><br>
          {{ transferData.transferStation.name }}
        </l-popup>
      </l-marker>

      <!-- Marker stație al doilea transfer (aceeași iconiță) -->
      <l-marker
        v-if="showTransfer && transferData.transferStation2"
        :lat-lng="[transferData.transferStation2.latitude, transferData.transferStation2.longitude]"
      >
        <l-icon :icon-size="[36, 36]" :icon-anchor="[18, 18]">
          <div class="trip-pin transfer-pin secondary-transfer-pin">
            <div class="pin-bubble">
              <svg viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14">
                <path d="M7 16V4l-4 4M17 8v12l4-4"/>
              </svg>
            </div>
          </div>
        </l-icon>
        <l-popup>
          <strong style="color:#f97316">Transfer 2</strong><br>
          {{ transferData.transferStation2.name }}
        </l-popup>
      </l-marker>

      <!-- Marker stație coborâre (verde) -->
      <l-marker
        v-if="(showMultimodal || showTransfer) && (showTransfer ? transferData.alightingStation : multimodalData.alightingStation)"
        :lat-lng="showTransfer
          ? [transferData.alightingStation!.latitude, transferData.alightingStation!.longitude]
          : [props.allStations.find(s => s.name === multimodalData.alightingStation)?.latitude ?? 0,
             props.allStations.find(s => s.name === multimodalData.alightingStation)?.longitude ?? 0]"
      >
        <l-icon :icon-size="[26, 35]" :icon-anchor="[13, 35]">
          <div class="trip-pin alighting-pin">
            <div class="pin-bubble">
              <svg viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="12" height="12">
                <polyline points="20 6 9 17 4 12"/>
              </svg>
            </div>
            <div class="pin-tail alighting-tail"></div>
          </div>
        </l-icon>
        <l-popup>
          <strong style="color:#10b981">Coborâre</strong><br>
          {{ showTransfer ? transferData.alightingStation?.name : multimodalData.alightingStation }}
        </l-popup>
      </l-marker>

      <!-- Marker destinație plan -->
      <l-marker
        v-if="(showMultimodal || showTransfer) && savedDestination"
        :lat-lng="[savedDestination.lat, savedDestination.lon]"
      >
        <l-icon :icon-size="[30, 41]" :icon-anchor="[15, 41]">
          <div class="trip-pin dest-pin">
            <div class="pin-bubble">
              <svg viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13">
                <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"/>
                <line x1="4" y1="22" x2="4" y2="15"/>
              </svg>
            </div>
            <div class="pin-tail dest-tail"></div>
          </div>
        </l-icon>
        <l-popup><strong>{{ savedDestination.name }}</strong></l-popup>
      </l-marker>

      <!-- Autobuzele LIVE sunt gestionate imperativ (watch pe liveBuses) pentru animație smooth -->

    </l-map>

  </div>

</template>



<script setup lang="ts">

import { ref, computed, onMounted, onActivated, onUnmounted, watch, nextTick } from 'vue'
import { useRouter, useRoute } from 'vue-router'
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
import { useLanguage } from '@/composables/useLanguage'
import { reverseGeocode } from '@/services/geocodingService'
import { useFavorites } from '@/composables/useFavorites'

const router = useRouter()

// Dark mode
const { isDarkMode, toggleDarkMode } = useDarkMode()
const { currentLanguage, toggleLanguage, t } = useLanguage()

// Navigation functions
const goToFavorites = () => {
  router.push('/favorites')
}

const goToStatistics = () => {
  router.push('/statistics')
}

const goToTickets = () => {
  router.push('/tickets')
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
let authCheckIntervalId: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  // Initial auth check
  checkAuthStatus()

  // Check notifications support
  const diagnostics = checkNotificationSupport()
  if (!diagnostics.supported) {
  }

  // Check auth status every 2 seconds to detect login changes
  authCheckIntervalId = setInterval(() => {
    const wasAuthenticated = isAuthenticated.value
    checkAuthStatus()
    if (!wasAuthenticated && isAuthenticated.value) {
    }
  }, 2000)
})

onUnmounted(() => {
  if (authCheckIntervalId !== null) clearInterval(authCheckIntervalId)
})

// Also check when component becomes active (after navigation, keep-alive)
onActivated(() => {
  checkAuthStatus()
  nextTick(() => {
    if (map.value && map.value.leafletObject) {
      map.value.leafletObject.invalidateSize()
    }
  })
})

// Force map refresh when returning to home route (panel closes)
const currentRoute = useRoute()
watch(() => currentRoute.path, (newPath) => {
  if (newPath === '/') {
    nextTick(() => {
      if (map.value?.leafletObject) {
        map.value.leafletObject.invalidateSize()
        // Re-attach bus layer if it was detached
        if (!busLayerAdded && !showMultimodal.value && !showTransfer.value) {
          busLayerGroup.addTo(map.value.leafletObject)
          busLayerAdded = true
        }
      }
    })
  }
})

// Am scos 'leaflet/dist/leaflet.css' de aici, deoarece este deja în main.ts

// Interface pentru datele autobuzelor live
interface BusLocation {
  id: string
  latitude: number
  longitude: number
  routeId: number
  routeNumber?: string
  routeName?: string
  timestamp: number
  speed: number
  heading: number
  occupancy?: number
  status?: string
  directionId?: number
  nextStationId?: number
  nextStationName?: string
  nextStationEta?: number
}

// API Base URL
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

// Props

interface Props {

  stations?: Station[]

  allStations?: Station[]

  routeColor?: string

  selectedRouteId?: number | null

  tripPlan?: PlanResult | null

}



const props = withDefaults(defineProps<Props>(), {

  stations: () => [],

  allStations: () => [],

  routeColor: '#2563eb', // Albastru

  selectedRouteId: null,

  tripPlan: null

})

// Emit events
const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  sidebarToggle: [visible: boolean]
  pinSetOrigin: [location: { lat: number; lon: number; name: string }]
  pinSetDestination: [location: { lat: number; lon: number; name: string }]
}>()

// State pentru a verifica dacă componenta este gata

const isReady = ref(false)

// State pentru traseul calculat pe străzi

const routePath = ref<[number, number][]>([])

const isLoadingRoute = ref(false)

// Ref pentru polyline pentru a adăuga săgeți

const routePolylineRef = ref<any>(null)

const map = ref<any>(null)
const pendingMapCenter = ref<[number, number] | null>(null)
const pendingMapZoom = ref<number>(13)

// State pentru locația utilizatorului
const userLocation = ref<{lat: number, lon: number} | null>(null)

// State pentru adresa selectată
const selectedAddress = ref<{lat: number, lon: number, name: string} | null>(null)

// State pentru pin pe hartă
const pinnedLocation = ref<{lat: number, lon: number, name: string} | null>(null)
const isPinLoading = ref(false)
const { addFavorite } = useFavorites()

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
const thirdBusPath = ref<[number, number][]>([])

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
const notificationFeedback = ref<{ message: string; type: 'success' | 'error' | 'info' } | null>(null)
let notificationFeedbackTimeout: number | null = null

// State pentru rute alternative
const alternativeRoutes = ref<any[]>([])
const showAlternatives = ref(false)
const selectedAlternative = ref<any | null>(null)

// State pentru istoric călătorii
const showTripHistory = ref(false)

// State pentru meniu expandabil
const showMoreMenu = ref(false)
const showAllBuses = ref(false)

// Toast notifications
const toastMessage = ref('')
const toastType = ref<'success' | 'error' | 'info'>('success')
let toastTimeout: ReturnType<typeof setTimeout> | null = null

const showToast = (message: string, type: 'success' | 'error' | 'info' = 'success') => {
  toastMessage.value = message
  toastType.value = type
  if (toastTimeout) clearTimeout(toastTimeout)
  toastTimeout = setTimeout(() => { toastMessage.value = '' }, 3000)
}

// Close route view — reset route path and show buses again
const closeRouteView = () => {
  routePath.value = []
}

// Close more menu when clicking outside
const handleGlobalClick = (e: MouseEvent) => {
  const target = e.target as HTMLElement
  if (!target.closest('.more-btn') && !target.closest('.more-menu')) {
    showMoreMenu.value = false
  }
}
onMounted(() => document.addEventListener('click', handleGlobalClick))
onUnmounted(() => document.removeEventListener('click', handleGlobalClick))

// State pentru afișarea/ascunderea sidebar-ului
const isMobile = ref(window.innerWidth < 768)
const showSidebar = ref(window.innerWidth >= 768)
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
  busTime: 0,
  busStationsCount: 0,
  busStartTime: '',
  busEndTime: '',
  departureTime: '',
  arrivalTime: ''
})

// State pentru panoul de traseu cu transfer
const showTransfer = ref(false)
const transferData = ref({
  startName: '',
  endName: '',
  boardingStation: null as Station | null,
  transferStation: null as Station | null,
  transferStation2: null as Station | null,
  alightingStation: null as Station | null,
  route1Number: '',
  route1Color: '#3b82f6',
  route1StationsCount: 0,
  route2Number: '',
  route2Color: '#3b82f6',
  route2StationsCount: 0,
  route3Number: '',
  route3Color: '#dc2626',
  route3StationsCount: 0,
  firstWalkDistance: 0,
  firstWalkTime: 0,
  busTime1: 0,
  busTime2: 0,
  busTime3: 0,
  secondWalkDistance: 0,
  secondWalkTime: 0,
  bus1StartTime: '',
  bus1EndTime: '',
  bus2StartTime: '',
  bus2EndTime: '',
  bus3StartTime: '',
  bus3EndTime: '',
  departureTime: '',
  arrivalTime: ''
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

// Debug: monitor Firebase connection
import { onValue } from 'firebase/database'
onValue(busLocationsRef, (snapshot) => {
  const val = snapshot.val()
  const count = val ? Object.keys(val).length : 0
  console.log(`🔥 Firebase onValue: ${count} entries, exists=${snapshot.exists()}`)
}, (error) => {
  console.error('🔥 Firebase ERROR:', error)
})

watch(busLocationsData, (val) => {
  console.log('🔥 busLocationsData changed:', val ? Object.keys(val).length + ' keys' : 'null/undefined', typeof val)
}, { immediate: true })

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
  
  // Procesare — doar intrări active (format route_X_bus_Y de la noul simulator)
  for (let i = 0; i < entries.length; i++) {
    const [id, data] = entries[i] as [string, any]
    if (!id.startsWith('route_')) continue
    if (data?.latitude && data?.longitude) {
      buses.push({
        id,
        latitude: data.latitude,
        longitude: data.longitude,
        routeId: data.routeId,
        routeNumber: data.routeNumber,
        routeName: data.routeName,
        timestamp: data.timestamp,
        speed: data.speed,
        heading: data.heading,
        occupancy: data.occupancy,
        status: data.status,
        directionId: data.directionId,
        nextStationId: data.nextStationId,
        nextStationName: data.nextStationName,
        nextStationEta: data.nextStationEta
      })
    }
  }
  
  const activeRoute = props.selectedRouteId

  // Dacă nu avem locația utilizatorului, returnăm primele N + toate autobuzele de pe ruta selectată
  if (!userLocation.value?.lat || !userLocation.value?.lon) {
    if (showAllBuses.value) return buses
    if (activeRoute != null) {
      const onRoute = buses.filter(b => b.routeId === activeRoute)
      const others = buses.filter(b => b.routeId !== activeRoute).slice(0, 30)
      return [...onRoute, ...others]
    }
    return buses.slice(0, 30)
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

  // Sortare după distanță
  busesNearby.sort((a, b) => a.distance - b.distance)
  if (showAllBuses.value) return busesNearby
  // Asigură-te că autobuzele de pe ruta selectată sunt mereu incluse
  if (activeRoute != null) {
    const onRoute = busesNearby.filter(b => b.routeId === activeRoute)
    const others = busesNearby.filter(b => b.routeId !== activeRoute).slice(0, 30)
    return [...onRoute, ...others]
  }
  return busesNearby.slice(0, 30)
})

// ========================
// AUTOBUZE LIVE — markeri imperativi Leaflet
// Când Firebase trimite o poziție nouă → setLatLng direct.
// CSS transition 0.5s face glisarea vizuală. Simplu.
// ========================
const busLayerGroup = L.layerGroup()
const busMarkerInstances = new Map<string, L.Marker>()
let busLayerAdded = false

function renderBusMarkers(buses: BusLocation[]) {
  if (!map.value?.leafletObject) return
  const leafletMap = map.value.leafletObject

  if (!busLayerAdded) {
    busLayerGroup.addTo(leafletMap)
    busLayerAdded = true
  }

  const currentIds = new Set<string>()
  const activeRouteId = props.selectedRouteId

  for (const bus of buses) {
    currentIds.add(bus.id)
    const isOnSelectedRoute = activeRouteId != null && bus.routeId === activeRouteId
    const busColor = routeColors.value[bus.routeId] || '#2563eb'
    const existing = busMarkerInstances.get(bus.id)

    const icon = L.divIcon({
      className: isOnSelectedRoute ? 'bus-marker-animated bus-marker-highlighted' : (activeRouteId != null ? 'bus-marker-animated bus-marker-dimmed' : 'bus-marker-animated'),
      html: isOnSelectedRoute
        ? `<div class="bus-highlight-ring" style="border-color:${busColor}"><img src="/front-of-bus.png" style="width:24px;height:24px;" /><span class="bus-route-label" style="background:${busColor}">${bus.routeNumber || bus.routeId}</span></div>`
        : `<img src="/front-of-bus.png" style="width:20px;height:20px;" />`,
      iconSize: isOnSelectedRoute ? [36, 36] : [20, 20],
      iconAnchor: isOnSelectedRoute ? [18, 18] : [10, 10]
    })

    if (existing) {
      existing.setLatLng([bus.latitude, bus.longitude])
      existing.setIcon(icon)
      existing.setZIndexOffset(isOnSelectedRoute ? 1000 : 0)
      const popup = existing.getPopup()
      if (popup?.isOpen()) popup.setContent(buildBusPopupHTML(bus))
    } else {
      const marker = L.marker([bus.latitude, bus.longitude], { icon, zIndexOffset: isOnSelectedRoute ? 1000 : 0 })
      marker.bindPopup(buildBusPopupHTML(bus))
      marker.addTo(busLayerGroup)
      busMarkerInstances.set(bus.id, marker)
    }
  }

  for (const [id, marker] of busMarkerInstances) {
    if (!currentIds.has(id)) {
      marker.remove()
      busMarkerInstances.delete(id)
    }
  }
}

watch(liveBuses, (buses) => {
  renderBusMarkers(buses)
}, { deep: true })

watch(() => props.selectedRouteId, () => {
  renderBusMarkers(liveBuses.value)
})

// Toggle vizibilitate: ascunde autobuzele doar pentru direcții multimodale/transfer
watch([showMultimodal, showTransfer], ([multi, transfer]) => {
  if (!map.value?.leafletObject) return
  if (multi || transfer) {
    busLayerGroup.removeFrom(map.value.leafletObject)
    busLayerAdded = false
  } else {
    busLayerGroup.addTo(map.value.leafletObject)
    busLayerAdded = true
  }
})

// Generează HTML-ul popup-ului pentru un autobuz
const getStatusLabel = (status: string | undefined): string => {
  switch (status) {
    case 'at_station': return 'La stație'
    case 'approaching': return 'Se apropie de stație'
    case 'departing': return 'Pleacă din stație'
    case 'in_transit': return 'În tranzit'
    default: return 'În tranzit'
  }
}

const getStatusColor = (status: string | undefined): string => {
  switch (status) {
    case 'at_station': return '#16a34a'
    case 'approaching': return '#f59e0b'
    case 'departing': return '#3b82f6'
    default: return '#6b7280'
  }
}

const formatEta = (seconds: number | undefined): string => {
  if (!seconds || seconds <= 0) return ''
  if (seconds < 60) return `< 1 min`
  const mins = Math.round(seconds / 60)
  return `~${mins} min`
}

const buildBusPopupHTML = (bus: any): string => {
  const color = getBusColor(bus.routeId)
  const occupancyLabel = getOccupancyLabel(bus.occupancy)
  const occupancyClass = getOccupancyClass(bus.occupancy)
  const occupancy = bus.occupancy ?? 0
  const speed = bus.speed?.toFixed(1) ?? '0.0'
  const routeLabel = bus.routeNumber ? `Linia ${bus.routeNumber}` : `Traseu ${bus.routeId}`
  const routeNameStr = bus.routeName ? `<br><small style="color:#6b7280">${bus.routeName}</small>` : ''
  const directionStr = bus.directionId === 1 ? 'Retur' : 'Tur'
  const statusLabel = getStatusLabel(bus.status)
  const statusColor = getStatusColor(bus.status)
  const etaStr = formatEta(bus.nextStationEta)
  const nextStationStr = bus.nextStationName
    ? `<div style="margin:4px 0;padding:4px 6px;background:#f0f9ff;border-radius:4px;font-size:12px">
        <span style="color:#64748b">Următoarea:</span> <strong>${bus.nextStationName}</strong>
        ${etaStr ? `<span style="color:#2563eb;margin-left:4px">${etaStr}</span>` : ''}
      </div>`
    : ''

  return `<div class="bus-popup">
    <strong style="color: ${color}">${routeLabel}</strong>${routeNameStr}<br>
    <span style="display:inline-block;padding:1px 6px;border-radius:8px;font-size:11px;color:white;background:${statusColor};margin:3px 0">${statusLabel}</span>
    <span style="display:inline-block;padding:1px 6px;border-radius:8px;font-size:11px;color:#475569;background:#e2e8f0;margin:3px 0">${directionStr}</span><br>
    <small>Viteză: ${speed} km/h</small><br>
    ${nextStationStr}
    <div class="occupancy-indicator ${occupancyClass}">
      <span class="occupancy-icon">👥</span>
      <span class="occupancy-text">${occupancyLabel}</span>
      <div class="occupancy-bar">
        <div class="occupancy-fill" style="width: ${occupancy}%"></div>
      </div>
    </div>
  </div>`
}

// Cleanup la unmount
onUnmounted(() => {
  busMarkerInstances.forEach(m => m.remove())
  busMarkerInstances.clear()
  busLayerGroup.clearLayers()
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
const onMapReady = (mapInstance: any) => {
  if (pendingMapCenter.value) {
    mapInstance.setView(pendingMapCenter.value, pendingMapZoom.value, { animate: false })
    pendingMapCenter.value = null
  }

  mapInstance.on('click', async (e: any) => {
    const { lat, lng } = e.latlng
    isPinLoading.value = true
    pinnedLocation.value = { lat, lon: lng, name: 'Se încarcă...' }
    const address = await reverseGeocode(lat, lng)
    pinnedLocation.value = { lat, lon: lng, name: address }
    isPinLoading.value = false
  })
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

  pendingMapCenter.value = [lat, lon]
  pendingMapZoom.value = newZoom

  if (map.value?.leafletObject) {
    map.value.leafletObject.setView([lat, lon], newZoom, { animate: true })
    pendingMapCenter.value = null
  }

}

// ========== PIN ACTIONS ==========

const pinNavigateHere = () => {
  if (!pinnedLocation.value) return
  const dest = { lat: pinnedLocation.value.lat, lon: pinnedLocation.value.lon, name: pinnedLocation.value.name }
  pinnedLocation.value = null
  emit('pinSetDestination', dest)
}

const pinSearchFrom = () => {
  if (!pinnedLocation.value) return
  const loc = { lat: pinnedLocation.value.lat, lon: pinnedLocation.value.lon, name: pinnedLocation.value.name }
  pinnedLocation.value = null
  emit('pinSetOrigin', loc)
}

const pinAddFavorite = async () => {
  if (!pinnedLocation.value) return
  await addFavorite({
    name: pinnedLocation.value.name,
    address: pinnedLocation.value.name,
    lat: pinnedLocation.value.lat,
    lon: pinnedLocation.value.lon,
    type: 'custom',
    icon: '📍'
  })
  pinnedLocation.value = null
}

const pinClose = () => {
  pinnedLocation.value = null
}

// Handler pentru locația găsită de LocationButton
const handleLocationFound = (lat: number, lon: number) => {
  userLocation.value = { lat, lon }
  centerMap(lat, lon, 18)

  // Reaplică centrul după ce markerul și layout-ul sunt actualizate.
  nextTick(() => {
    window.setTimeout(() => {
      centerMap(lat, lon, 18)
    }, 200)
  })

  // Activează automat panelul cu stații apropiate
  showNearbyStations.value = true
}

// Handler pentru stația selectată din EnhancedSearch
const handleStationSelected = (station: Station) => {
  pinnedLocation.value = null
  centerMap(station.latitude, station.longitude, 16)
}

const handleStationNotificationRequested = async (station: Station) => {
  centerMap(station.latitude, station.longitude, 17)
  showNearbyStations.value = true
  await handleNotificationToggle({ stationId: station.id })
}

// Handler pentru adresa selectată
const handleAddressSelected = (location: { lat: number; lon: number; name: string }) => {
  selectedAddress.value = location
  pinnedLocation.value = null
  centerMap(location.lat, location.lon, 15)
}

const showAddressLocation = (location: { lat: number; lon: number; name: string }) => {
  selectedAddress.value = location
  centerMap(location.lat, location.lon, 17)
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
    // 1) Încearcă mai întâi algoritmul backend (Dijkstra)
    try {
      const backendAlternatives = await apiService.calculateRouteAlternatives(startStationId, endStationId)
      if (Array.isArray(backendAlternatives) && backendAlternatives.length > 0) {
        return backendAlternatives
          .sort((a, b) => (a.totalDuration ?? 0) - (b.totalDuration ?? 0))
          .slice(0, 3)
          .map((route, idx) => ({
            ...route,
            routeRank: route.routeRank ?? idx + 1,
            routeCategory: route.routeCategory || (
              idx === 0
                ? 'Cea mai rapidă'
                : route.routeType === 'direct'
                  ? 'Fără transfer'
                  : 'Variantă alternativă'
            )
          }))
      }
    } catch (error) {
      // Fallback local dacă endpoint-ul nu este disponibil.
    }

    // 2) Fallback local (estimare simplificată)
    
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
// Trunchiază shape-ul rutei între două stații (backend returnează ruta completă)
const trimShapeToStations = (
  points: Array<{latitude: number, longitude: number}>,
  from: {latitude: number, longitude: number},
  to: {latitude: number, longitude: number}
): [number, number][] => {
  if (!points || points.length === 0) return []
  let fromIdx = 0, fromDist = Infinity, toIdx = points.length - 1, toDist = Infinity
  for (let i = 0; i < points.length; i++) {
    const point = points[i]
    if (!point) continue
    const dFrom = Math.hypot(point.latitude - from.latitude, point.longitude - from.longitude)
    const dTo   = Math.hypot(point.latitude - to.latitude,   point.longitude - to.longitude)
    if (dFrom < fromDist) { fromDist = dFrom; fromIdx = i }
    if (dTo   < toDist)   { toDist   = dTo;   toIdx   = i }
  }
  const start = Math.min(fromIdx, toIdx)
  const end   = Math.max(fromIdx, toIdx)
  return points.slice(start, end + 1).map(p => [p.latitude, p.longitude] as [number, number])
}

const handleAlternativeRouteSelected = async (route: any) => {
  selectedAlternative.value = route
  showAlternatives.value = false
  
  // Salvează călătoria în istoric
  saveTripToHistory(route)
  
  // Curățăm traseele anterioare
  walkingPath.value = []
  secondWalkingPath.value = []
  thirdBusPath.value = []
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
      
      let busPart: [number, number][] = gtfsSegment?.points
        ? trimShapeToStations(gtfsSegment.points, segment.startStation, segment.endStation)
        : []
      
      
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
      
      // Setăm segmentul de autobuz (doar între stațiile de urcare și coborâre)
      walkingPath.value = busPart

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
        busTime: segment.duration || 0,
        busStationsCount: segment.stationCount || 0,
        busStartTime: segment.startTime || '',
        busEndTime: segment.endTime || '',
        departureTime: route.departureTime || '',
        arrivalTime: route.arrivalTime || ''
      }

      showMultimodal.value = true
      
      // Centrează harta pe traseu complet
      if (completeRoutePath.value.length > 0) {
        const bounds = L.latLngBounds(completeRoutePath.value)
        map.value?.leafletObject?.fitBounds(bounds, { padding: [50, 50] })
      }
      
    } catch (error) {
    }
    
  } else if (busSegments.length >= 2) {
    // RUTĂ CU TRANSFER (2 sau 3 autobuze)
    const segment1 = busSegments[0]
    const segment2 = busSegments[1]
    const segment3 = busSegments[2] || null
    const transferSegment = route.segments.find((s: any) => s.type === 'transfer')

    const buildBusPart = async (busSegment: any): Promise<[number, number][]> => {
      const routes = await apiService.getRoutes()
      const foundRoute = routes.find(r => r.routeNumber === busSegment.routeNumber)
      if (!foundRoute) return []

      let busPart: [number, number][] = []
      try {
        const gtfsSegment = await apiService.getRouteSegment(
          foundRoute.id,
          busSegment.startStation.id,
          busSegment.endStation.id
        )
        busPart = gtfsSegment?.points
          ? trimShapeToStations(gtfsSegment.points, busSegment.startStation, busSegment.endStation)
          : []
      } catch {}

      if (busPart.length < 2) {
        const coords = `${busSegment.startStation.longitude},${busSegment.startStation.latitude};${busSegment.endStation.longitude},${busSegment.endStation.latitude}`
        const url = `https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson`
        try {
          const resp = await fetch(url)
          const data = await resp.json()
          if (data.code === 'Ok' && data.routes?.[0]) {
            busPart = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch {}
      }

      return busPart
    }

    try {
      const busPart1 = await buildBusPart(segment1)
      const busPart2 = await buildBusPart(segment2)
      const busPart3 = segment3 ? await buildBusPart(segment3) : []

      // Calculăm traseele de mers pe jos
      if (savedUserLocation.value && savedDestination.value) {
        try {
          const url1 = `https://router.project-osrm.org/route/v1/foot/${savedUserLocation.value.lon},${savedUserLocation.value.lat};${segment1.startStation.longitude},${segment1.startStation.latitude}?overview=full&geometries=geojson`
          const resp1 = await fetch(url1)
          const data1 = await resp1.json()
          if (data1.code === 'Ok' && data1.routes?.[0]) {
            actualWalkingPath.value = data1.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch {}

        const lastSegmentEnd = segment3 ? segment3.endStation : segment2.endStation
        try {
          const url2 = `https://router.project-osrm.org/route/v1/foot/${lastSegmentEnd.longitude},${lastSegmentEnd.latitude};${savedDestination.value.lon},${savedDestination.value.lat}?overview=full&geometries=geojson`
          const resp2 = await fetch(url2)
          const data2 = await resp2.json()
          if (data2.code === 'Ok' && data2.routes?.[0]) {
            actualSecondWalkingPath.value = data2.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
          }
        } catch {}
      }

      // Construiește traseul COMPLET
      completeRoutePath.value = [
        ...actualWalkingPath.value,
        ...busPart1,
        ...busPart2,
        ...busPart3,
        ...actualSecondWalkingPath.value
      ]

      // Setăm segmentele pe hartă
      walkingPath.value = busPart1
      secondWalkingPath.value = busPart2
      thirdBusPath.value = busPart3

      const firstTransferStation = transferSegment?.station || segment1.endStation
      const secondTransferStation = segment3 ? segment2.endStation : null
      const finalSegment = segment3 || segment2

      transferData.value = {
        startName: 'Locația ta',
        endName: 'Destinația',
        boardingStation: segment1.startStation,
        transferStation: firstTransferStation,
        transferStation2: secondTransferStation,
        alightingStation: finalSegment.endStation,
        route1Number: segment1.routeNumber,
        route1Color: segment1.color || '#3b82f6',
        route1StationsCount: segment1.stationCount || 0,
        route2Number: segment2.routeNumber,
        route2Color: segment2.color || '#10b981',
        route2StationsCount: segment2.stationCount || 0,
        route3Number: segment3?.routeNumber || '',
        route3Color: segment3?.color || '#dc2626',
        route3StationsCount: segment3?.stationCount || 0,
        firstWalkDistance: 0,
        firstWalkTime: 0,
        busTime1: segment1.duration || 0,
        busTime2: segment2.duration || 0,
        busTime3: segment3?.duration || 0,
        secondWalkDistance: 0,
        secondWalkTime: 0,
        bus1StartTime: segment1.startTime || '',
        bus1EndTime: segment1.endTime || '',
        bus2StartTime: segment2.startTime || '',
        bus2EndTime: segment2.endTime || '',
        bus3StartTime: segment3?.startTime || '',
        bus3EndTime: segment3?.endTime || '',
        departureTime: route.departureTime || '',
        arrivalTime: route.arrivalTime || ''
      }

      showTransfer.value = true
      showMultimodal.value = false

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
            multimodalBusPath.value = trimShapeToStations(shapeData.points, startStation, endStation)
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
          busTime: Math.max(stationsBetween * 2, 2),
          busStationsCount: stationsBetween,
          busStartTime: '',
          busEndTime: '',
          departureTime: '',
          arrivalTime: ''
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
            walkingPath.value = trimShapeToStations(segment1.points, startStation, transferStation)
          }

          // Al doilea segment de autobuz (transfer → end)
          const segment2 = await apiService.getRouteSegment(route2.id, transferStation.id, endStation.id)
          if (segment2 && segment2.points && segment2.points.length > 0) {
            secondWalkingPath.value = trimShapeToStations(segment2.points, transferStation, endStation)
          }
        } catch (error) {
        }
        
        // Populăm datele pentru panoul de transfer
        transferData.value = {
          startName: userLoc.name || 'Locația ta',
          endName: destination.name || 'Destinația',
          boardingStation: startStation,
          transferStation: transferStation,
          transferStation2: null,
          alightingStation: endStation,
          route1Number: route1.routeNumber,
          route1Color: routeColors.value[route1.id] || '#3b82f6',
          route1StationsCount: route1StationsCount,
          route2Number: route2.routeNumber,
          route2Color: routeColors.value[route2.id] || '#10b981',
          route2StationsCount: route2StationsCount,
          route3Number: '',
          route3Color: '#dc2626',
          route3StationsCount: 0,
          firstWalkDistance: routingData.firstDistance,
          firstWalkTime: routingData.firstTime,
          busTime1: calculateBusTime(route1StationsCount),
          busTime2: calculateBusTime(route2StationsCount),
          busTime3: 0,
          secondWalkDistance: routingData.secondDistance,
          secondWalkTime: routingData.secondTime,
          bus1StartTime: '',
          bus1EndTime: '',
          bus2StartTime: '',
          bus2EndTime: '',
          bus3StartTime: '',
          bus3EndTime: '',
          departureTime: '',
          arrivalTime: ''
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
  thirdBusPath.value = []
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
    busTime: 0,
    busStationsCount: 0,
    busStartTime: '',
    busEndTime: '',
    departureTime: '',
    arrivalTime: ''
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
  thirdBusPath.value = []
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
    transferStation2: null,
    alightingStation: null,
    route1Number: '',
    route1Color: '#3b82f6',
    route1StationsCount: 0,
    route2Number: '',
    route2Color: '#3b82f6',
    route2StationsCount: 0,
    route3Number: '',
    route3Color: '#dc2626',
    route3StationsCount: 0,
    firstWalkDistance: 0,
    firstWalkTime: 0,
    busTime1: 0,
    busTime2: 0,
    busTime3: 0,
    secondWalkDistance: 0,
    secondWalkTime: 0,
    bus1StartTime: '',
    bus1EndTime: '',
    bus2StartTime: '',
    bus2EndTime: '',
    bus3StartTime: '',
    bus3EndTime: '',
    departureTime: '',
    arrivalTime: ''
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
  thirdBusPath.value = []
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
  const planBusSegments = (plan.busSegments ?? []).filter(segment => segment.routeId > 0)

  const resolveStationByName = (name?: string): Station | null => {
    if (!name) return null
    return props.allStations.find(station => station.name === name) ?? null
  }

  const firstBusSegment = planBusSegments[0] ?? null
  const secondBusSegment = planBusSegments[1] ?? null
  const thirdBusSegment = planBusSegments[2] ?? null

  const firstTransferStation = transfer
    ?? resolveStationByName(firstBusSegment?.endStationName)
  const secondTransferStation = thirdBusSegment
    ? resolveStationByName(secondBusSegment?.endStationName)
    : null

  const buildBusSegmentPath = async (routeId: number, fromStation: Station, toStation: Station) => {
    let segmentPath: [number, number][] = []
    try {
      const segment = await apiService.getRouteSegment(routeId, fromStation.id, toStation.id)
      if (segment?.points?.length) {
        segmentPath = trimShapeToStations(segment.points, fromStation, toStation)
      }
    } catch {}

    if (segmentPath.length < 2) {
      try {
        const coords = `${fromStation.longitude},${fromStation.latitude};${toStation.longitude},${toStation.latitude}`
        const response = await fetch(`https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson`)
        const data = await response.json()
        if (data.code === 'Ok' && data.routes?.[0]) {
          segmentPath = data.routes[0].geometry.coordinates.map((c: number[]) => [c[1], c[0]] as [number, number])
        }
      } catch {}
    }

    return segmentPath
  }

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

  // 2. Segmentele de autobuz (1, 2 și 3 dacă există)
  const firstBusRouteId = firstBusSegment?.routeId ?? plan.route1Id
  const firstBusEndStation = firstTransferStation ?? alighting

  if (firstBusRouteId > 0 && firstBusEndStation) {
    const busSegment1Path = await buildBusSegmentPath(firstBusRouteId, boarding, firstBusEndStation)
    if (busSegment1Path.length > 0) {
      walkingPath.value = busSegment1Path
      allPoints.push(...busSegment1Path)
    }
  }

  const secondBusRouteId = secondBusSegment?.routeId ?? plan.route2Id ?? 0
  const secondBusStartStation = firstTransferStation
  const secondBusEndStation = secondTransferStation ?? alighting

  if (plan.type === 'transfer' && secondBusRouteId > 0 && secondBusStartStation) {
    const busSegment2Path = await buildBusSegmentPath(secondBusRouteId, secondBusStartStation, secondBusEndStation)
    if (busSegment2Path.length > 0) {
      secondWalkingPath.value = busSegment2Path
      allPoints.push(...busSegment2Path)
    }
  }

  const thirdBusRouteId = thirdBusSegment?.routeId ?? 0
  if (plan.type === 'transfer' && thirdBusRouteId > 0 && secondTransferStation) {
    const busSegment3Path = await buildBusSegmentPath(thirdBusRouteId, secondTransferStation, alighting)
    if (busSegment3Path.length > 0) {
      thirdBusPath.value = busSegment3Path
      allPoints.push(...busSegment3Path)
    }
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
    // Stil identic cu varianta de rută alternativă directă: segmentul autobuzului
    // este păstrat în walkingPath (linie albastră groasă), cu panel multimodal.
    multimodalBusPath.value = []

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
      busTime: plan.stationsBetween * 2,
      busStationsCount: plan.stationsBetween ?? 0,
      busStartTime: '',
      busEndTime: '',
      departureTime: '',
      arrivalTime: ''
    }
    showMultimodal.value = true
  } else if (plan.type === 'transfer' && transfer) {
    const route2Color = secondBusSegment?.color || plan.route2Color || '#10b981'
    const route3Color = thirdBusSegment?.color || '#dc2626'
    const route2Stations = secondBusSegment?.stationCount ?? plan.route2StationsCount ?? 0
    const route3Stations = thirdBusSegment?.stationCount ?? 0

    transferData.value = {
      startName: plan.originName,
      endName: plan.destName,
      boardingStation: boarding,
      transferStation: firstTransferStation,
      transferStation2: secondTransferStation,
      alightingStation: alighting,
      route1Number: plan.route1Number,
      route1Color: plan.route1Color || '#3b82f6',
      route1StationsCount: plan.route1StationsCount ?? 0,
      route2Number: secondBusSegment?.routeNumber || plan.route2Number || '',
      route2Color,
      route2StationsCount: route2Stations,
      route3Number: thirdBusSegment?.routeNumber || '',
      route3Color,
      route3StationsCount: route3Stations,
      firstWalkDistance: 0,
      firstWalkTime: plan.walkToStartMinutes ?? 0,
      busTime1: (plan.route1StationsCount ?? 0) * 2,
      busTime2: route2Stations * 2,
      busTime3: route3Stations * 2,
      secondWalkDistance: 0,
      secondWalkTime: plan.walkToEndMinutes ?? 0,
      bus1StartTime: '',
      bus1EndTime: '',
      bus2StartTime: '',
      bus2EndTime: '',
      bus3StartTime: '',
      bus3EndTime: '',
      departureTime: '',
      arrivalTime: ''
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

const showNotificationFeedback = (
  message: string,
  type: 'success' | 'error' | 'info' = 'info'
) => {
  notificationFeedback.value = { message, type }

  if (notificationFeedbackTimeout) {
    clearTimeout(notificationFeedbackTimeout)
  }

  notificationFeedbackTimeout = window.setTimeout(() => {
    notificationFeedback.value = null
    notificationFeedbackTimeout = null
  }, 4200)
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
  showAddressLocation,
  setTripMode,
  setSidebarOpen: (open: boolean) => { showSidebar.value = open },
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
const handleNotificationToggle = async (payload: { stationId: number; routeId?: number }) => {
  const { stationId, routeId } = payload

  try {
    const notificationSettings = getNotificationSettings()
    const routeMatches = (routeId ?? null) === (notificationSettings.routeId ?? null)
    
    if (notificationSettings.enabled && notificationSettings.stationId === stationId && routeMatches) {
      // Dezactivează notificările
      disableNotifications()
      showNotificationFeedback('Notificările au fost dezactivate.', 'info')
    } else {
      // Verifică mai întâi suportul pentru notificări
      if (!('Notification' in window)) {
        showNotificationFeedback(
          'Browserul nu suportă notificări. Încearcă Chrome, Firefox sau Edge.',
          'error'
        )
        return
      }
      
      // Activează notificări
      const success = await enableNotifications(stationId, routeId)
      
      if (success) {
        showNotificationFeedback(
          routeId
            ? `Notificări active pe stație pentru Linia ${routeId}.`
            : 'Notificări active pentru toate liniile din stație.',
          'success'
        )
      } else {
        showNotificationFeedback(
          'Nu s-au putut activa notificările. Permite notificările în browser și încearcă din nou.',
          'error'
        )
      }
    }
  } catch (error) {
    showNotificationFeedback(
      'Eroare la notificări. Verifică permisiunea browserului și folosește HTTPS/localhost.',
      'error'
    )
  }
}

// Calcul ETA pentru autobuze care vin către o stație
// Optimizat cu memoization și cache persistent
const stationETACache = new Map<string, Array<{ busId: string, routeId: number, routeNumber: string, eta: string, color: string }>>()
let etaCacheTimeout: number | null = null
const ETA_CACHE_DURATION = 5000 // 5 secunde

/** Compass bearing (0-360°) from point A to point B */
const calculateBearing = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const dLon = (lon2 - lon1) * Math.PI / 180
  const lat1R = lat1 * Math.PI / 180
  const lat2R = lat2 * Math.PI / 180
  const y = Math.sin(dLon) * Math.cos(lat2R)
  const x = Math.cos(lat1R) * Math.sin(lat2R) - Math.sin(lat1R) * Math.cos(lat2R) * Math.cos(dLon)
  return (Math.atan2(y, x) * 180 / Math.PI + 360) % 360
}

/** Smallest absolute angle between two headings (0–180°) */
const angleDiff = (a: number, b: number): number =>
  Math.abs(((a - b) + 540) % 360 - 180)

const getStationETAs = (stationId: number) => {
  // Cache per 5-second bucket
  const now = Date.now()
  const cacheKey = `${stationId}-${now - (now % ETA_CACHE_DURATION)}`
  if (stationETACache.has(cacheKey)) return stationETACache.get(cacheKey)!

  const allStations = props.allStations
  if (!allStations?.length) return []

  const station = allStations.find(s => s.id === stationId)
  if (!station) return []

  const stationLat = station.latitude
  const stationLon = station.longitude
  const buses = liveBuses.value

  // Keep only the closest approaching bus per route
  const routeBest = new Map<number, { busId: string; distKm: number; speed: number }>()

  for (const bus of buses) {
    if (typeof bus.latitude !== 'number' || typeof bus.longitude !== 'number') continue

    const distKm = calculateDistance(bus.latitude, bus.longitude, stationLat, stationLon)

    // Ignore buses more than 4 km away or already past/at the stop (<30 m)
    if (distKm > 4 || distKm < 0.03) continue

    // Direction check — skip buses heading away (>90° off course)
    if (typeof bus.heading === 'number') {
      const bearing = calculateBearing(bus.latitude, bus.longitude, stationLat, stationLon)
      if (angleDiff(bus.heading, bearing) > 90) continue
    }

    const existing = routeBest.get(bus.routeId)
    if (!existing || distKm < existing.distKm) {
      routeBest.set(bus.routeId, { busId: bus.id, distKm, speed: bus.speed ?? 0 })
    }
  }

  const etas: Array<{ busId: string; routeId: number; routeNumber: string; eta: string; color: string }> = []

  for (const [routeId, { busId, distKm, speed }] of routeBest) {
    // Use actual speed if meaningful, otherwise default to 25 km/h city average
    const avgSpeed = speed > 5 ? Math.min(speed, 50) : 25
    const etaMin = Math.round((distKm / avgSpeed) * 60)
    if (etaMin > 30) continue

    etas.push({
      busId,
      routeId,
      routeNumber: `Linia ${routeId}`,
      eta: etaMin <= 1 ? '<1 min' : `${etaMin} min`,
      color: routeColors.value[routeId] || '#2563eb'
    })
  }

  // Sort by ETA ascending, cap at 8 entries
  etas.sort((a, b) => {
    const ta = a.eta === '<1 min' ? 0 : parseInt(a.eta)
    const tb = b.eta === '<1 min' ? 0 : parseInt(b.eta)
    return ta - tb
  })
  const result = etas.slice(0, 8)

  stationETACache.set(cacheKey, result)
  if (stationETACache.size > 20) {
    const oldest = stationETACache.keys().next().value
    if (oldest) stationETACache.delete(oldest)
  }

  return result
}
</script>

<style scoped>
.map-container {
  height: 100dvh;
  width: 100%;
  position: relative;
  overflow: hidden;
  background:
    radial-gradient(circle at 20% 20%, rgba(59, 130, 246, 0.16), transparent 28%),
    radial-gradient(circle at 80% 15%, rgba(139, 92, 246, 0.14), transparent 24%),
    radial-gradient(circle at 50% 85%, rgba(16, 185, 129, 0.10), transparent 22%),
    var(--bg-secondary);
}

.map-container::before {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  background:
    linear-gradient(180deg, rgba(255,255,255,0.05), transparent 18%),
    linear-gradient(90deg, rgba(255,255,255,0.03), transparent 20%, rgba(255,255,255,0.02));
  opacity: 0.75;
  z-index: 1;
}

.map-aura {
  position: absolute;
  inset: 0;
  pointer-events: none;
  background:
    radial-gradient(circle at 16% 18%, rgba(59, 130, 246, 0.10), transparent 18%),
    radial-gradient(circle at 85% 78%, rgba(249, 115, 22, 0.08), transparent 20%);
  z-index: 1;
}

.map-hud {
  position: fixed;
  left: 76px;
  bottom: 18px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  z-index: 1150;
  max-width: min(520px, calc(100vw - 100px));
  transition: left 0.25s ease, max-width 0.25s ease;
}

.map-hud--sidebar-open {
  left: 356px;
  max-width: min(520px, calc(100vw - 392px));
}

.hud-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.72);
  color: white;
  backdrop-filter: blur(14px);
  border: 1px solid rgba(255, 255, 255, 0.12);
  box-shadow: 0 12px 30px rgba(15, 23, 42, 0.18);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.01em;
}

.hud-chip-primary {
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.9), rgba(139, 92, 246, 0.88));
}

.hud-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  box-shadow: 0 0 0 4px rgba(255, 255, 255, 0.08);
}

.hud-dot.live { background: #22c55e; }
.hud-dot.nearby { background: #60a5fa; }
.hud-dot.route { background: #f97316; }

@media (max-width: 767px) {
  .map-hud,
  .map-hud--sidebar-open {
    left: 12px;
    right: 12px;
    bottom: 84px;
    max-width: calc(100vw - 24px);
  }
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
  z-index: 900;
  display: grid;
  grid-template-columns: repeat(3, 40px);
  gap: 8px;
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

.action-btn.language-btn {
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.04em;
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

/* More Menu Dropdown */
.more-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 6px;
  background: var(--bg-primary);
  border-radius: 12px;
  box-shadow: var(--shadow-lg);
  min-width: 200px;
  padding: 6px;
  z-index: 100;
  border: 1px solid var(--border-primary);
}

.more-menu-item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 10px 14px;
  border: none;
  background: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  color: var(--text-primary);
  transition: background 0.15s;
}

.more-menu-item:hover {
  background: var(--bg-secondary);
}

.more-menu-item--danger {
  color: var(--danger, #ef4444);
}

.more-menu-item--danger:hover {
  background: #fef2f2;
}

.more-menu-icon {
  font-size: 16px;
  width: 20px;
  text-align: center;
}

.more-menu-divider {
  height: 1px;
  background: var(--border-primary);
  margin: 4px 8px;
}

.fade-enter-active, .fade-leave-active {
  transition: opacity 0.15s, transform 0.15s;
}
.fade-enter-from, .fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

/* Toast Notifications */
.toast-notification {
  position: fixed;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  padding: 12px 24px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  z-index: 9999;
  box-shadow: var(--shadow-lg);
  pointer-events: none;
}

.toast--success {
  background: #16a34a;
  color: white;
}

.toast--error {
  background: #ef4444;
  color: white;
}

.toast--info {
  background: #3b82f6;
  color: white;
}

.toast-enter-active, .toast-leave-active {
  transition: opacity 0.3s, transform 0.3s;
}
.toast-enter-from, .toast-leave-to {
  opacity: 0;
  transform: translate(-50%, -20px);
}

/* Close route button */
.close-route-btn {
  position: absolute;
  top: 14px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 90;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 18px;
  background: var(--bg-primary);
  color: var(--text-primary);
  border: none;
  border-radius: 20px;
  box-shadow: var(--shadow-lg);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.close-route-btn:hover {
  background: #ef4444;
  color: white;
}

/* Responsive mobile */
@media (max-width: 900px) {
  .sidebar-toggle-btn {
    top: 10px;
    left: 10px;
    width: 36px;
    height: 36px;
  }

  .top-right-buttons {
    top: 10px;
    right: 10px;
    grid-template-columns: repeat(3, 44px);
    gap: 6px;
  }

  .top-right-buttons.top-right-buttons--docked {
    top: auto;
    bottom: 12px;
    right: 10px;
  }

  .action-btn {
    width: 44px;
    height: 44px;
  }

  .action-btn.language-btn {
    font-size: 11px;
  }

  .action-btn svg {
    width: 20px;
    height: 20px;
  }
}

/* Trip plan markers - modern pin style */
.trip-pin {
  display: flex;
  flex-direction: column;
  align-items: center;
  filter: drop-shadow(0 2px 5px rgba(0,0,0,0.22));
}

.pin-bubble {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  border: 2px solid rgba(255,255,255,0.95);
}

.pin-tail {
  width: 0;
  height: 0;
  border-left: 5px solid transparent;
  border-right: 5px solid transparent;
  margin-top: -1px;
}

/* Origin – slate circle, no tail */
.origin-pin .pin-bubble {
  width: 26px;
  height: 26px;
  background: #475569;
  box-shadow: 0 1px 5px rgba(71,85,105,0.35);
}

/* Boarding – blue teardrop pin */
.boarding-pin .pin-bubble {
  width: 24px;
  height: 24px;
  background: #3b82f6;
  box-shadow: 0 1px 5px rgba(59,130,246,0.4);
}
.boarding-tail {
  border-top: 9px solid #3b82f6;
}

/* Transfer – orange circle with ring, no tail */
.transfer-pin .pin-bubble {
  width: 30px;
  height: 30px;
  background: #f97316;
  box-shadow: 0 0 0 4px rgba(249,115,22,0.18), 0 1px 5px rgba(249,115,22,0.4);
}

.secondary-transfer-pin .pin-bubble {
  box-shadow: 0 0 0 4px rgba(249,115,22,0.25), 0 1px 7px rgba(249,115,22,0.5);
}

/* Alighting – green teardrop pin */
.alighting-pin .pin-bubble {
  width: 24px;
  height: 24px;
  background: #10b981;
  box-shadow: 0 1px 5px rgba(16,185,129,0.4);
}
.alighting-tail {
  border-top: 9px solid #10b981;
}

/* Destination – red teardrop pin */
.dest-pin .pin-bubble {
  width: 28px;
  height: 28px;
  background: #ef4444;
  box-shadow: 0 1px 6px rgba(239,68,68,0.45);
}
.dest-tail {
  border-top: 11px solid #ef4444;
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

.notification-feedback {
  position: fixed;
  top: 82px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1400;
  padding: 10px 16px;
  border-radius: 999px;
  font-size: 13px;
  font-weight: 600;
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.18);
  border: 1px solid transparent;
}

.notification-feedback.success {
  background: #ecfdf5;
  color: #065f46;
  border-color: #6ee7b7;
}

.notification-feedback.error {
  background: #fef2f2;
  color: #991b1b;
  border-color: #fca5a5;
}

.notification-feedback.info {
  background: #eff6ff;
  color: #1e40af;
  border-color: #93c5fd;
}
</style>

<!-- CSS non-scoped pentru markeri Leaflet (generați în afara arborelui Vue) -->
<style>
/* Markeri autobuze live */
.bus-marker-animated {
  background: transparent !important;
  border: none !important;
  /* 0.5s ease-out pe transform (translate3d setat de Leaflet la setLatLng).
     !important bate orice "transition: all" din stiluri globale. */
  transition: transform 0.5s ease-out !important;
  will-change: transform;
}
.bus-marker-animated img {
  display: block;
  transition: none !important;
}

.bus-marker-highlighted {
  z-index: 1000 !important;
}

.bus-marker-dimmed {
  opacity: 0.3;
  filter: grayscale(0.8);
}

.bus-highlight-ring {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  border: 3px solid #3b82f6;
  background: rgba(255,255,255,0.95);
  box-shadow: 0 0 10px rgba(59,130,246,0.5), 0 2px 8px rgba(0,0,0,0.2);
  position: relative;
  animation: busPulse 2s ease-in-out infinite;
}

.bus-route-label {
  position: absolute;
  bottom: -10px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 9px;
  font-weight: 800;
  color: white;
  padding: 1px 5px;
  border-radius: 6px;
  white-space: nowrap;
  line-height: 1.3;
  box-shadow: 0 1px 3px rgba(0,0,0,0.3);
}

@keyframes busPulse {
  0%, 100% { box-shadow: 0 0 10px rgba(59,130,246,0.5), 0 2px 8px rgba(0,0,0,0.2); }
  50% { box-shadow: 0 0 20px rgba(59,130,246,0.7), 0 2px 12px rgba(0,0,0,0.3); }
}

/* Pin popup styles */
.pin-popup {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.pin-popup-address {
  margin-bottom: 8px;
  font-size: 13px;
  line-height: 1.3;
  color: #1f2937;
  word-break: break-word;
}

.pin-popup-actions {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.pin-action-btn {
  display: block;
  width: 100%;
  padding: 7px 10px;
  border: none;
  border-radius: 6px;
  background: #eff6ff;
  color: #1d4ed8;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  text-align: left;
  transition: background 0.15s;
}

.pin-action-btn:hover {
  background: #dbeafe;
}

.pin-action-btn--close {
  background: #fef2f2;
  color: #dc2626;
}

.pin-action-btn--close:hover {
  background: #fee2e2;
}
</style>