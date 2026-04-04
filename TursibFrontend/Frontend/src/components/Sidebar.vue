<template>
  <aside class="sidebar">

    <!-- Header -->
    <div class="sidebar-header">
      <div class="header-icon">🚌</div>
      <div>
        <h1>Tursib Tracker</h1>
        <p>Sibiu — Transport Public</p>
      </div>
    </div>

    <!-- Tab Bar -->
    <div class="tab-bar">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        @click="activeTab = tab.id"
        :class="['tab-btn', { active: activeTab === tab.id }]"
      >
        <span class="tab-icon">{{ tab.icon }}</span>
        <span class="tab-label">{{ tab.label }}</span>
      </button>
    </div>

    <!-- ==================== TAB: TRASEE ==================== -->
    <div v-if="activeTab === 'routes'" class="tab-content">
      <div v-if="loading" class="center-state">
        <div class="spinner"></div>
        <p>Se încarcă traseele...</p>
      </div>

      <div v-else-if="error" class="center-state error-state">
        <span class="state-icon">⚠️</span>
        <p>{{ error }}</p>
        <button @click="loadRoutes" class="btn-retry">Reîncearcă</button>
      </div>

      <div v-else class="routes-section">
        <p class="section-hint">Selectează un traseu pentru a-l vedea pe hartă</p>

        <button
          v-for="route in routes"
          :key="route.id"
          @click="selectRoute(route.id)"
          class="route-item"
          :class="{ active: selectedRouteId === route.id }"
        >
          <span
            class="route-badge"
            :style="{ background: route.color || '#3b82f6' }"
          >{{ route.routeNumber }}</span>
          <span class="route-name">{{ route.name }}</span>
          <span v-if="selectedRouteId === route.id" class="route-check">✓</span>
        </button>
      </div>

      <!-- Stații traseu selectat -->
      <transition name="slide-down">
        <div v-if="selectedRouteId && (loadingStations || currentStations.length > 0)" class="stations-section">
          <div class="stations-header">
            <span>📍 Stații pe traseu</span>
            <span class="stations-count" v-if="!loadingStations">{{ currentStations.length }}</span>
          </div>
          <div v-if="loadingStations" class="loading-small">
            <div class="spinner-small"></div> Se încarcă...
          </div>
          <div v-else class="stations-list">
            <div
              v-for="(station, index) in currentStations"
              :key="station.id"
              class="station-row"
            >
              <span class="station-index">{{ index + 1 }}</span>
              <span class="station-dot"></span>
              <span class="station-name">{{ station.name }}</span>
            </div>
          </div>
        </div>
      </transition>
    </div>

    <!-- ==================== TAB: ORAR ==================== -->
    <div v-if="activeTab === 'schedule'" class="tab-content">

      <!-- Search input -->
      <div class="search-box">
        <div class="search-input-wrap">
          <span class="search-icon">🔍</span>
          <input
            v-model="scheduleQuery"
            @input="onScheduleInput"
            placeholder="Caută stație... (ex: Gara)"
            class="search-input"
            autocomplete="off"
          />
          <button v-if="scheduleQuery" @click="clearSchedule" class="clear-btn">✕</button>
        </div>

        <!-- Autocomplete -->
        <div v-if="filteredScheduleStations.length > 0 && !selectedScheduleStation" class="autocomplete">
          <button
            v-for="s in filteredScheduleStations.slice(0, 7)"
            :key="s.id"
            @click="selectScheduleStation(s)"
            class="autocomplete-item"
          >
            🚏 {{ s.name }}
          </button>
        </div>
      </div>

      <!-- Stație selectată → orar -->
      <div v-if="selectedScheduleStation" class="schedule-panel">
        <div class="schedule-station-title">
          <span class="stop-icon">🚏</span>
          <strong>{{ selectedScheduleStation.name }}</strong>
        </div>

        <div v-if="loadingSchedule" class="center-state">
          <div class="spinner"></div>
          <p>Se încarcă orarul...</p>
        </div>

        <div v-else-if="scheduleETAs.length === 0" class="center-state">
          <span class="state-icon">🚫</span>
          <p>Nu există curse în următoarea oră.</p>
        </div>

        <div v-else class="etas-list">
          <div
            v-for="eta in scheduleETAs"
            :key="`${eta.routeNumber}-${eta.arrivalTime}`"
            class="eta-card"
            :style="{ borderLeftColor: eta.color }"
          >
            <div class="eta-badge" :style="{ background: eta.color }">{{ eta.routeNumber }}</div>
            <div class="eta-info">
              <span class="eta-route-name">{{ eta.routeName }}</span>
              <span class="eta-direction">{{ eta.direction }}</span>
            </div>
            <div class="eta-time" :class="{ urgent: eta.countdown < 120 }">
              {{ formatETA(eta.countdown) }}
            </div>
          </div>
        </div>
      </div>

      <!-- Stare inițială -->
      <div v-else-if="!scheduleQuery" class="empty-hero">
        <div class="empty-hero-icon">🕐</div>
        <h3>Orar în timp real</h3>
        <p>Caută o stație pentru a vedea autobuzele care urmează să sosească.</p>
      </div>
    </div>

    <!-- ==================== TAB: PLANIFICARE ==================== -->
    <div v-if="activeTab === 'plan'" class="tab-content">

      <div class="plan-form">
        <!-- Plecare -->
        <div class="form-group">
          <label class="form-label">📍 Plecare din</label>
          <div v-if="planOrigin" class="selected-station-chip">
            <span>{{ planOrigin.type === 'station' ? '🚏' : '📍' }} {{ planOrigin.name }}</span>
            <button @click="clearOrigin" class="chip-close">✕</button>
          </div>
          <div v-else>
            <div class="search-input-wrap">
              <input v-model="planOriginQuery" @input="debouncedOriginInput"
                placeholder="Stație sau adresă de plecare..." class="search-input" autocomplete="off" />
              <button v-if="planOriginQuery" @click="clearOrigin" class="clear-btn">✕</button>
            </div>
            <div v-if="originSuggestions.length > 0" class="autocomplete">
              <button v-for="s in originSuggestions" :key="`${s.type}-${s.name}`"
                @click="selectOrigin(s)" class="autocomplete-item">
                {{ s.type === 'station' ? '🚏' : '📍' }} {{ s.name }}
              </button>
            </div>
          </div>
        </div>

        <!-- Destinație -->
        <div class="form-group">
          <label class="form-label">🎯 Destinație</label>
          <div v-if="planDest" class="selected-station-chip">
            <span>{{ planDest.type === 'station' ? '🚏' : '📍' }} {{ planDest.name }}</span>
            <button @click="clearDest" class="chip-close">✕</button>
          </div>
          <div v-else>
            <div class="search-input-wrap">
              <input v-model="planDestQuery" @input="debouncedDestInput"
                placeholder="Stație sau adresă destinație..." class="search-input" autocomplete="off" />
              <button v-if="planDestQuery" @click="clearDest" class="clear-btn">✕</button>
            </div>
            <div v-if="destSuggestions.length > 0" class="autocomplete">
              <button v-for="s in destSuggestions" :key="`${s.type}-${s.name}`"
                @click="selectDest(s)" class="autocomplete-item">
                {{ s.type === 'station' ? '🚏' : '📍' }} {{ s.name }}
              </button>
            </div>
          </div>
        </div>

        <!-- Ora plecării -->
        <div class="form-group">
          <label class="form-label">🕐 Plecare după ora</label>
          <input v-model="planTime" type="time" class="time-input" required />
        </div>

        <button @click="searchRoutes" :disabled="!planOrigin || !planDest || !planTime || isSearching" class="btn-search-routes">
          <span v-if="isSearching">⏳ Se caută...</span>
          <span v-else>🔍 Caută curse</span>
        </button>
      </div>

      <!-- Rezultate -->
      <div v-if="planResults.length > 0" class="plan-results">
        <div class="results-header">
          <strong>{{ planResults.length }} rezultate</strong>
          <span class="results-sub">{{ planOrigin?.name }} → {{ planDest?.name }}</span>
        </div>

        <div v-for="(result, idx) in planResults" :key="idx"
          class="result-card" :class="result.type" @click="selectPlanResult(result)" style="cursor:pointer">

          <!-- Direct -->
          <template v-if="result.type === 'direct'">
            <div class="result-top">
              <span class="result-badge" :style="{ background: result.route1Color }">{{ result.route1Number }}</span>
              <span class="result-route">{{ result.route1Name }}</span>
            </div>
          </template>

          <!-- Transfer -->
          <template v-else>
            <div class="result-top">
              <span class="result-badge" :style="{ background: result.route1Color }">{{ result.route1Number }}</span>
              <span class="transfer-icon">⇄</span>
              <span class="result-badge" :style="{ background: result.route2Color }">{{ result.route2Number }}</span>
              <span class="result-route transfer-label">via {{ result.transferStation?.name }}</span>
            </div>
          </template>

          <div class="result-times">
            <div class="time-block">
              <span class="time-label">Plecare</span>
              <span class="time-value">{{ result.departureTime }}</span>
            </div>
            <div class="time-arrow">→</div>
            <div class="time-block">
              <span class="time-label">Sosire est.</span>
              <span class="time-value arrival">{{ result.arrivalTime }}</span>
            </div>
          </div>

          <div v-if="result.walkToStartMinutes || result.walkToEndMinutes" class="walking-info">
            <span v-if="result.walkToStartMinutes">🚶 {{ result.walkToStartMinutes }} min →</span>
            🚌
            <span v-if="result.walkToEndMinutes">→ 🚶 {{ result.walkToEndMinutes }} min</span>
          </div>

          <div class="result-meta">
            <span class="result-countdown" :class="{ urgent: result.minutesUntil < 5 }">
              {{ result.minutesUntil <= 0 ? 'Acum' : `în ${result.minutesUntil} min` }}
            </span>
            <span>•</span>
            <span v-if="result.type === 'transfer'">
              {{ result.route1StationsCount }}+{{ result.route2StationsCount }} stații • transfer
            </span>
            <span v-else>{{ result.stationsBetween }} stații</span>
          </div>
        </div>
      </div>

      <div v-else-if="searchDone" class="center-state">
        <span class="state-icon">🔎</span>
        <p>Nu s-au găsit curse în intervalul selectat.</p>
        <p class="hint-text">Încearcă o altă oră sau locații diferite.</p>
      </div>

      <div v-else-if="!planOrigin && !planDest" class="empty-hero">
        <div class="empty-hero-icon">🧭</div>
        <h3>Planifică o călătorie</h3>
        <p>Caută orice adresă sau stație, alege ora și găsim cel mai bun traseu — direct sau cu transfer.</p>
      </div>
    </div>

  </aside>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import apiService, { type Route, type Station, type StationScheduleEntry } from '@/services/apiService'

// Props
const props = defineProps<{
  allStations?: Station[]
}>()

// Emits
const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  planSelected: [plan: PlanResult]
}>()

// ===================== TABS =====================
type TabId = 'routes' | 'schedule' | 'plan'
const activeTab = ref<TabId>('routes')
const tabs: { id: TabId; icon: string; label: string }[] = [
  { id: 'routes',   icon: '🗺️', label: 'Trasee' },
  { id: 'schedule', icon: '🕐', label: 'Orar' },
  { id: 'plan',     icon: '🧭', label: 'Planificare' },
]

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
    const cachedList = localStorage.getItem('routesList')
    const cachedTs   = localStorage.getItem('routesListTs')
    if (cachedList && cachedTs && Date.now() - parseInt(cachedTs) < 30 * 60 * 1000) {
      routes.value = JSON.parse(cachedList)
      return
    }
    routes.value = await apiService.getRoutes()
    localStorage.setItem('routesList', JSON.stringify(routes.value))
    localStorage.setItem('routesListTs', Date.now().toString())
  } catch {
    error.value = 'Nu s-au putut încărca traseele. Verifică dacă API-ul rulează.'
  } finally {
    loading.value = false
  }
}

const selectRoute = async (routeId: number) => {
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
}

const planOriginQuery   = ref('')
const planDestQuery     = ref('')
const planTime          = ref(new Date().toTimeString().substring(0, 5))
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

// Geocoding via Nominatim (debounced)
let geocodeAbort: AbortController | null = null
const geocode = async (query: string): Promise<Suggestion[]> => {
  if (geocodeAbort) geocodeAbort.abort()
  geocodeAbort = new AbortController()
  try {
    const url = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=4&countrycodes=ro&viewbox=23.9,45.65,24.45,46.0&bounded=0`
    const res = await fetch(url, { signal: geocodeAbort.signal, headers: { 'Accept-Language': 'ro' } })
    const data: any[] = await res.json()
    return data.map(d => ({
      type: 'address' as const,
      name: d.display_name.split(',').slice(0, 2).join(', '),
      lat: parseFloat(d.lat),
      lon: parseFloat(d.lon),
    }))
  } catch { return [] }
}

const getSuggestions = async (query: string): Promise<Suggestion[]> => {
  const q = query.toLowerCase().trim()
  if (q.length < 2) return []
  const stationMatches: Suggestion[] = (props.allStations || [])
    .filter(s => s.name.toLowerCase().includes(q))
    .slice(0, 4)
    .map(s => ({ type: 'station', name: s.name, lat: s.latitude, lon: s.longitude, stationId: s.id }))
  const geocoded = await geocode(query)
  return [...stationMatches, ...geocoded.slice(0, Math.max(0, 6 - stationMatches.length))]
}

let debounceTimer: number | null = null
const debouncedOriginInput = () => {
  planOrigin.value = null
  if (debounceTimer) clearTimeout(debounceTimer)
  if (!planOriginQuery.value.trim()) { originSuggestions.value = []; return }
  debounceTimer = window.setTimeout(async () => {
    originSuggestions.value = await getSuggestions(planOriginQuery.value)
  }, 300)
}
const debouncedDestInput = () => {
  planDest.value = null
  if (debounceTimer) clearTimeout(debounceTimer)
  if (!planDestQuery.value.trim()) { destSuggestions.value = []; return }
  debounceTimer = window.setTimeout(async () => {
    destSuggestions.value = await getSuggestions(planDestQuery.value)
  }, 300)
}

const selectOrigin = (s: Suggestion) => { planOrigin.value = s; planOriginQuery.value = ''; originSuggestions.value = [] }
const selectDest   = (s: Suggestion) => { planDest.value = s;   planDestQuery.value = '';   destSuggestions.value = [] }
const clearOrigin  = () => { planOrigin.value = null; planOriginQuery.value = ''; originSuggestions.value = [] }
const clearDest    = () => { planDest.value = null;   planDestQuery.value = '';   destSuggestions.value = [] }

const parseTimeToMinutes = (time: string): number | null => {
  const [hh, mm] = time.split(':')
  if (hh === undefined || mm === undefined) return null
  const h = Number(hh)
  const m = Number(mm)
  if (!Number.isFinite(h) || !Number.isFinite(m)) return null
  return h * 60 + m
}

const minutesToStr = (m: number) => `${String(Math.floor(m/60)%24).padStart(2,'0')}:${String(m%60).padStart(2,'0')}`

const searchRoutes = async () => {
  if (!planOrigin.value || !planDest.value) return
  isSearching.value = true; searchDone.value = false; planResults.value = []

  try {
    const origin = planOrigin.value!, dest = planDest.value!
    const now = new Date()
    const nowMin = now.getHours() * 60 + now.getMinutes()

    // Stații candidate: exactă dacă user a selectat stație, altfel cele mai apropiate 5
    // (5 stații acoperă și stații de pe cealaltă parte a străzii sau din apropiere)
    const boardingCandidates: Station[] = origin.type === 'station'
      ? [props.allStations!.find(s => s.id === origin.stationId)!].filter(Boolean)
      : nearestStations(origin.lat, origin.lon, 5)
    const alightingCandidates: Station[] = dest.type === 'station'
      ? [props.allStations!.find(s => s.id === dest.stationId)!].filter(Boolean)
      : nearestStations(dest.lat, dest.lon, 5)

    if (!boardingCandidates.length || !alightingCandidates.length) { searchDone.value = true; return }

    // Map: cheie_ruta → cel mai bun rezultat (walking minim)
    const bestByRoute = new Map<string, PlanResult>()

    // Încearcă toate combinațiile boarding × alighting prin Dijkstra backend
    await Promise.all(boardingCandidates.flatMap(boarding =>
      alightingCandidates.map(async alighting => {
        if (boarding.id === alighting.id) return

        const walkStart = origin.type === 'address'
          ? walkMin(origin.lat, origin.lon, boarding.latitude, boarding.longitude) : undefined
        const walkEnd = dest.type === 'address'
          ? walkMin(alighting.latitude, alighting.longitude, dest.lat, dest.lon) : undefined

        try {
          const routes = await apiService.calculateRouteAlternatives(boarding.id, alighting.id)

          for (const route of routes) {
            const busSegments = (route.segments as any[]).filter(s => s.type === 'bus')
            if (!busSegments.length) continue

            const firstBus = busSegments[0]
            const lastBus = busSegments[busSegments.length - 1]
            const isDirect = busSegments.length === 1

            const depMin = nowMin + (walkStart ?? 0)
            const arrMin = depMin + route.totalDuration + (walkEnd ?? 0)
            const totalWalk = (walkStart ?? 0) + (walkEnd ?? 0)

            // Cheia de deduplicare: bazată pe rutele folosite + stația de transfer
            // (nu pe stația de urcare/coborâre - poate fi aceeași rută dar stații diferite)
            const transferKey = isDirect ? '' : (firstBus.endStation?.name ?? '')
            const key = isDirect
              ? `D-${firstBus.routeNumber}`
              : `T-${firstBus.routeNumber}-${lastBus.routeNumber}-${transferKey}`

            const result: PlanResult = {
              type: isDirect ? 'direct' : 'transfer',
              route1Id: firstBus.routeId ?? 0,
              route1Number: firstBus.routeNumber ?? '',
              route1Name: firstBus.routeName ?? '',
              route1Color: firstBus.color ?? '#3b82f6',
              stationsBetween: busSegments.reduce((sum: number, s: any) => sum + (s.stationCount ?? 0), 0),
              boardingStation: boarding,
              alightingStation: alighting,
              originLat: origin.lat, originLon: origin.lon, originName: origin.name,
              destLat: dest.lat, destLon: dest.lon, destName: dest.name,
              walkToStartMinutes: walkStart,
              walkToEndMinutes: walkEnd,
              departureTime: minutesToStr(depMin),
              arrivalTime: minutesToStr(arrMin),
              minutesUntil: walkStart ?? 0,
            }

            if (!isDirect && busSegments.length >= 2) {
              const secondBus = busSegments[1]
              result.route2Id = secondBus.routeId ?? 0
              result.route2Number = secondBus.routeNumber ?? ''
              result.route2Name = secondBus.routeName ?? ''
              result.route2Color = secondBus.color ?? '#10b981'
              result.route1StationsCount = firstBus.stationCount ?? 0
              result.route2StationsCount = lastBus.stationCount ?? 0
              result.transferStation = firstBus.endStation
            }

            // Păstrează varianta cu cel mai puțin walking pentru aceeași rută
            const existing = bestByRoute.get(key)
            if (!existing) {
              bestByRoute.set(key, result)
            } else {
              const existingWalk = (existing.walkToStartMinutes ?? 0) + (existing.walkToEndMinutes ?? 0)
              if (totalWalk < existingWalk) bestByRoute.set(key, result)
            }
          }
        } catch {
          // Skip combinații fără rută
        }
      })
    ))

    const results = [...bestByRoute.values()]

    // Sortare: direct primul, apoi walking minim, apoi durată totală
    results.sort((a, b) => {
      if (a.type !== b.type) return a.type === 'direct' ? -1 : 1
      const walkA = (a.walkToStartMinutes ?? 0) + (a.walkToEndMinutes ?? 0)
      const walkB = (b.walkToStartMinutes ?? 0) + (b.walkToEndMinutes ?? 0)
      if (walkA !== walkB) return walkA - walkB
      return a.departureTime.localeCompare(b.departureTime)
    })
    planResults.value = results.slice(0, 8)
    searchDone.value = true
  } catch {
    searchDone.value = true
  } finally {
    isSearching.value = false
  }
}

const selectPlanResult = (result: PlanResult) => {
  emit('planSelected', result)
}

// ===================== LIFECYCLE =====================
onMounted(loadRoutes)
onUnmounted(() => { if (countdownTimer) clearInterval(countdownTimer) })

const openPlanTab = () => { activeTab.value = 'plan' }
defineExpose({ openPlanTab })
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
  font-family: 'Inter', system-ui, sans-serif;
}

/* ===== HEADER ===== */
.sidebar-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 18px 16px 14px;
  background: var(--gradient-primary);
  color: white;
  flex-shrink: 0;
}

.header-icon { font-size: 28px; }

.sidebar-header h1 {
  font-size: 16px;
  font-weight: 700;
  margin: 0;
  line-height: 1.2;
}

.sidebar-header p {
  font-size: 11px;
  margin: 0;
  opacity: 0.85;
}

/* ===== TAB BAR ===== */
.tab-bar {
  display: flex;
  border-bottom: 1px solid var(--border-primary);
  background: var(--bg-secondary);
  flex-shrink: 0;
}

.tab-btn {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: 10px 4px;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 11px;
  font-weight: 500;
  transition: all 0.2s;
  border-bottom: 2px solid transparent;
}

.tab-btn:hover { color: var(--text-primary); background: var(--bg-tertiary); }

.tab-btn.active {
  color: #3b82f6;
  border-bottom-color: #3b82f6;
  background: var(--bg-primary);
}

.tab-icon { font-size: 16px; }
.tab-label { font-size: 10px; font-weight: 600; letter-spacing: 0.02em; }

/* ===== TAB CONTENT ===== */
.tab-content {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

/* ===== SHARED STATES ===== */
.center-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 24px 16px;
  text-align: center;
  color: var(--text-secondary);
  font-size: 13px;
}

.state-icon { font-size: 28px; }

.error-state { color: #ef4444; }

.empty-hero {
  padding: 32px 16px;
  text-align: center;
  color: var(--text-secondary);
}

.empty-hero-icon { font-size: 40px; margin-bottom: 12px; }
.empty-hero h3 { font-size: 15px; font-weight: 600; color: var(--text-primary); margin: 0 0 8px; }
.empty-hero p  { font-size: 13px; line-height: 1.5; margin: 0; }

.spinner {
  width: 24px; height: 24px;
  border: 2px solid var(--border-primary);
  border-top-color: #3b82f6;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}
.spinner-small {
  display: inline-block;
  width: 12px; height: 12px;
  border: 2px solid var(--border-primary);
  border-top-color: #3b82f6;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  vertical-align: middle;
  margin-right: 4px;
}
@keyframes spin { to { transform: rotate(360deg); } }

.btn-retry {
  padding: 6px 16px;
  border: 1px solid #ef4444;
  border-radius: 6px;
  background: transparent;
  color: #ef4444;
  font-size: 12px;
  cursor: pointer;
}

.section-hint {
  font-size: 11px;
  color: var(--text-tertiary);
  margin: 0 0 8px;
  padding: 0 2px;
}

/* ===== ROUTES LIST ===== */
.routes-section { display: flex; flex-direction: column; gap: 4px; }

.route-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  background: var(--bg-secondary);
  cursor: pointer;
  text-align: left;
  transition: all 0.15s;
  width: 100%;
}

.route-item:hover { border-color: #3b82f6; background: var(--bg-tertiary); }
.route-item.active { border-color: #3b82f6; background: rgba(59, 130, 246, 0.08); }

.route-badge {
  flex-shrink: 0;
  min-width: 36px;
  padding: 3px 6px;
  border-radius: 6px;
  color: white;
  font-size: 12px;
  font-weight: 700;
  text-align: center;
}

.route-name {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.route-check { color: #3b82f6; font-weight: 700; font-size: 14px; }

/* ===== STATIONS ===== */
.stations-section {
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  overflow: hidden;
  background: var(--bg-secondary);
  margin-top: 4px;
}

.stations-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: var(--bg-tertiary);
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  border-bottom: 1px solid var(--border-primary);
}

.stations-count {
  background: #3b82f6;
  color: white;
  border-radius: 10px;
  padding: 2px 7px;
  font-size: 11px;
}

.loading-small {
  padding: 12px;
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 6px;
}

.stations-list { max-height: 280px; overflow-y: auto; }

.station-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 12px;
  border-bottom: 1px solid var(--border-primary);
  font-size: 12px;
}

.station-row:last-child { border-bottom: none; }

.station-index {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  font-size: 10px;
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: center;
}

.station-dot {
  flex-shrink: 0;
  width: 6px; height: 6px;
  border-radius: 50%;
  background: #3b82f6;
}

.station-name { color: var(--text-primary); }

/* ===== SEARCH BOX ===== */
.search-box { position: relative; }

.search-input-wrap {
  display: flex;
  align-items: center;
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  background: var(--bg-secondary);
  overflow: hidden;
  transition: border-color 0.15s;
}

.search-input-wrap:focus-within { border-color: #3b82f6; }

.search-icon { padding: 0 10px; font-size: 14px; }

.search-input {
  flex: 1;
  padding: 10px 8px 10px 0;
  border: none;
  background: transparent;
  color: var(--text-primary);
  font-size: 13px;
  outline: none;
}

.search-input::placeholder { color: var(--text-tertiary); }

.clear-btn {
  padding: 8px 10px;
  border: none;
  background: transparent;
  color: var(--text-tertiary);
  cursor: pointer;
  font-size: 12px;
  transition: color 0.15s;
}
.clear-btn:hover { color: var(--text-primary); }

/* ===== AUTOCOMPLETE ===== */
.autocomplete {
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  background: var(--bg-primary);
  box-shadow: var(--shadow-md);
  overflow: hidden;
  margin-top: 4px;
  z-index: 10;
}

.autocomplete-item {
  display: block;
  width: 100%;
  text-align: left;
  padding: 9px 12px;
  border: none;
  background: transparent;
  color: var(--text-primary);
  font-size: 13px;
  cursor: pointer;
  border-bottom: 1px solid var(--border-primary);
  transition: background 0.1s;
}

.autocomplete-item:last-child { border-bottom: none; }
.autocomplete-item:hover { background: var(--bg-tertiary); }

/* ===== SCHEDULE PANEL ===== */
.schedule-panel { display: flex; flex-direction: column; gap: 8px; }

.schedule-station-title {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  font-size: 13px;
  color: var(--text-primary);
}

.stop-icon { font-size: 18px; }

/* ===== ETAs ===== */
.etas-list { display: flex; flex-direction: column; gap: 6px; }

.eta-card {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-left: 4px solid #3b82f6;
  border-radius: 8px;
  background: var(--bg-secondary);
  border-top: 1px solid var(--border-primary);
  border-right: 1px solid var(--border-primary);
  border-bottom: 1px solid var(--border-primary);
}

.eta-badge {
  flex-shrink: 0;
  min-width: 34px;
  padding: 3px 6px;
  border-radius: 6px;
  color: white;
  font-size: 12px;
  font-weight: 700;
  text-align: center;
}

.eta-info { flex: 1; min-width: 0; }
.eta-route-name {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.eta-direction { display: block; font-size: 11px; color: var(--text-secondary); margin-top: 1px; }

.eta-time {
  flex-shrink: 0;
  font-size: 18px;
  font-weight: 800;
  color: #3b82f6;
  min-width: 48px;
  text-align: right;
}

.eta-time.urgent { color: #ef4444; }

/* ===== PLAN FORM ===== */
.plan-form { display: flex; flex-direction: column; gap: 12px; }

.form-group { display: flex; flex-direction: column; gap: 6px; }

.form-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.selected-station-chip {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: rgba(59, 130, 246, 0.1);
  border: 1px solid rgba(59, 130, 246, 0.3);
  border-radius: 8px;
  font-size: 13px;
  color: var(--text-primary);
}

.chip-close {
  border: none;
  background: transparent;
  color: var(--text-tertiary);
  cursor: pointer;
  font-size: 12px;
  padding: 0 2px;
}
.chip-close:hover { color: #ef4444; }

.time-input {
  padding: 10px 12px;
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-size: 14px;
  font-weight: 600;
  outline: none;
  transition: border-color 0.15s;
}

.time-input:focus { border-color: #3b82f6; }

.btn-search-routes {
  padding: 12px;
  border: none;
  border-radius: 10px;
  background: var(--gradient-primary);
  color: white;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.2s, transform 0.1s;
}

.btn-search-routes:hover:not(:disabled) { opacity: 0.9; transform: translateY(-1px); }
.btn-search-routes:disabled { opacity: 0.5; cursor: not-allowed; }

/* ===== PLAN RESULTS ===== */
.plan-results { display: flex; flex-direction: column; gap: 8px; }

.results-header {
  padding: 8px 0 4px;
  border-bottom: 1px solid var(--border-primary);
}

.results-header strong { font-size: 13px; color: var(--text-primary); display: block; }
.results-sub { font-size: 11px; color: var(--text-secondary); }

.result-card {
  padding: 12px;
  border: 1px solid var(--border-primary);
  border-radius: 10px;
  background: var(--bg-secondary);
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: border-color 0.15s, box-shadow 0.15s;
}
.result-card:hover { border-color: #3b82f6; box-shadow: 0 0 0 2px rgba(59,130,246,0.12); }
.result-card.transfer { border-left: 3px solid #f59e0b; }

.result-top { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }

.result-badge {
  flex-shrink: 0;
  min-width: 34px;
  padding: 3px 6px;
  border-radius: 6px;
  color: white;
  font-size: 12px;
  font-weight: 700;
  text-align: center;
}

.transfer-icon { font-size: 14px; color: #f59e0b; flex-shrink: 0; }

.result-route {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}
.result-route.transfer-label { color: var(--text-secondary); font-weight: 500; }

.result-times {
  display: flex;
  align-items: center;
  gap: 12px;
  background: var(--bg-tertiary);
  border-radius: 8px;
  padding: 8px 12px;
}

.time-block { text-align: center; }
.time-label { display: block; font-size: 10px; color: var(--text-tertiary); margin-bottom: 2px; text-transform: uppercase; }
.time-value { display: block; font-size: 20px; font-weight: 800; color: var(--text-primary); line-height: 1; }
.time-value.arrival { color: #10b981; }
.time-arrow { font-size: 18px; color: var(--text-tertiary); flex: 1; text-align: center; }

.walking-info {
  font-size: 11px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  background: var(--bg-tertiary);
  border-radius: 6px;
}

.result-meta {
  display: flex;
  gap: 6px;
  font-size: 11px;
  color: var(--text-secondary);
  align-items: center;
}

.result-countdown { font-weight: 700; color: #10b981; }
.result-countdown.urgent { color: #ef4444; }

.hint-text { font-size: 12px; color: var(--text-tertiary); margin-top: 4px; }

/* ===== TRANSITION ===== */
.slide-down-enter-active,
.slide-down-leave-active { transition: all 0.25s ease; }
.slide-down-enter-from,
.slide-down-leave-to { opacity: 0; transform: translateY(-8px); }
</style>
