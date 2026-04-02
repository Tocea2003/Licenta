<template>
  <aside class="sidebar">
    <!-- Header -->
    <div class="sidebar-header">
      <div class="brand">
        <div class="brand-icon">🚌</div>
        <div class="brand-text">
          <span class="brand-name">Tursib</span>
          <span class="brand-sub">Sibiu · Transport Public</span>
        </div>
      </div>
    </div>

    <div class="sidebar-content">
      <!-- Quick Actions -->
      <div class="quick-actions">
        <button @click="toggleTripMode" class="action-btn trip-btn" :class="{ active: tripMode }">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
            <path d="M9 11L12 14L22 4M21 12V19a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
          <span>Planifică</span>
        </button>
        <button @click="goToFavorites" class="action-btn fav-btn">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
            <path d="M20.84 4.61a5.5 5.5 0 00-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 00-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 000-7.78z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
          <span>Favorite</span>
        </button>
        <button @click="goToStatistics" class="action-btn stats-btn">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
            <path d="M3 3v18h18M18 17V9M13 17V5M8 17v-3" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
          <span>Statistici</span>
        </button>
      </div>

      <!-- Quick Stats -->
      <div class="quick-stats">
        <div class="stat-item">
          <span class="stat-value">{{ routes.length }}</span>
          <span class="stat-label">Trasee</span>
        </div>
        <div class="stat-divider"></div>
        <div class="stat-item">
          <span class="stat-value">{{ favoriteCount }}</span>
          <span class="stat-label">Favorite</span>
        </div>
        <div class="stat-divider"></div>
        <div class="stat-item">
          <span class="stat-value">{{ searchCount }}</span>
          <span class="stat-label">Căutări</span>
        </div>
      </div>

      <!-- Loading skeletons -->
      <div v-if="loading" class="skeleton-list">
        <div v-for="i in 6" :key="i" class="skeleton-route-item">
          <div class="skeleton-badge"></div>
          <div class="skeleton-text"></div>
        </div>
      </div>

      <!-- Error -->
      <div v-else-if="error" class="error-state">
        <p>{{ error }}</p>
        <button @click="loadRoutes" class="retry-btn">Reîncearcă</button>
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

const router = useRouter()
const { favorites } = useFavorites()
const { statistics, recordRouteUsage } = useStatistics()

const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  planSelected: [plan: PlanResult]
}>()

const routes = ref<Route[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedRouteId = ref<number | null>(null)
const currentStations = ref<Station[]>([])
const loadingStations = ref(false)
const tripMode = ref(false)

const favoriteCount = computed(() => favorites.value.length)
const searchCount = computed(() => statistics.value.totalSearches)

const goToFavorites = () => router.push('/favorites')
const goToStatistics = () => router.push('/statistics')

const toggleTripMode = () => {
  tripMode.value = !tripMode.value
  emit('tripModeChanged', tripMode.value)
  if (tripMode.value) {
    selectedRouteId.value = null
    currentStations.value = []
  }
}

const loadRoutes = async () => {
  loading.value = true
  error.value = null
  try {
    const cached = localStorage.getItem('routes')
    const ts = localStorage.getItem('routesTimestamp')
    if (cached && ts && Date.now() - parseInt(ts) < 1800000) {
      routes.value = JSON.parse(cached)
    } else {
      const fetched = await apiService.getRoutes()
      routes.value = fetched
      localStorage.setItem('routes', JSON.stringify(fetched))
      localStorage.setItem('routesTimestamp', Date.now().toString())
    }
  } catch {
    error.value = 'Nu s-au putut încărca traseele. Verifică dacă API-ul rulează.'
  } finally {
    loading.value = false
  }
}

const stationsCache = new Map<number, Station[]>()

const selectRoute = async (routeId: number) => {
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
    const parts = entry.departureTime.split(':')
    if (parts.length < 2) continue
    const entryMinutes = parseInt(parts[0]) * 60 + parseInt(parts[1])
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

const minutesToStr = (m: number) => `${String(Math.floor(m/60)%24).padStart(2,'0')}:${String(m%60).padStart(2,'0')}`

const searchRoutes = async () => {
  if (!planOrigin.value || !planDest.value) return
  isSearching.value = true; searchDone.value = false; planResults.value = []

  try {
    const [hh, mm] = planTime.value.split(':').map(Number)
    const targetMin = hh * 60 + mm
    const nowMin = new Date().getHours() * 60 + new Date().getMinutes()
    const origin = planOrigin.value!, dest = planDest.value!

    // Candidate stations: exact if station selected, else top 5 nearest by walk distance
    const boardingCandidates: Station[] = origin.type === 'station'
      ? [props.allStations!.find(s => s.id === origin.stationId)!].filter(Boolean)
      : nearestStations(origin.lat, origin.lon, 5)
    const alightingCandidates: Station[] = dest.type === 'station'
      ? [props.allStations!.find(s => s.id === dest.stationId)!].filter(Boolean)
      : nearestStations(dest.lat, dest.lon, 5)

    if (!boardingCandidates.length || !alightingCandidates.length) { searchDone.value = true; return }

    // Phase 1: preload schedules + route stations for all boarding candidates
    const boardingScheduleMap = new Map<number, any[]>()
    await Promise.all(boardingCandidates.map(async boarding => {
      const schedule = await apiService.getStationSchedule(boarding.id)
      const upcoming = schedule.filter(e => {
        const p = e.departureTime.split(':')
        return parseInt(p[0]) * 60 + parseInt(p[1]) >= targetMin
      })
      boardingScheduleMap.set(boarding.id, upcoming)
      const routeIds = [...new Set(upcoming.map((e: any) => e.routeId as number))]
      await Promise.all(routeIds.map(async rid => {
        if (!stationsCache.has(rid)) stationsCache.set(rid, await apiService.getRouteStations(rid))
      }))
    }))

    // Phase 2: preload routes through alighting candidates
    const alightingRoutesMap = new Map<number, any[]>()
    await Promise.all(alightingCandidates.map(async alighting => {
      const routes = await apiService.getStationRoutes(alighting.id)
      alightingRoutesMap.set(alighting.id, routes)
      await Promise.all(routes.map(async (r: any) => {
        if (!stationsCache.has(r.id)) stationsCache.set(r.id, await apiService.getRouteStations(r.id))
      }))
    }))

    const results: PlanResult[] = []
    const seen = new Set<string>()

    // Phase 3: try all boarding × alighting combinations
    for (const boarding of boardingCandidates) {
      const upcoming = boardingScheduleMap.get(boarding.id) ?? []
      if (!upcoming.length) continue
      const routeIdsInSchedule = [...new Set(upcoming.map((e: any) => e.routeId as number))]

      for (const alighting of alightingCandidates) {
        if (boarding.id === alighting.id) continue

        const walkStart = origin.type === 'address'
          ? walkMin(origin.lat, origin.lon, boarding.latitude, boarding.longitude) : undefined
        const walkEnd = dest.type === 'address'
          ? walkMin(alighting.latitude, alighting.longitude, dest.lat, dest.lon) : undefined

        const commonFields = {
          originLat: origin.lat, originLon: origin.lon, originName: origin.name,
          destLat: dest.lat, destLon: dest.lon, destName: dest.name,
          boardingStation: boarding, alightingStation: alighting,
          walkToStartMinutes: walkStart, walkToEndMinutes: walkEnd,
        }

        // === DIRECT ROUTES ===
        for (const routeId of routeIdsInSchedule) {
          const stations = stationsCache.get(routeId)!
          const oIdx = stations.findIndex(s => s.id === boarding.id)
          const dIdx = stations.findIndex(s => s.id === alighting.id)
          if (oIdx === -1 || dIdx === -1 || dIdx === oIdx) continue

          const stationsBetween = Math.abs(dIdx - oIdx)
          for (const entry of upcoming.filter((e: any) => e.routeId === routeId).slice(0, 5)) {
            const depMin = parseInt(entry.departureTime.split(':')[0]) * 60 + parseInt(entry.departureTime.split(':')[1])
            const key = `D-${routeId}-${boarding.id}-${alighting.id}-${entry.departureTime}`
            if (seen.has(key)) continue; seen.add(key)
            results.push({
              type: 'direct', ...commonFields,
              route1Id: routeId, route1Number: entry.routeNumber,
              route1Name: entry.routeName, route1Color: entry.routeColor || '#3b82f6',
              stationsBetween,
              departureTime: minutesToStr(depMin), arrivalTime: minutesToStr(depMin + stationsBetween * 2),
              minutesUntil: depMin - nowMin,
            })
          }
        }

        // === TRANSFER ROUTES ===
        const routesThroughDest = alightingRoutesMap.get(alighting.id) ?? []

        for (const routeAId of routeIdsInSchedule) {
          const stationsA = stationsCache.get(routeAId)!
          const oIdxA = stationsA.findIndex(s => s.id === boarding.id)
          if (oIdxA === -1) continue
          if (!upcoming.find((e: any) => e.routeId === routeAId)) continue

          for (const routeB of routesThroughDest) {
            if (routeB.id === routeAId) continue
            const stationsB = stationsCache.get(routeB.id)
            if (!stationsB) continue
            const dIdxB = stationsB.findIndex(s => s.id === alighting.id)
            if (dIdxB === -1) continue

            let transfer: Station | null = null, tIdxA = -1, tIdxB = -1
            const step = oIdxA < stationsA.length - 1 ? 1 : -1
            for (let i = oIdxA + step; i >= 0 && i < stationsA.length; i += step) {
              const idxB = stationsB.findIndex(s => s.id === stationsA[i]!.id)
              if (idxB !== -1 && idxB !== dIdxB) {
                transfer = stationsA[i]!; tIdxA = i; tIdxB = idxB; break
              }
            }
            if (!transfer) continue

            const r1Stops = Math.abs(tIdxA - oIdxA)
            const r2Stops = Math.abs(dIdxB - tIdxB)

            for (const entry of upcoming.filter((e: any) => e.routeId === routeAId).slice(0, 3)) {
              const depMin = parseInt(entry.departureTime.split(':')[0]) * 60 + parseInt(entry.departureTime.split(':')[1])
              const arrMin = depMin + r1Stops * 2 + 5 + r2Stops * 2
              const key = `T-${routeAId}-${routeB.id}-${boarding.id}-${alighting.id}-${entry.departureTime}`
              if (seen.has(key)) continue; seen.add(key)
              results.push({
                type: 'transfer', ...commonFields,
                route1Id: routeAId, route1Number: entry.routeNumber,
                route1Name: entry.routeName, route1Color: entry.routeColor || '#3b82f6',
                route2Id: routeB.id, route2Number: routeB.routeNumber,
                route2Name: routeB.name, route2Color: (routeB as any).color || '#10b981',
                route1StationsCount: r1Stops, route2StationsCount: r2Stops,
                stationsBetween: r1Stops + r2Stops,
                transferStation: transfer,
                departureTime: minutesToStr(depMin), arrivalTime: minutesToStr(arrMin),
                minutesUntil: depMin - nowMin,
              })
            }
          }
        }
      }
    }

    // Sort: direct first, then by least total walking, then by departure time
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
</script>

<style scoped>
/* ── Layout ─────────────────────────────────────────────────────────────────── */
.sidebar {
  width: 100%;
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--bg-primary);
  color: var(--text-primary);
  border-right: 1px solid var(--border-color);
}

/* ── Header ─────────────────────────────────────────────────────────────────── */
.sidebar-header {
  padding: 20px 18px 18px;
  background: linear-gradient(135deg, #1e3a5f 0%, #2563eb 100%);
  flex-shrink: 0;
}

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
}

.brand-icon {
  font-size: 28px;
  line-height: 1;
  filter: drop-shadow(0 2px 4px rgba(0,0,0,0.3));
}

.brand-text {
  display: flex;
  flex-direction: column;
}

.brand-name {
  font-size: 1.35rem;
  font-weight: 800;
  color: #fff;
  letter-spacing: -0.3px;
  line-height: 1.2;
}

.brand-sub {
  font-size: 0.72rem;
  color: rgba(255,255,255,0.65);
  font-weight: 500;
  letter-spacing: 0.5px;
  text-transform: uppercase;
}

/* ── Scrollable content ─────────────────────────────────────────────────────── */
.sidebar-content {
  flex: 1;
  overflow-y: auto;
  padding: 14px 14px 80px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.sidebar-content::-webkit-scrollbar { width: 4px; }
.sidebar-content::-webkit-scrollbar-track { background: transparent; }
.sidebar-content::-webkit-scrollbar-thumb { background: var(--border-color); border-radius: 2px; }

/* ── Quick actions ──────────────────────────────────────────────────────────── */
.quick-actions {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}

/* ===== SHARED STATES ===== */
.center-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 5px;
  padding: 11px 6px;
  border: none;
  border-radius: 12px;
  cursor: pointer;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.2px;
  transition: all 0.2s ease;
}

.trip-btn  { background: #eff6ff; color: #1d4ed8; }
.fav-btn   { background: #fff1f2; color: #be123c; }
.stats-btn { background: #f5f3ff; color: #6d28d9; }

.action-btn:hover { filter: brightness(0.94); transform: translateY(-1px); }

.trip-btn.active {
  background: linear-gradient(135deg, #10b981, #059669);
  color: white;
  box-shadow: 0 3px 10px rgba(16,185,129,0.3);
}

/* dark-mode overrides */
:global(.dark) .trip-btn  { background: rgba(59,130,246,0.15); color: #93c5fd; }
:global(.dark) .fav-btn   { background: rgba(239,68,68,0.15);  color: #fca5a5; }
:global(.dark) .stats-btn { background: rgba(139,92,246,0.15); color: #c4b5fd; }

/* ── Stats bar ──────────────────────────────────────────────────────────────── */
.quick-stats {
  display: flex;
  align-items: center;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 10px 0;
}

.stat-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}

.stat-value {
  font-size: 1.1rem;
  font-weight: 800;
  color: var(--text-primary);
  line-height: 1;
}

.stat-label {
  font-size: 10px;
  font-weight: 500;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.stat-divider {
  width: 1px;
  height: 28px;
  background: var(--border-color);
}

/* ── Section header ─────────────────────────────────────────────────────────── */
.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.section-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  color: var(--text-secondary);
}
.clear-btn:hover { color: var(--text-primary); }

.section-count {
  font-size: 11px;
  font-weight: 700;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  padding: 2px 7px;
  border-radius: 20px;
  border: 1px solid var(--border-color);
}

/* ── Routes list ────────────────────────────────────────────────────────────── */
.routes-section { display: flex; flex-direction: column; }

.routes-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.route-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 10px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.15s ease;
  text-align: left;
  color: var(--text-primary);
  width: 100%;
}

.route-item:hover {
  background: var(--bg-tertiary);
  border-color: #93c5fd;
  transform: translateX(2px);
}

.route-item.active {
  background: linear-gradient(135deg, #eff6ff 0%, #f5f3ff 100%);
  border-color: #3b82f6;
}

:global(.dark) .route-item.active {
  background: rgba(59,130,246,0.12);
  border-color: #3b82f6;
}

.route-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 34px;
  height: 24px;
  padding: 0 6px;
  border-radius: 6px;
  font-size: 0.72rem;
  font-weight: 800;
  color: #fff;
  letter-spacing: 0.3px;
  flex-shrink: 0;
  text-shadow: 0 1px 2px rgba(0,0,0,0.25);
  box-shadow: 0 1px 3px rgba(0,0,0,0.15);
}

.route-name {
  flex: 1;
  font-size: 0.82rem;
  font-weight: 500;
  line-height: 1.3;
  color: var(--text-primary);
}
.eta-direction { display: block; font-size: 11px; color: var(--text-secondary); margin-top: 1px; }

.route-arrow {
  color: var(--text-tertiary);
  flex-shrink: 0;
  transition: transform 0.15s;
}

.route-item:hover .route-arrow,
.route-item.active .route-arrow {
  transform: translateX(2px);
  color: #3b82f6;
}

/* ── Stations list ──────────────────────────────────────────────────────────── */
.stations-section {
  display: flex;
  flex-direction: column;
  padding-top: 4px;
  border-top: 1px solid var(--border-color);
}

.stations-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  max-height: 320px;
  overflow-y: auto;
  padding-left: 6px;
}

.stations-list::-webkit-scrollbar { width: 3px; }
.stations-list::-webkit-scrollbar-thumb { background: var(--border-color); border-radius: 2px; }

.stations-list li {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 8px 6px 0;
  position: relative;
  font-size: 0.82rem;
  color: var(--text-primary);
  border-left: 2px solid var(--border-color);
  padding-left: 14px;
  margin-left: 6px;
}

.stations-list li:last-child {
  border-left-color: transparent;
}
.chip-close:hover { color: #ef4444; }

.stop-dot {
  position: absolute;
  left: -5px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--text-tertiary);
  border: 2px solid var(--bg-primary);
  flex-shrink: 0;
}

.stop-dot.first, .stop-dot.last {
  background: #3b82f6;
  width: 10px;
  height: 10px;
  left: -6px;
}

.stop-name {
  font-weight: 500;
  line-height: 1.3;
}

/* ── States ─────────────────────────────────────────────────────────────────── */
@keyframes shimmer {
  0%   { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.skeleton-list {
  padding: 8px 12px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.skeleton-route-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 8px;
  border-radius: 10px;
  background: var(--bg-secondary);
}

.skeleton-badge {
  width: 36px;
  height: 22px;
  border-radius: 6px;
  background: linear-gradient(90deg, var(--border-color) 0%, var(--bg-primary) 50%, var(--border-color) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
  flex-shrink: 0;
}

.skeleton-text {
  flex: 1;
  height: 13px;
  border-radius: 6px;
  background: linear-gradient(90deg, var(--border-color) 0%, var(--bg-primary) 50%, var(--border-color) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
  animation-delay: 0.1s;
}

.skeleton-stations {
  padding: 4px 8px;
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
  background: linear-gradient(90deg, var(--border-color) 0%, var(--bg-primary) 50%, var(--border-color) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
  margin-left: 20px;
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

.retry-btn {
  margin-top: 10px;
  padding: 8px 16px;
  background: #3b82f6;
  color: white;
  border: none;
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
