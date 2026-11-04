<template>
  <aside class="sidebar" :aria-label="t('planning')">

    <!-- Header -->
    <header class="sidebar-header">
      <span class="brand-mark" aria-hidden="true"><BusFront :size="18" /></span>
      <span class="brand-name">Tursib</span>
      <span class="live-badge">{{ t('live') }}</span>
    </header>

    <!-- Tab Bar -->
    <nav class="tab-bar" role="tablist">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        type="button"
        role="tab"
        :aria-selected="activeTab === tab.id"
        @click="activeTab = tab.id"
        :class="['tab-btn', { active: activeTab === tab.id }]"
      >
        <component :is="tab.icon" :size="18" aria-hidden="true" />
        <span class="tab-label">{{ tab.label }}</span>
      </button>
    </nav>

    <!-- ==================== TAB: TRASEE ==================== -->
    <div v-if="activeTab === 'routes'" class="tab-content">
      <div v-if="loading" class="center-state">
        <div class="spinner"></div>
        <p>{{ t('loadingRoutes') }}</p>
      </div>

      <div v-else-if="error" class="center-state error-state">
        <AlertTriangle :size="28" aria-hidden="true" />
        <p>{{ error }}</p>
        <button type="button" @click="loadRoutes" class="btn-secondary-sm">{{ t('retry') }}</button>
      </div>

      <div v-else class="routes-section">
        <p class="section-hint">{{ t('selectRouteHint') }}</p>

        <button
          v-for="route in routes"
          :key="route.id"
          type="button"
          @click="selectRoute(route.id)"
          class="route-item"
          :class="{ active: selectedRouteId === route.id }"
          :aria-pressed="selectedRouteId === route.id"
        >
          <span class="line-pill" :style="{ background: route.color || '#5f6672' }">{{ route.routeNumber }}</span>
          <span class="route-name">{{ route.name }}</span>
          <Check v-if="selectedRouteId === route.id" :size="18" class="route-check" aria-hidden="true" />
        </button>
      </div>

      <!-- Stații traseu selectat -->
      <transition name="slide-down">
        <div v-if="selectedRouteId && (loadingStations || currentStations.length > 0)" class="stations-section">
          <div class="section-title">
            <span>{{ t('routeStations') }}</span>
            <span class="count" v-if="!loadingStations">{{ currentStations.length }}</span>
          </div>
          <div v-if="loadingStations" class="loading-small">
            <div class="spinner-small"></div> {{ t('loading') }}
          </div>
          <ol v-else class="stations-list">
            <li v-for="station in currentStations" :key="station.id" class="station-row">
              <span class="station-dot" aria-hidden="true"></span>
              <span class="station-name">{{ station.name }}</span>
            </li>
          </ol>
        </div>
      </transition>
    </div>

    <!-- ==================== TAB: ORAR ==================== -->
    <div v-if="activeTab === 'schedule'" class="tab-content">

      <div class="search-box">
        <label class="field field--search">
          <Search :size="18" class="field-icon" aria-hidden="true" />
          <span class="sr-only">{{ t('searchStationPlaceholder') }}</span>
          <input
            v-model="scheduleQuery"
            @input="onScheduleInput"
            :placeholder="t('searchStationPlaceholder')"
            class="field-input"
            autocomplete="off"
          />
          <button v-if="scheduleQuery" type="button" @click="clearSchedule" class="icon-btn" :aria-label="t('close')">
            <X :size="16" aria-hidden="true" />
          </button>
        </label>

        <div v-if="filteredScheduleStations.length > 0 && !selectedScheduleStation" class="autocomplete">
          <button
            v-for="s in filteredScheduleStations.slice(0, 7)"
            :key="s.id"
            type="button"
            @click="selectScheduleStation(s)"
            class="autocomplete-item"
          >
            <Bus :size="16" class="ac-icon" aria-hidden="true" />
            <span class="ac-name">{{ s.name }}</span>
          </button>
        </div>
      </div>

      <div v-if="selectedScheduleStation" class="schedule-panel">
        <div class="board-header">
          <span class="board-eyebrow">{{ t('stationLabel') }}</span>
          <h2 class="board-title">{{ selectedScheduleStation.name }}</h2>
        </div>

        <div v-if="loadingSchedule" class="center-state">
          <div class="spinner"></div>
          <p>{{ t('loadingSchedule') }}</p>
        </div>

        <div v-else-if="scheduleETAs.length === 0" class="center-state">
          <Clock :size="28" aria-hidden="true" />
          <p>{{ t('noServicesNextHour') }}</p>
        </div>

        <ul v-else class="departures">
          <li
            v-for="eta in scheduleETAs"
            :key="`${eta.routeNumber}-${eta.arrivalTime}`"
            class="departure"
          >
            <span class="line-pill line-pill--lg" :style="{ background: eta.color }">{{ eta.routeNumber }}</span>
            <span class="departure-info">
              <span class="departure-dest">{{ eta.direction || eta.routeName }}</span>
              <span class="departure-sub">{{ eta.routeName }}</span>
            </span>
            <span class="departure-time" :class="{ urgent: eta.countdown < 120 }">{{ formatETA(eta.countdown) }}</span>
          </li>
        </ul>
      </div>

      <div v-else-if="!scheduleQuery" class="empty-hero">
        <Clock :size="36" class="empty-hero-icon" aria-hidden="true" />
        <h3>{{ t('realtimeSchedule') }}</h3>
        <p>{{ t('searchStationScheduleHint') }}</p>
      </div>
    </div>

    <!-- ==================== TAB: PLANIFICARE ==================== -->
    <div v-if="activeTab === 'plan'" class="tab-content">

      <section class="plan-form">
        <h2 class="plan-title">{{ t('whereTo') }}</h2>

        <div class="endpoints">
          <div class="endpoints-rail" aria-hidden="true">
            <span class="rail-start"></span>
            <span class="rail-line"></span>
            <span class="rail-end"></span>
          </div>

          <div class="endpoints-fields">
            <!-- Plecare -->
            <div class="endpoint">
              <div v-if="planOrigin" class="field field--filled">
                <span class="field-text">
                  <span class="field-label">{{ t('fieldFrom') }}</span>
                  <span class="field-value">{{ planOrigin.name }}</span>
                </span>
                <button type="button" @click="clearOrigin" class="icon-btn" :aria-label="t('close')"><X :size="16" aria-hidden="true" /></button>
              </div>
              <label v-else class="field">
                <span class="field-text">
                  <span class="field-label">{{ t('fieldFrom') }}</span>
                  <input v-model="planOriginQuery" @input="debouncedOriginInput"
                    :placeholder="t('originPlaceholder')" class="field-input" autocomplete="off" />
                </span>
                <button v-if="planOriginQuery" type="button" @click="clearOrigin" class="icon-btn" :aria-label="t('close')"><X :size="16" aria-hidden="true" /></button>
              </label>
              <div v-if="!planOrigin && originSuggestions.length > 0" class="autocomplete">
                <button v-for="s in originSuggestions" :key="`${s.type}-${s.name}`" type="button"
                  @click="selectOrigin(s)" class="autocomplete-item">
                  <component :is="s.type === 'station' ? Bus : MapPin" :size="16" class="ac-icon" aria-hidden="true" />
                  <span class="ac-text">
                    <span class="ac-name">{{ s.name }}</span>
                    <span v-if="s.subtext" class="ac-sub">{{ s.subtext }}</span>
                  </span>
                </button>
              </div>
            </div>

            <!-- Destinație -->
            <div class="endpoint">
              <div v-if="planDest" class="field field--filled">
                <span class="field-text">
                  <span class="field-label">{{ t('fieldTo') }}</span>
                  <span class="field-value">{{ planDest.name }}</span>
                </span>
                <button type="button" @click="clearDest" class="icon-btn" :aria-label="t('close')"><X :size="16" aria-hidden="true" /></button>
              </div>
              <label v-else class="field">
                <span class="field-text">
                  <span class="field-label">{{ t('fieldTo') }}</span>
                  <input v-model="planDestQuery" @input="debouncedDestInput"
                    :placeholder="t('destinationPlaceholder')" class="field-input" autocomplete="off" />
                </span>
                <button v-if="planDestQuery" type="button" @click="clearDest" class="icon-btn" :aria-label="t('close')"><X :size="16" aria-hidden="true" /></button>
              </label>
              <div v-if="!planDest && destSuggestions.length > 0" class="autocomplete">
                <button v-for="s in destSuggestions" :key="`${s.type}-${s.name}`" type="button"
                  @click="selectDest(s)" class="autocomplete-item">
                  <component :is="s.type === 'station' ? Bus : MapPin" :size="16" class="ac-icon" aria-hidden="true" />
                  <span class="ac-text">
                    <span class="ac-name">{{ s.name }}</span>
                    <span v-if="s.subtext" class="ac-sub">{{ s.subtext }}</span>
                  </span>
                </button>
              </div>
            </div>
          </div>

          <button type="button" class="swap-btn" @click="swapEndpoints" :aria-label="t('swapEndpoints')">
            <ArrowUpDown :size="18" aria-hidden="true" />
          </button>
        </div>

        <!-- Când -->
        <div class="segmented" role="group" :aria-label="t('when')">
          <button type="button" :class="{ active: planMode === 'leaveNow' }" @click="planMode = 'leaveNow'">{{ t('leaveNow') }}</button>
          <button type="button" :class="{ active: planMode === 'departAt' }" @click="planMode = 'departAt'">{{ t('departAt') }}</button>
          <button type="button" :class="{ active: planMode === 'arriveBy' }" @click="planMode = 'arriveBy'">{{ t('arriveBy') }}</button>
        </div>

        <div class="datetime-row">
          <label class="field field--compact">
            <span class="field-text">
              <span class="field-label">{{ t('date') }}</span>
              <input v-model="planDate" type="date" class="field-input" />
            </span>
          </label>
          <label v-if="planMode !== 'leaveNow'" class="field field--compact">
            <span class="field-text">
              <span class="field-label">{{ planMode === 'arriveBy' ? t('arrivalTime') : t('departureTime') }}</span>
              <input v-model="planTime" type="time" class="field-input" required />
            </span>
          </label>
        </div>

        <button type="button" @click="searchRoutes" :disabled="(!planOrigin && !planOriginQuery.trim()) || (!planDest && !planDestQuery.trim()) || (planMode !== 'leaveNow' && !planTime) || isSearching" class="btn-search-routes">
          <span v-if="isSearching" class="spinner-small spinner-small--light" aria-hidden="true"></span>
          <Search v-else :size="18" aria-hidden="true" />
          {{ isSearching ? t('searching') : t('searchTrips') }}
        </button>
      </section>

      <!-- Rezultate -->
      <section v-if="planResults.length > 0" class="plan-results" :aria-label="t('selectedResults')">
        <div class="results-header">
          <span class="section-title">{{ planResults.length }} {{ planResults.length === 1 ? t('resultSingular') : t('selectedResults') }}</span>
          <span class="results-sub">{{ planOrigin?.name }} → {{ planDest?.name }}</span>
        </div>

        <button
          v-for="(result, idx) in planResults"
          :key="idx"
          type="button"
          class="result-card"
          :class="{ selected: selectedResultIdx === idx }"
          @click="selectedResultIdx = idx; selectPlanResult(result)"
        >
          <span class="result-top">
            <span class="live-badge" v-if="result.minutesUntil <= 15">
              {{ result.minutesUntil <= 0 ? t('now') : t('inMinutes', 'in {n} min', { n: result.minutesUntil }) }}
            </span>
            <span v-else class="result-when">{{ t('inMinutes', 'in {n} min', { n: result.minutesUntil }) }}</span>
            <span v-if="idx === 0" class="result-tag">{{ t('fastest') }}</span>
          </span>

          <span class="result-times">
            <span class="time">{{ result.departureTime }}</span>
            <ArrowRight :size="18" class="time-arrow" aria-hidden="true" />
            <span class="time">{{ result.arrivalTime }}</span>
            <span class="duration">{{ tripDuration(result) }}</span>
          </span>

          <span class="result-legs">
            <template v-if="result.walkToStartMinutes">
              <span class="leg-walk"><Footprints :size="15" aria-hidden="true" />{{ result.walkToStartMinutes }}</span>
              <ChevronRight :size="14" class="leg-sep" aria-hidden="true" />
            </template>
            <template v-if="result.type === 'direct'">
              <span class="line-pill" :style="{ background: result.route1Color }">{{ result.route1Number }}</span>
            </template>
            <template v-else>
              <template v-for="(segment, segmentIndex) in result.busSegments" :key="`${segment.routeId}-${segmentIndex}`">
                <span class="line-pill" :style="{ background: segment.color }">{{ segment.routeNumber }}</span>
                <ChevronRight v-if="segmentIndex < result.busSegments.length - 1" :size="14" class="leg-sep" aria-hidden="true" />
              </template>
            </template>
            <template v-if="result.walkToEndMinutes">
              <ChevronRight :size="14" class="leg-sep" aria-hidden="true" />
              <span class="leg-walk"><Footprints :size="15" aria-hidden="true" />{{ result.walkToEndMinutes }}</span>
            </template>
          </span>

          <span class="result-meta">
            <template v-if="result.type === 'transfer'">
              {{ formatTransferCount(result.busSegments.length - 1) }} · {{ getTransferLabel(result) }}
            </template>
            <template v-else>{{ t('directRoute') }} · {{ result.stationsBetween }} {{ t('stations') }}</template>
          </span>

          <span v-if="selectedResultIdx === idx && (result.walkToStartMinutes || result.walkToEndMinutes)" class="result-steps">
            <span v-if="result.walkToStartMinutes" class="step">
              {{ t('walkToStation') }} <strong>{{ result.boardingStation.name }}</strong> ({{ result.walkToStartMinutes }} {{ t('minutes') }}).
            </span>
            <span v-if="result.walkToEndMinutes" class="step">
              {{ t('afterAlightingAt') }} <strong>{{ result.alightingStation.name }}</strong>, {{ t('walkMore') }} {{ result.walkToEndMinutes }} {{ t('minutes') }} {{ t('toDestination') }}
            </span>
          </span>
        </button>
      </section>

      <div v-else-if="searchDone" class="center-state">
        <SearchX :size="28" aria-hidden="true" />
        <p>{{ t('noTripsFound') }}</p>
        <p class="hint-text">{{ t('tryDifferentTime') }}</p>
      </div>

      <div v-else-if="!planOrigin && !planDest" class="empty-hero">
        <Compass :size="36" class="empty-hero-icon" aria-hidden="true" />
        <h3>{{ t('tripPlanningTitle') }}</h3>
        <p>{{ t('tripPlanningHint') }}</p>
      </div>
    </div>

  </aside>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, onUnmounted } from 'vue'
import apiService, { type Route, type Station, type StationScheduleEntry } from '@/services/apiService'
import { useLanguage } from '@/composables/useLanguage'
import { useStatistics } from '@/composables/useStatistics'
import { useRecentSearches } from '@/composables/useRecentSearches'
import { searchAddresses, getTypeIcon } from '@/services/geocodingService'
import {
  AlertTriangle, ArrowRight, ArrowUpDown, Bus, BusFront, Check, ChevronRight, Clock, Compass,
  Footprints, Map as MapIcon, MapPin, Route as RouteIcon, Search, SearchX, X
} from 'lucide-vue-next'

// Props
const props = defineProps<{
  allStations?: Station[]
}>()

const { t } = useLanguage()
const { recordRouteUsage, incrementTrips } = useStatistics()
const { addRecentSearch } = useRecentSearches()

// Emits
const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  planSelected: [plan: PlanResult]
}>()

// ===================== TABS =====================
type TabId = 'routes' | 'schedule' | 'plan'
const activeTab = ref<TabId>('plan')
const tabs = computed(() => [
  { id: 'plan' as TabId, icon: RouteIcon, label: t('planning') },
  { id: 'schedule' as TabId, icon: Clock, label: t('schedule') },
  { id: 'routes' as TabId, icon: MapIcon, label: t('routes') },
])

watch(activeTab, (newTab) => {
  if (newTab !== 'routes' && selectedRouteId.value !== null) {
    selectedRouteId.value = null
    currentStations.value = []
    emit('routeSelected', 0, [])
  }
})

// ===================== TAB: TRASEE =====================
const routes = ref<Route[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedRouteId = ref<number | null>(null)
const currentStations = ref<Station[]>([])
const loadingStations = ref(false)
const stationsCache = new Map<number, Station[]>()

const loadRoutes = async () => {
  loading.value = true
  error.value = null
  try {
    const cachedList = localStorage.getItem('routesList.v2')
    const cachedTs   = localStorage.getItem('routesList.v2.ts')
    if (cachedList && cachedTs && Date.now() - parseInt(cachedTs) < 30 * 60 * 1000) {
      routes.value = JSON.parse(cachedList)
      return
    }
    routes.value = await apiService.getRoutes()
    localStorage.setItem('routesList.v2', JSON.stringify(routes.value))
    localStorage.setItem('routesList.v2.ts', Date.now().toString())
  } catch {
    error.value = 'Nu s-au putut încărca traseele. Verifică dacă API-ul rulează.'
  } finally {
    loading.value = false
  }
}

const selectRoute = async (routeId: number) => {
  if (selectedRouteId.value === routeId) {
    selectedRouteId.value = null
    currentStations.value = []
    emit('routeSelected', 0, [])
    return
  }
  recordRouteUsage(routeId)
  selectedRouteId.value = routeId
  if (stationsCache.has(routeId)) {
    currentStations.value = stationsCache.get(routeId)!
    emit('routeSelected', routeId, currentStations.value)
    return
  }
  loadingStations.value = true
  try {
    const stations = await apiService.getRouteStations(routeId)
    stationsCache.set(routeId, stations)
    currentStations.value = stations
    emit('routeSelected', routeId, stations)
  } finally {
    loadingStations.value = false
  }
}

// ===================== TAB: ORAR =====================
interface ETAItem {
  routeNumber: string
  routeName: string
  direction: string
  arrivalTime: number
  countdown: number
  color: string
}

const scheduleQuery = ref('')
const selectedScheduleStation = ref<Station | null>(null)
const filteredScheduleStations = ref<Station[]>([])
const scheduleETAs = ref<ETAItem[]>([])
const loadingSchedule = ref(false)
let countdownTimer: number | null = null

const onScheduleInput = () => {
  selectedScheduleStation.value = null
  const q = scheduleQuery.value.toLowerCase().trim()
  filteredScheduleStations.value = q && props.allStations
    ? props.allStations.filter(s => s.name.toLowerCase().includes(q))
    : []
}

const selectScheduleStation = async (station: Station) => {
  selectedScheduleStation.value = station
  scheduleQuery.value = station.name
  filteredScheduleStations.value = []
  addRecentSearch({
    query: station.name,
    type: 'station',
    result: { name: station.name, lat: station.latitude, lon: station.longitude, stationId: station.id }
  })
  loadingSchedule.value = true
  try {
    const schedule = await apiService.getStationSchedule(station.id)
    buildETAs(schedule)
    startCountdown()
  } finally {
    loadingSchedule.value = false
  }
}

const buildETAs = (schedule: StationScheduleEntry[]) => {
  const now = new Date()
  const currentMinutes = now.getHours() * 60 + now.getMinutes()
  const etas: ETAItem[] = []
  const seen = new Set<string>()
  for (const entry of schedule) {
    const entryMinutes = parseTimeToMinutes(entry.departureTime)
    if (entryMinutes === null) continue
    const diffSeconds = (entryMinutes - currentMinutes) * 60
    if (diffSeconds < 0 || diffSeconds > 3600) continue
    const dedupeKey = `${entry.routeNumber}-${entry.departureTime}`
    if (seen.has(dedupeKey)) continue
    seen.add(dedupeKey)
    etas.push({
      routeNumber: entry.routeNumber,
      routeName:   entry.routeName,
      direction:   entry.direction || (entry.directionId === 0 ? 'Dus' : 'Întors'),
      arrivalTime: Date.now() + diffSeconds * 1000,
      countdown:   diffSeconds,
      color:       entry.routeColor || '#3b82f6',
    })
  }
  scheduleETAs.value = etas.sort((a, b) => a.arrivalTime - b.arrivalTime).slice(0, 10)
}

const startCountdown = () => {
  if (countdownTimer) clearInterval(countdownTimer)
  countdownTimer = window.setInterval(() => {
    const now = Date.now()
    scheduleETAs.value = scheduleETAs.value
      .map(e => ({ ...e, countdown: Math.max(0, Math.floor((e.arrivalTime - now) / 1000)) }))
      .filter(e => e.countdown > 0)
  }, 1000)
}

const formatETA = (seconds: number): string => {
  if (seconds < 60) return `${seconds}s`
  const m = Math.floor(seconds / 60)
  return m === 1 ? '1 min' : `${m} min`
}

const clearSchedule = () => {
  scheduleQuery.value = ''
  selectedScheduleStation.value = null
  filteredScheduleStations.value = []
  scheduleETAs.value = []
  if (countdownTimer) { clearInterval(countdownTimer); countdownTimer = null }
}

// ===================== TAB: PLANIFICARE =====================

interface PlanLocation {
  type: 'station' | 'address'
  name: string
  lat: number
  lon: number
  stationId?: number
}

interface Suggestion {
  type: 'station' | 'address'
  name: string
  lat: number
  lon: number
  stationId?: number
  icon?: string        // type icon from geocoding service
  subtext?: string     // suburb / city line
}

export interface PlanResult {
  type: 'direct' | 'transfer'
  route1Id: number
  route1Number: string
  route1Name: string
  route1Color: string
  stationsBetween: number
  route2Id?: number
  route2Number?: string
  route2Name?: string
  route2Color?: string
  route1StationsCount?: number
  route2StationsCount?: number
  transferStation?: Station
  departureTime: string
  arrivalTime: string
  minutesUntil: number
  boardingStation: Station
  alightingStation: Station
  originLat: number
  originLon: number
  originName: string
  destLat: number
  destLon: number
  destName: string
  walkToStartMinutes?: number
  walkToEndMinutes?: number
  busSegments: {
    routeId: number
    routeNumber: string
    routeName: string
    color: string
    startStationName: string
    endStationName: string
    stationCount: number
  }[]
}

const planOriginQuery   = ref('')
const planDestQuery     = ref('')
const planTime          = ref(new Date().toTimeString().substring(0, 5))
const planDate          = ref(new Date().toISOString().split('T')[0]) // Data selectată (azi implicit)
const planMode           = ref<'leaveNow' | 'departAt' | 'arriveBy'>('leaveNow')
const planOrigin        = ref<PlanLocation | null>(null)
const planDest          = ref<PlanLocation | null>(null)
const originSuggestions = ref<Suggestion[]>([])
const destSuggestions   = ref<Suggestion[]>([])
const planResults       = ref<PlanResult[]>([])
const isSearching       = ref(false)
const searchDone        = ref(false)

// Haversine distance in meters
const haversineM = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371000, dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180
  const a = Math.sin(dLat/2)**2 + Math.cos(lat1*Math.PI/180)*Math.cos(lat2*Math.PI/180)*Math.sin(dLon/2)**2
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
}
const walkMin = (lat1: number, lon1: number, lat2: number, lon2: number) =>
  Math.ceil(haversineM(lat1, lon1, lat2, lon2) * 1.3 / 83) // 5 km/h walking

const nearestStations = (lat: number, lon: number, n: number): Station[] => {
  if (!props.allStations?.length) return []
  return [...props.allStations]
    .sort((a, b) => haversineM(lat, lon, a.latitude, a.longitude) - haversineM(lat, lon, b.latitude, b.longitude))
    .slice(0, n)
}

const normalizeText = (value: string) =>
  value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')

const normalizeAddressInput = (value: string) => {
  let normalized = value.trim()
  // Corecteaza cateva greseli uzuale de tastare pentru adrese.
  normalized = normalized.replace(/\bstarfa\b/gi, 'strada')
  normalized = normalized.replace(/\bstarda\b/gi, 'strada')
  normalized = normalized.replace(/\bstr\.?\b/gi, 'strada')
  return normalized
}

const looksLikeAddress = (value: string) => {
  const v = normalizeText(value)
  const hasRoadWord = /\b(strada|str|bulevard|bd|aleea|piata|calea|nr)\b/.test(v)
  const hasHouseNumber = /\b\d+[a-z]?\b/.test(v)
  return hasRoadWord || hasHouseNumber
}

// Geocoding via shared service (Nominatim, exact address support)
const geocode = async (query: string): Promise<Suggestion[]> => {
  const results = await searchAddresses(query)
  return results.map(r => ({
    type: 'address' as const,
    name: r.displayName,
    lat: r.lat,
    lon: r.lon,
    icon: getTypeIcon(r.type),
    subtext: [r.addressDetails.suburb, r.addressDetails.city].filter(Boolean).join(', ') || undefined
  }))
}

const getSuggestions = async (query: string): Promise<Suggestion[]> => {
  const cleanedQuery = normalizeAddressInput(query)
  const q = cleanedQuery.toLowerCase().trim()
  if (q.length < 2) return []

  const geocoded = await geocode(cleanedQuery)
  const stationMatches: Suggestion[] = (props.allStations || [])
    .filter(s => s.name.toLowerCase().includes(q))
    .slice(0, 4)
    .map(s => ({ type: 'station', name: s.name, lat: s.latitude, lon: s.longitude, stationId: s.id }))

  if (looksLikeAddress(cleanedQuery)) {
    return geocoded.slice(0, 6)
  }

  return [...stationMatches, ...geocoded.slice(0, Math.max(0, 6 - stationMatches.length))]
}

let originDebounceTimer: number | null = null
let destDebounceTimer: number | null = null
const debouncedOriginInput = () => {
  planOrigin.value = null
  if (originDebounceTimer) clearTimeout(originDebounceTimer)
  if (!planOriginQuery.value.trim()) { originSuggestions.value = []; return }
  originDebounceTimer = window.setTimeout(async () => {
    originSuggestions.value = await getSuggestions(planOriginQuery.value)
  }, 300)
}
const debouncedDestInput = () => {
  planDest.value = null
  if (destDebounceTimer) clearTimeout(destDebounceTimer)
  if (!planDestQuery.value.trim()) { destSuggestions.value = []; return }
  destDebounceTimer = window.setTimeout(async () => {
    destSuggestions.value = await getSuggestions(planDestQuery.value)
  }, 300)
}

const selectOrigin = (s: Suggestion) => {
  planOrigin.value = s; planOriginQuery.value = ''; originSuggestions.value = []
  addRecentSearch({
    query: s.name,
    type: s.type === 'station' ? 'station' : 'address',
    result: { name: s.name, lat: s.lat, lon: s.lon, stationId: s.stationId }
  })
}
const selectDest = (s: Suggestion) => {
  planDest.value = s; planDestQuery.value = ''; destSuggestions.value = []
  addRecentSearch({
    query: s.name,
    type: s.type === 'station' ? 'station' : 'address',
    result: { name: s.name, lat: s.lat, lon: s.lon, stationId: s.stationId }
  })
}
const clearOrigin  = () => { planOrigin.value = null; planOriginQuery.value = ''; originSuggestions.value = [] }
const clearDest    = () => { planDest.value = null;   planDestQuery.value = '';   destSuggestions.value = [] }

// Inversează plecarea cu destinația (și textele căutate)
const swapEndpoints = () => {
  const origin = planOrigin.value
  const originQuery = planOriginQuery.value
  planOrigin.value = planDest.value
  planOriginQuery.value = planDestQuery.value
  planDest.value = origin
  planDestQuery.value = originQuery
  originSuggestions.value = []
  destSuggestions.value = []
}

// Varianta selectată din listă (evidențiată, cu pașii afișați)
const selectedResultIdx = ref<number | null>(null)

// Durata călătoriei din orele "HH:MM" (trece peste miezul nopții)
const tripDuration = (result: PlanResult): string => {
  const toMin = (hhmm: string) => {
    const [h, m] = hhmm.split(':').map(Number)
    return (h ?? 0) * 60 + (m ?? 0)
  }
  let minutes = toMin(result.arrivalTime) - toMin(result.departureTime)
  if (minutes < 0) minutes += 24 * 60
  return `${minutes} min`
}

const resolveTextQueryToLocation = async (query: string): Promise<Suggestion | null> => {
  const q = normalizeAddressInput(query)
  if (!q) return null

  // Daca seamana cu adresa, geocodam direct in lat/lon si folosim acea locatie.
  if (looksLikeAddress(q)) {
    const geocoded = await geocode(q)
    const firstGeocoded = geocoded[0]
    if (firstGeocoded) {
      return firstGeocoded
    }
  }

  const exactStation = (props.allStations || []).find(station => station.name.toLowerCase() === q.toLowerCase())
  if (exactStation) {
    return {
      type: 'station',
      name: exactStation.name,
      lat: exactStation.latitude,
      lon: exactStation.longitude,
      stationId: exactStation.id
    }
  }

  const suggestions = await getSuggestions(q)
  return suggestions[0] || null
}

const ensurePlanLocationsResolved = async (): Promise<boolean> => {
  if (!planOrigin.value && planOriginQuery.value.trim()) {
    const originResolved = await resolveTextQueryToLocation(planOriginQuery.value)
    if (originResolved) {
      planOrigin.value = originResolved
      planOriginQuery.value = ''
      originSuggestions.value = []
    }
  }

  if (!planDest.value && planDestQuery.value.trim()) {
    const destResolved = await resolveTextQueryToLocation(planDestQuery.value)
    if (destResolved) {
      planDest.value = destResolved
      planDestQuery.value = ''
      destSuggestions.value = []
    }
  }

  return Boolean(planOrigin.value && planDest.value)
}

const parseTimeToMinutes = (time: string): number | null => {
  const [hh, mm] = time.split(':')
  if (hh === undefined || mm === undefined) return null
  const h = Number(hh)
  const m = Number(mm)
  if (!Number.isFinite(h) || !Number.isFinite(m)) return null
  return h * 60 + m
}

const minutesToStr = (m: number) => `${String(Math.floor(m/60)%24).padStart(2,'0')}:${String(m%60).padStart(2,'0')}`

const buildDepartureDateTime = (time: string) => {
  const [hoursRaw, minutesRaw] = time.split(':')
  const hours = Number(hoursRaw)
  const minutes = Number(minutesRaw)
  const [year, month, day] = planDate.value!.split('-').map(Number)
  const departure = new Date(year!, month! - 1, day!, hours, minutes, 0, 0)
  return departure.toISOString()
}

let currentSearchId = 0

const searchRoutes = async () => {
  const hasLocations = await ensurePlanLocationsResolved()
  if (!hasLocations) return

  const mySearchId = ++currentSearchId
  isSearching.value = true; searchDone.value = false; planResults.value = []; selectedResultIdx.value = null

  try {
    const origin = planOrigin.value!, dest = planDest.value!
    const now = new Date()
    const nowMin = now.getHours() * 60 + now.getMinutes()
    const effectiveTime = planMode.value === 'leaveNow'
      ? `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
      : planTime.value
    const baseMin = parseTimeToMinutes(effectiveTime) ?? nowMin
    const targetArrivalMin = planMode.value === 'arriveBy'
      ? (parseTimeToMinutes(planTime.value) ?? nowMin)
      : null
    const primaryMaxWalkMinutes = 25
    const fallbackMaxWalkMinutes = 45

    const originLat = origin.type === 'station'
      ? (props.allStations!.find(s => s.id === origin.stationId)?.latitude ?? origin.lat)
      : origin.lat
    const originLon = origin.type === 'station'
      ? (props.allStations!.find(s => s.id === origin.stationId)?.longitude ?? origin.lon)
      : origin.lon

    // Căutare extinsă: adresele folosesc până la 20 stații apropiate,
    // apoi extindere progresivă (6 -> 12 -> 20) până găsim variante viabile.
    const boardingRaw: Station[] = origin.type === 'station'
      ? [props.allStations!.find(s => s.id === origin.stationId)!].filter(Boolean)
      : nearestStations(originLat, originLon, 20)
    const alightingRaw: Station[] = dest.type === 'station'
      ? [props.allStations!.find(s => s.id === dest.stationId)!].filter(Boolean)
      : nearestStations(dest.lat, dest.lon, 20)

    const boardingCandidatesPrimary = origin.type === 'station'
      ? boardingRaw
      : boardingRaw.filter(station => walkMin(originLat, originLon, station.latitude, station.longitude) <= primaryMaxWalkMinutes)
    const alightingCandidatesPrimary = dest.type === 'station'
      ? alightingRaw
      : alightingRaw.filter(station => walkMin(dest.lat, dest.lon, station.latitude, station.longitude) <= primaryMaxWalkMinutes)

    const finalBoardingCandidates = origin.type === 'station'
      ? boardingRaw
      : (boardingCandidatesPrimary.length > 0
          ? boardingCandidatesPrimary
          : boardingRaw.filter(station => walkMin(originLat, originLon, station.latitude, station.longitude) <= fallbackMaxWalkMinutes))

    const finalAlightingCandidates = dest.type === 'station'
      ? alightingRaw
      : (alightingCandidatesPrimary.length > 0
          ? alightingCandidatesPrimary
          : alightingRaw.filter(station => walkMin(dest.lat, dest.lon, station.latitude, station.longitude) <= fallbackMaxWalkMinutes))

    if (!finalBoardingCandidates.length || !finalAlightingCandidates.length) { searchDone.value = true; return }

    // Map: cheie_ruta → cel mai bun rezultat (walking minim)
    const bestByRoute = new Map<string, PlanResult>()
    const attemptedPairs = new Set<string>()
    const expansionStages = origin.type === 'station' || dest.type === 'station' ? [6, 12] : [6, 12, 20]

    for (const stageSize of expansionStages) {
      const stageBoarding = origin.type === 'station'
        ? finalBoardingCandidates
        : finalBoardingCandidates.slice(0, Math.min(stageSize, finalBoardingCandidates.length))
      const stageAlighting = dest.type === 'station'
        ? finalAlightingCandidates
        : finalAlightingCandidates.slice(0, Math.min(stageSize, finalAlightingCandidates.length))

      await Promise.all(stageBoarding.flatMap(boarding =>
        stageAlighting.map(async alighting => {
          if (boarding.id === alighting.id) return
          const pairKey = `${boarding.id}-${alighting.id}`
          if (attemptedPairs.has(pairKey)) return
          attemptedPairs.add(pairKey)

          try {
            // Pregătește opțiunile pentru backend în funcție de modul de căutare.
            // Pentru arriveBy, scade timpul de mers de la destinație (walkEnd) per pereche,
            // astfel încât ora de sosire afișată (incl. walking final) să fie <= target.
            let apiOptions: { mode: 'departAt' | 'arriveBy'; dateTime: string } | undefined
            const today = new Date().toISOString().split('T')[0]
            if (planMode.value === 'leaveNow' && planDate.value !== today) {
              // Utilizatorul a selectat o altă zi dar modul „Pleacă acum" — trimite data selectată cu ora curentă
              apiOptions = { mode: 'departAt', dateTime: buildDepartureDateTime(effectiveTime) }
            } else if (planMode.value === 'departAt') {
              apiOptions = { mode: 'departAt', dateTime: buildDepartureDateTime(planTime.value) }
            } else if (planMode.value === 'arriveBy' && targetArrivalMin !== null) {
              const walkEndMin = dest.type === 'address'
                ? walkMin(alighting.latitude, alighting.longitude, dest.lat, dest.lon)
                : 0
              const adjustedArrivalMin = targetArrivalMin - walkEndMin
              apiOptions = { mode: 'arriveBy', dateTime: buildDepartureDateTime(minutesToStr(Math.max(0, adjustedArrivalMin))) }
            }
            const routes = await apiService.calculateRouteAlternatives(boarding.id, alighting.id, apiOptions)

            for (const route of routes) {
              const busSegments = (route.segments as any[]).filter(s => s.type === 'bus')
              if (!busSegments.length) continue

              const firstBus = busSegments[0]
              const lastBus = busSegments[busSegments.length - 1]
              const firstBusStart = firstBus.startStation as Station | undefined
              const lastBusEnd = lastBus.endStation as Station | undefined

              const effectiveBoarding = origin.type === 'address'
                ? (firstBusStart ?? boarding)
                : (firstBusStart ?? boarding)
              const effectiveAlighting = dest.type === 'address'
                ? (lastBusEnd ?? alighting)
                : (lastBusEnd ?? alighting)

              const stationWalkStart = origin.type === 'station' && effectiveBoarding.id !== boarding.id
                ? walkMin(originLat, originLon, effectiveBoarding.latitude, effectiveBoarding.longitude)
                : 0
              const stationWalkEnd = dest.type === 'station' && effectiveAlighting.id !== alighting.id
                ? walkMin(effectiveAlighting.latitude, effectiveAlighting.longitude, dest.lat, dest.lon)
                : 0

              const walkStart = origin.type === 'address'
                ? walkMin(originLat, originLon, effectiveBoarding.latitude, effectiveBoarding.longitude)
                : stationWalkStart
              const walkEnd = dest.type === 'address'
                ? walkMin(effectiveAlighting.latitude, effectiveAlighting.longitude, dest.lat, dest.lon)
                : stationWalkEnd

              const isDirect = busSegments.length === 1
              const normalizedBusSegments = busSegments.map((segment: any) => ({
                routeId: segment.routeId ?? 0,
                routeNumber: segment.routeNumber ?? '',
                routeName: segment.routeName ?? '',
                color: segment.color ?? '#3b82f6',
                startStationName: segment.startStation?.name ?? '',
                endStationName: segment.endStation?.name ?? '',
                stationCount: segment.stationCount ?? 0
              }))

              let depMin: number
              let arrMin: number
              if (planMode.value === 'arriveBy' && targetArrivalMin !== null) {
                arrMin = targetArrivalMin
                depMin = arrMin - walkEnd - route.totalDuration - walkStart
              } else {
                depMin = baseMin + walkStart
                arrMin = depMin + route.totalDuration + walkEnd
              }
              const totalWalk = walkStart + walkEnd
              const minutesUntil = Math.max(0, depMin - nowMin)

              const transferKey = isDirect
                ? ''
                : normalizedBusSegments
                    .slice(0, -1)
                    .map(segment => segment.endStationName || String(segment.routeId))
                    .join('>')
              const chainKey = normalizedBusSegments
                .map(segment => segment.routeNumber || String(segment.routeId))
                .join('>')
              const key = isDirect
                ? `D-${chainKey}`
                : `T-${chainKey}-${transferKey}`

              const result: PlanResult = {
                type: isDirect ? 'direct' : 'transfer',
                route1Id: firstBus.routeId ?? 0,
                route1Number: firstBus.routeNumber ?? '',
                route1Name: firstBus.routeName ?? '',
                route1Color: firstBus.color ?? '#3b82f6',
                stationsBetween: busSegments.reduce((sum: number, s: any) => sum + (s.stationCount ?? 0), 0),
                boardingStation: effectiveBoarding,
                alightingStation: effectiveAlighting,
                originLat: origin.lat,
                originLon: origin.lon,
                originName: origin.name,
                destLat: dest.lat,
                destLon: dest.lon,
                destName: dest.name,
                walkToStartMinutes: walkStart > 0 ? walkStart : undefined,
                walkToEndMinutes: walkEnd > 0 ? walkEnd : undefined,
                departureTime: minutesToStr(depMin),
                arrivalTime: minutesToStr(arrMin),
                minutesUntil,
                busSegments: normalizedBusSegments
              }

              if (!isDirect && busSegments.length >= 2) {
                const secondBus = busSegments[1]
                result.route2Id = secondBus.routeId ?? 0
                result.route2Number = secondBus.routeNumber ?? ''
                result.route2Name = secondBus.routeName ?? ''
                result.route2Color = secondBus.color ?? '#10b981'
                result.route1StationsCount = firstBus.stationCount ?? 0
                result.route2StationsCount = secondBus.stationCount ?? 0
                result.transferStation = firstBus.endStation
              }

              const existing = bestByRoute.get(key)
              if (!existing) {
                bestByRoute.set(key, result)
              } else {
                const existingWalk = (existing.walkToStartMinutes ?? 0) + (existing.walkToEndMinutes ?? 0)
                if (totalWalk < existingWalk) bestByRoute.set(key, result)
              }
            }
          } catch (err: any) {
            const status = err?.response?.status
            if (status && status !== 400 && status !== 404) {
              console.warn(`Route search error (${status}) for pair ${boarding.id} → ${alighting.id}:`, err?.response?.data?.message || err.message)
            }
          }
        })
      ))

      if (bestByRoute.size >= 6) break
    }

    const results = [...bestByRoute.values()]

    // Sortare: direct primul, apoi walking minim, apoi plecare mai rapidă
    results.sort((a, b) => {
      if (a.type !== b.type) return a.type === 'direct' ? -1 : 1
      const walkA = (a.walkToStartMinutes ?? 0) + (a.walkToEndMinutes ?? 0)
      const walkB = (b.walkToStartMinutes ?? 0) + (b.walkToEndMinutes ?? 0)
      if (walkA !== walkB) return walkA - walkB
      if (a.minutesUntil !== b.minutesUntil) return a.minutesUntil - b.minutesUntil
      return a.arrivalTime.localeCompare(b.arrivalTime)
    })
    if (currentSearchId !== mySearchId) return // superseded by a newer search
    planResults.value = results.slice(0, 8)
    searchDone.value = true
  } catch (err) {
    console.error('Route search failed:', err)
    searchDone.value = true
  } finally {
    isSearching.value = false
  }
}

const selectPlanResult = (result: PlanResult) => {
  incrementTrips()
  emit('planSelected', result)
}

const getTransferLabel = (result: PlanResult) => {
  const transferStops = result.busSegments
    .slice(0, -1)
    .map(segment => segment.endStationName)
    .filter(Boolean)

  if (!transferStops.length) {
    return result.transferStation?.name ?? ''
  }

  if (transferStops.length === 1) {
    return transferStops[0]
  }

  return transferStops.join(' → ')
}

const formatTransferCount = (count: number) => {
  return `${count} ${count === 1 ? t('transferLabel') : t('transfersPlural')}`
}

// ===================== LIFECYCLE =====================
onMounted(loadRoutes)
onUnmounted(() => { if (countdownTimer) clearInterval(countdownTimer) })

const openPlanTab = () => { activeTab.value = 'plan' }

const setPlanOrigin = (location: { lat: number; lon: number; name: string }) => {
  activeTab.value = 'plan'
  planOrigin.value = { type: 'address', name: location.name, lat: location.lat, lon: location.lon }
  planOriginQuery.value = ''
  originSuggestions.value = []
}

const setPlanDestination = (location: { lat: number; lon: number; name: string }) => {
  activeTab.value = 'plan'
  planDest.value = { type: 'address', name: location.name, lat: location.lat, lon: location.lon }
  planDestQuery.value = ''
  destSuggestions.value = []
}

defineExpose({ openPlanTab, setPlanOrigin, setPlanDestination })
</script>

<style scoped>
/* ===== SIDEBAR SHELL ===== */
.sidebar {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-primary);
  border-right: 1px solid var(--border-primary);
  overflow: hidden;
  font-family: var(--font-sans);
  color: var(--text-primary);
  position: relative;
}

/* ===== HEADER ===== */
.sidebar-header {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 76px;
  /* loc pentru butonul de ascundere a panoului (fix, stânga sus) */
  padding: 16px 16px 16px 72px;
  border-bottom: 1px solid var(--border-subtle);
  flex-shrink: 0;
}

.brand-mark {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: var(--bg-inverse);
  color: var(--text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
}

.brand-name {
  font-family: var(--font-display);
  font-weight: var(--fw-heavy);
  font-size: 20px;
  letter-spacing: -0.02em;
}

.sidebar-header .live-badge {
  margin-left: auto;
}

/* ===== TABS ===== */
.tab-bar {
  display: flex;
  gap: 4px;
  padding: 8px 12px 0;
  border-bottom: 1px solid var(--border-subtle);
  flex-shrink: 0;
}

.tab-btn {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 44px;
  padding: 0 8px;
  border: none;
  border-bottom: 3px solid transparent;
  background: none;
  color: var(--text-secondary);
  font: inherit;
  font-size: 14px;
  font-weight: var(--fw-bold);
  cursor: pointer;
  transition: var(--transition-colors);
}

.tab-btn:hover {
  color: var(--text-primary);
}

.tab-btn.active {
  color: var(--text-primary);
  border-bottom-color: var(--text-primary);
}

.tab-btn:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
  border-radius: 8px 8px 0 0;
}

/* ===== CONTENT ===== */
.tab-content {
  flex: 1;
  overflow-y: auto;
  overscroll-behavior: contain;
  padding: 20px 16px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 12px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.section-title .count {
  font-variant-numeric: tabular-nums;
}

.section-hint,
.hint-text {
  margin: 0;
  font-size: 14px;
  color: var(--text-secondary);
}

/* ===== FIELDS ===== */
.field {
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 56px;
  padding: 6px 6px 6px 14px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-primary);
  transition: var(--transition-colors);
  cursor: text;
}

.field:focus-within {
  border-color: var(--border-focus);
  box-shadow: var(--ring-focus);
}

.field--search {
  min-height: 48px;
  background: var(--bg-secondary);
}

.field--filled {
  cursor: default;
  background: var(--bg-secondary);
}

.field--compact {
  flex: 1;
  min-height: 52px;
}

.field-icon {
  color: var(--text-secondary);
  flex-shrink: 0;
}

.field-text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.field-label {
  font-size: 12px;
  font-weight: var(--fw-semibold);
  color: var(--text-secondary);
}

.field-value {
  font-size: 15px;
  font-weight: var(--fw-semibold);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.field-input {
  width: 100%;
  min-width: 0;
  padding: 0;
  border: none;
  background: transparent;
  font: inherit;
  font-size: 15px;
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
  outline: none;
}

.field-input::placeholder {
  color: var(--text-tertiary);
  font-weight: var(--fw-normal);
}

.icon-btn {
  width: 36px;
  height: 36px;
  flex-shrink: 0;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.icon-btn:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

/* ===== AUTOCOMPLETE ===== */
.search-box {
  position: relative;
}

.autocomplete {
  margin-top: 6px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-elevated);
  box-shadow: var(--shadow-lg);
  overflow: hidden;
}

.autocomplete-item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-height: 44px;
  padding: 8px 12px;
  border: none;
  border-bottom: 1px solid var(--border-subtle);
  background: none;
  font: inherit;
  text-align: left;
  color: var(--text-primary);
  cursor: pointer;
}

.autocomplete-item:last-child {
  border-bottom: none;
}

.autocomplete-item:hover,
.autocomplete-item:focus-visible {
  background: var(--bg-secondary);
  outline: none;
}

.ac-icon {
  color: var(--text-secondary);
  flex-shrink: 0;
}

.ac-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.ac-name {
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

.ac-sub {
  font-size: 12px;
  color: var(--text-secondary);
}

/* ===== PLAN FORM ===== */
.plan-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.plan-title {
  margin: 0;
  font-size: 22px;
  font-weight: var(--fw-bold);
}

.endpoints {
  display: flex;
  gap: 10px;
  align-items: stretch;
}

.endpoints-rail {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 24px 0;
}

.rail-start {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: 2px solid var(--text-primary);
}

.rail-line {
  flex: 1;
  width: 2px;
  background: repeating-linear-gradient(var(--border-secondary) 0 4px, transparent 4px 8px);
}

.rail-end {
  width: 10px;
  height: 10px;
  border-radius: 2px;
  background: var(--text-primary);
}

.endpoints-fields {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.swap-btn {
  align-self: center;
  width: 44px;
  height: 44px;
  flex-shrink: 0;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-primary);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.swap-btn:hover {
  background: var(--bg-tertiary);
}

.segmented {
  display: flex;
  gap: 4px;
  padding: 4px;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
}

.segmented button {
  flex: 1;
  min-height: 40px;
  padding: 0 6px;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--text-secondary);
  font: inherit;
  font-size: 13px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.segmented button.active {
  background: var(--bg-primary);
  color: var(--text-primary);
  box-shadow: var(--shadow-xs), 0 1px 2px rgba(22, 24, 29, 0.08);
}

.datetime-row {
  display: flex;
  gap: 8px;
}

.btn-search-routes {
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
  font-size: 15px;
  font-weight: var(--fw-bold);
  cursor: pointer;
  transition: var(--transition-colors);
}

.btn-search-routes:hover:not(:disabled) {
  background: var(--accent-primary-hover);
}

.btn-search-routes:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

/* ===== RESULTS ===== */
.plan-results {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.results-header {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.results-sub {
  font-size: 13px;
  color: var(--text-secondary);
}

.result-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  width: 100%;
  padding: 14px 16px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s ease;
}

.result-card:hover {
  border-color: var(--border-secondary);
}

.result-card.selected {
  border: 2px solid var(--text-primary);
  padding: 13px 15px;
}

.result-card:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

.result-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.result-when {
  font-size: 13px;
  font-weight: var(--fw-semibold);
  color: var(--text-secondary);
}

.result-tag {
  font-size: 13px;
  font-weight: var(--fw-bold);
  color: var(--color-success);
}

.result-times {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.result-times .time {
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: var(--fw-bold);
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
}

.time-arrow {
  color: var(--text-tertiary);
  align-self: center;
}

.duration {
  margin-left: auto;
  font-size: 15px;
  font-weight: var(--fw-bold);
}

.result-legs {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.leg-walk {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 13px;
  font-weight: var(--fw-semibold);
  color: var(--text-secondary);
}

.leg-sep {
  color: var(--border-secondary);
}

.result-meta {
  font-size: 13px;
  color: var(--text-secondary);
}

.result-steps {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding-top: 10px;
  border-top: 1px solid var(--border-subtle);
  font-size: 14px;
  line-height: 1.45;
}

/* ===== ROUTES TAB ===== */
.routes-section {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.route-item {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  min-height: 48px;
  padding: 8px 10px;
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  background: none;
  font: inherit;
  text-align: left;
  color: var(--text-primary);
  cursor: pointer;
}

.route-item:hover {
  background: var(--bg-secondary);
}

.route-item.active {
  border-color: var(--text-primary);
  background: var(--bg-secondary);
}

.route-item .line-pill {
  min-width: 40px;
}

.route-name {
  flex: 1;
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

.route-check {
  color: var(--text-primary);
}

.stations-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-top: 12px;
  border-top: 1px solid var(--border-subtle);
}

.stations-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.station-row {
  position: relative;
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 36px;
  font-size: 14px;
}

.station-row::before {
  content: '';
  position: absolute;
  left: 5px;
  top: 0;
  bottom: 0;
  width: 2px;
  background: var(--border-primary);
}

.station-row:first-child::before { top: 50%; }
.station-row:last-child::before { bottom: 50%; }

.station-dot {
  position: relative;
  width: 12px;
  height: 12px;
  flex-shrink: 0;
  box-sizing: border-box;
  border-radius: 50%;
  border: 3px solid var(--text-primary);
  background: var(--bg-primary);
}

/* ===== SCHEDULE TAB ===== */
.schedule-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.board-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 16px;
  border-radius: var(--radius-lg);
  background: #16181d;
  color: #fff;
}

.board-eyebrow {
  font-size: 12px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: #b9bcc4;
}

.board-title {
  margin: 0;
  font-size: 26px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.02em;
}

.departures {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.departure {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  background: var(--bg-primary);
}

.departure-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.departure-dest {
  font-weight: var(--fw-bold);
  font-size: 15px;
}

.departure-sub {
  font-size: 12px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.departure-time {
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: var(--fw-heavy);
  font-variant-numeric: tabular-nums;
}

.departure-time.urgent {
  color: var(--live-ink);
}

/* ===== STATES ===== */
.center-state,
.empty-hero {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 32px 16px;
  text-align: center;
  color: var(--text-secondary);
}

.center-state p,
.empty-hero p {
  margin: 0;
  font-size: 14px;
  line-height: 1.5;
}

.empty-hero h3 {
  margin: 4px 0 0;
  font-size: 18px;
  color: var(--text-primary);
}

.empty-hero-icon {
  color: var(--text-tertiary);
}

.error-state {
  color: var(--color-danger);
}

.btn-secondary-sm {
  min-height: 40px;
  padding: 0 16px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.spinner,
.spinner-small {
  border-radius: 50%;
  border: 3px solid var(--border-primary);
  border-top-color: var(--text-primary);
  animation: spin 0.8s linear infinite;
}

.spinner { width: 28px; height: 28px; }
.spinner-small { width: 16px; height: 16px; border-width: 2px; display: inline-block; }
.spinner-small--light { border-color: rgba(255, 255, 255, 0.35); border-top-color: #fff; }

.loading-small {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--text-secondary);
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.slide-down-enter-active,
.slide-down-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.slide-down-enter-from,
.slide-down-leave-to {
  opacity: 0;
  transform: translateY(-6px);
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
