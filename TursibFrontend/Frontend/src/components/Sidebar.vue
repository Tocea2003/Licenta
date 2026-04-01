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
            <span>🚏 {{ planOrigin.name }}</span>
            <button @click="clearOrigin" class="chip-close">✕</button>
          </div>
          <div v-else>
            <div class="search-input-wrap">
              <input
                v-model="planOriginQuery"
                @input="onOriginInput"
                placeholder="Caută stație de plecare..."
                class="search-input"
                autocomplete="off"
              />
              <button v-if="planOriginQuery" @click="clearOrigin" class="clear-btn">✕</button>
            </div>
            <div v-if="originSuggestions.length > 0" class="autocomplete">
              <button
                v-for="s in originSuggestions"
                :key="s.id"
                @click="selectOrigin(s)"
                class="autocomplete-item"
              >🚏 {{ s.name }}</button>
            </div>
          </div>
        </div>

        <!-- Destinație -->
        <div class="form-group">
          <label class="form-label">🎯 Destinație</label>
          <div v-if="planDest" class="selected-station-chip">
            <span>🚏 {{ planDest.name }}</span>
            <button @click="clearDest" class="chip-close">✕</button>
          </div>
          <div v-else>
            <div class="search-input-wrap">
              <input
                v-model="planDestQuery"
                @input="onDestInput"
                placeholder="Caută stație destinație..."
                class="search-input"
                autocomplete="off"
              />
              <button v-if="planDestQuery" @click="clearDest" class="clear-btn">✕</button>
            </div>
            <div v-if="destSuggestions.length > 0" class="autocomplete">
              <button
                v-for="s in destSuggestions"
                :key="s.id"
                @click="selectDest(s)"
                class="autocomplete-item"
              >🚏 {{ s.name }}</button>
            </div>
          </div>
        </div>

        <!-- Ora plecării -->
        <div class="form-group">
          <label class="form-label">🕐 Ora plecării</label>
          <input v-model="planTime" type="time" class="time-input" />
        </div>

        <!-- Buton căutare -->
        <button
          @click="searchRoutes"
          :disabled="!planOrigin || !planDest || isSearching"
          class="btn-search-routes"
        >
          <span v-if="isSearching">⏳ Se caută cursele...</span>
          <span v-else>🔍 Caută curse</span>
        </button>
      </div>

      <!-- Rezultate -->
      <div v-if="planResults.length > 0" class="plan-results">
        <div class="results-header">
          <strong>{{ planResults.length }} curse găsite</strong>
          <span class="results-sub">{{ planOrigin?.name }} → {{ planDest?.name }}</span>
        </div>

        <div
          v-for="(result, idx) in planResults"
          :key="`${result.routeNumber}-${result.departureTime}-${idx}`"
          class="result-card"
        >
          <div class="result-top">
            <span class="result-badge" :style="{ background: result.color }">{{ result.routeNumber }}</span>
            <span class="result-route">{{ result.routeName }}</span>
          </div>
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
          <div class="result-meta">
            <span>{{ result.stationsBetween }} stații</span>
            <span>•</span>
            <span>{{ result.direction }}</span>
          </div>
        </div>
      </div>

      <div v-else-if="searchDone" class="center-state">
        <span class="state-icon">🔎</span>
        <p>Nu există curse directe în intervalul selectat.</p>
        <p class="hint-text">Încearcă o altă oră sau stații diferite.</p>
      </div>

      <div v-else-if="!planOrigin && !planDest" class="empty-hero">
        <div class="empty-hero-icon">🧭</div>
        <h3>Planifică o călătorie</h3>
        <p>Selectează stația de plecare, destinația și ora pentru a vedea cursele disponibile.</p>
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
  for (const entry of schedule) {
    const parts = entry.departureTime.split(':')
    if (parts.length < 2) continue
    const entryMinutes = parseInt(parts[0]) * 60 + parseInt(parts[1])
    const diffSeconds = (entryMinutes - currentMinutes) * 60
    if (diffSeconds < 0 || diffSeconds > 3600) continue
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
interface PlanResult {
  routeNumber: string
  routeName: string
  color: string
  departureTime: string
  arrivalTime: string
  stationsBetween: number
  direction: string
}

const planOriginQuery = ref('')
const planDestQuery   = ref('')
const planTime        = ref(new Date().toTimeString().substring(0, 5))
const planOrigin      = ref<Station | null>(null)
const planDest        = ref<Station | null>(null)
const originSuggestions = ref<Station[]>([])
const destSuggestions   = ref<Station[]>([])
const planResults  = ref<PlanResult[]>([])
const isSearching  = ref(false)
const searchDone   = ref(false)

const onOriginInput = () => {
  planOrigin.value = null
  const q = planOriginQuery.value.toLowerCase().trim()
  originSuggestions.value = q && props.allStations
    ? props.allStations.filter(s => s.name.toLowerCase().includes(q)).slice(0, 6)
    : []
}

const onDestInput = () => {
  planDest.value = null
  const q = planDestQuery.value.toLowerCase().trim()
  destSuggestions.value = q && props.allStations
    ? props.allStations.filter(s => s.name.toLowerCase().includes(q)).slice(0, 6)
    : []
}

const selectOrigin = (s: Station) => {
  planOrigin.value = s; planOriginQuery.value = ''; originSuggestions.value = []
}
const selectDest = (s: Station) => {
  planDest.value = s; planDestQuery.value = ''; destSuggestions.value = []
}
const clearOrigin = () => {
  planOrigin.value = null; planOriginQuery.value = ''; originSuggestions.value = []
}
const clearDest = () => {
  planDest.value = null; planDestQuery.value = ''; destSuggestions.value = []
}

const minutesToStr = (totalMin: number): string => {
  const h = Math.floor(totalMin / 60) % 24
  const m = totalMin % 60
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`
}

const searchRoutes = async () => {
  if (!planOrigin.value || !planDest.value) return
  isSearching.value = true
  searchDone.value  = false
  planResults.value = []

  try {
    const [hh, mm] = planTime.value.split(':').map(Number)
    const targetMinutes = hh * 60 + mm

    // 1. Orar stație de plecare
    const schedule = await apiService.getStationSchedule(planOrigin.value.id)

    // 2. Filtrăm cursele în intervalul [ora selectată, +3h]
    const upcoming = schedule.filter(entry => {
      const p = entry.departureTime.split(':')
      const entryMin = parseInt(p[0]) * 60 + parseInt(p[1])
      return entryMin >= targetMinutes && entryMin <= targetMinutes + 180
    })

    if (!upcoming.length) { searchDone.value = true; return }

    // 3. Route-uri unice din schedule
    const uniqueRouteIds = [...new Set(upcoming.map(e => e.routeId))]

    // 4. Verificăm dacă destinația e pe același traseu, după origine
    const results: PlanResult[] = []
    for (const routeId of uniqueRouteIds) {
      let stations: Station[]
      if (stationsCache.has(routeId)) {
        stations = stationsCache.get(routeId)!
      } else {
        stations = await apiService.getRouteStations(routeId)
        stationsCache.set(routeId, stations)
      }

      const originIdx = stations.findIndex(s => s.id === planOrigin.value!.id)
      const destIdx   = stations.findIndex(s => s.id === planDest.value!.id)

      // Destinația trebuie să fie după origine pe traseu
      if (originIdx === -1 || destIdx === -1 || destIdx <= originIdx) continue

      const stationsBetween = destIdx - originIdx
      for (const entry of upcoming.filter(e => e.routeId === routeId)) {
        const depParts = entry.departureTime.split(':')
        const depMin   = parseInt(depParts[0]) * 60 + parseInt(depParts[1])
        const arrMin   = depMin + stationsBetween * 2 // ~2 min per stație

        results.push({
          routeNumber:     entry.routeNumber,
          routeName:       entry.routeName,
          color:           entry.routeColor || '#3b82f6',
          departureTime:   minutesToStr(depMin),
          arrivalTime:     minutesToStr(arrMin),
          stationsBetween,
          direction:       entry.direction || (entry.directionId === 0 ? 'Dus' : 'Întors'),
        })
      }
    }

    planResults.value = results.sort((a, b) => a.departureTime.localeCompare(b.departureTime))
    searchDone.value  = true
  } catch (err) {
    console.error('❌ Eroare la căutare curse:', err)
    searchDone.value = true
  } finally {
    isSearching.value = false
  }
}

// ===================== LIFECYCLE =====================
onMounted(loadRoutes)
onUnmounted(() => { if (countdownTimer) clearInterval(countdownTimer) })
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
}

.result-top { display: flex; align-items: center; gap: 8px; }

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

.result-route {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

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

.result-meta {
  display: flex;
  gap: 6px;
  font-size: 11px;
  color: var(--text-secondary);
}

.hint-text { font-size: 12px; color: var(--text-tertiary); margin-top: 4px; }

/* ===== TRANSITION ===== */
.slide-down-enter-active,
.slide-down-leave-active { transition: all 0.25s ease; }
.slide-down-enter-from,
.slide-down-leave-to { opacity: 0; transform: translateY(-8px); }
</style>
