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

      <!-- Routes List -->
      <div v-else class="routes-section">
        <div class="section-header">
          <span class="section-title">Trasee disponibile</span>
          <span class="section-count">{{ routes.length }}</span>
        </div>

        <div class="routes-list">
          <button
            v-for="route in routes"
            :key="route.id"
            class="route-item"
            :class="{ active: selectedRouteId === route.id }"
            @click="selectRoute(route.id)"
          >
            <span
              class="route-badge"
              :style="{ background: route.color || '#3b82f6' }"
            >{{ route.routeNumber }}</span>
            <span class="route-name">{{ route.name }}</span>
            <svg class="route-arrow" width="14" height="14" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Selected Route Stations -->
      <div v-if="selectedRouteId" class="stations-section">
        <div class="section-header">
          <span class="section-title">Stații pe traseu</span>
          <span v-if="!loadingStations" class="section-count">{{ currentStations.length }}</span>
        </div>

        <div v-if="loadingStations" class="skeleton-stations">
          <div v-for="i in 5" :key="i" class="skeleton-station-item"></div>
        </div>

        <ol v-else class="stations-list">
          <li v-for="(station, index) in currentStations" :key="station.id">
            <span class="stop-dot" :class="{ first: index === 0, last: index === currentStations.length - 1 }"></span>
            <span class="stop-name">{{ station.name }}</span>
          </li>
        </ol>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import apiService, { type Route, type Station } from '@/services/apiService'
import { useFavorites } from '@/composables/useFavorites'
import { useStatistics } from '@/composables/useStatistics'

const router = useRouter()
const { favorites } = useFavorites()
const { statistics, recordRouteUsage } = useStatistics()

const emit = defineEmits<{
  routeSelected: [routeId: number, stations: Station[]]
  tripModeChanged: [enabled: boolean]
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
  loadingStations.value = true
  try {
    if (stationsCache.has(routeId)) {
      currentStations.value = stationsCache.get(routeId)!
    } else {
      const stations = await apiService.getRouteStations(routeId)
      currentStations.value = stations
      stationsCache.set(routeId, stations)
    }
    emit('routeSelected', routeId, currentStations.value)
  } catch {
    currentStations.value = []
  } finally {
    loadingStations.value = false
  }
}

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

.action-btn {
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
  gap: 10px;
}

.skeleton-station-item {
  height: 12px;
  border-radius: 6px;
  background: linear-gradient(90deg, var(--border-color) 0%, var(--bg-primary) 50%, var(--border-color) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
  margin-left: 20px;
}

.error-state { color: #ef4444; }

.retry-btn {
  margin-top: 10px;
  padding: 8px 16px;
  background: #3b82f6;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 0.85rem;
  font-weight: 600;
  cursor: pointer;
}
</style>
