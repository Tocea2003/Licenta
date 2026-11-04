<template>
  <div class="station-page">
    <header class="board-header">
      <div class="board-actions">
        <button type="button" @click="goBack" class="board-btn" :aria-label="t('back')">
          <ArrowLeft :size="20" aria-hidden="true" />
        </button>
        <span class="board-spacer"></span>
        <button type="button" @click="addToFavorites" class="board-btn" :aria-label="t('addToFavorites')" :disabled="!station">
          <Star :size="20" aria-hidden="true" :class="{ 'is-starred': favoriteAdded }" />
        </button>
        <button
          type="button"
          @click="toggleNotifications"
          class="board-btn"
          :class="{ active: notificationsActive }"
          :aria-pressed="notificationsActive"
          :aria-label="notificationsActive ? t('notifOff') : t('enableNotifications')"
          :disabled="!station"
        >
          <BellRing v-if="notificationsActive" :size="20" aria-hidden="true" />
          <Bell v-else :size="20" aria-hidden="true" />
        </button>
      </div>

      <span class="board-eyebrow">{{ t('stationLabel') }}</span>
      <h1 class="board-title">{{ station?.name || t('stationLoading') }}</h1>
      <span v-if="stationLines.length" class="board-sub">{{ t('linesCount', '', { n: stationLines.length }) }}</span>

      <div v-if="stationLines.length > 1" class="line-filter" role="group" :aria-label="t('routesPassing')">
        <button type="button" :class="{ active: selectedLine === null }" :aria-pressed="selectedLine === null" @click="selectedLine = null">{{ t('allLines') }}</button>
        <button
          v-for="line in stationLines"
          :key="line"
          type="button"
          :class="{ active: selectedLine === line }"
          :aria-pressed="selectedLine === line"
          @click="selectedLine = selectedLine === line ? null : line"
        >{{ line }}</button>
      </div>
    </header>

    <Transition name="fade">
      <div v-if="notificationMessage" class="notification-feedback" :class="notificationMessageType" role="status">
        {{ notificationMessage }}
      </div>
    </Transition>

    <div v-if="loading" class="loading-state" aria-busy="true">
      <div v-for="i in 4" :key="i" class="skeleton" style="height: 60px"></div>
    </div>

    <div v-else-if="error" class="error-state" role="alert">
      <p>{{ error }}</p>
      <button type="button" @click="loadStationData" class="btn btn-secondary">{{ t('retry') }}</button>
    </div>

    <section v-else class="departures-section" :aria-label="t('departuresTitle')">
      <div class="departures-head">
        <span>{{ t('lineDirection') }}</span>
        <span>{{ t('departsColumn') }}</span>
      </div>

      <p v-if="visibleETAs.length === 0" class="empty-state">{{ t('noBusesNextHour') }}</p>

      <ul v-else class="departures">
        <li v-for="eta in visibleETAs" :key="`${eta.routeNumber}-${eta.arrivalTime}`" class="departure">
          <span class="line-pill line-pill--lg" :style="{ background: eta.color }">{{ eta.routeNumber }}</span>
          <span class="departure-info">
            <span class="departure-dest">{{ eta.direction }}</span>
            <span class="departure-sub">{{ eta.routeName }}</span>
          </span>
          <span class="departure-when">
            <span class="departure-minutes">{{ formatCountdown(eta.countdown) }}</span>
            <span class="departure-clock">{{ formatClock(eta.arrivalTime) }} · {{ t('fromSchedule') }}</span>
          </span>
        </li>
      </ul>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import apiService, { type Station, type Route, type StationScheduleEntry } from '@/services/apiService'
import { useFavorites } from '@/composables/useFavorites'
import { enableNotifications as enableNotificationsComposable, disableNotifications } from '@/composables/useNotifications'
import { ArrowLeft, Bell, BellRing, Star } from 'lucide-vue-next'
import { authService } from '@/services/adminService'
import { useLanguage } from '@/composables/useLanguage'

interface LiveETA {
  routeNumber: string
  routeName: string
  direction: string
  arrivalTime: number // timestamp
  countdown: number // seconds
  color: string
}

const route = useRoute()
const router = useRouter()
const { addFavorite } = useFavorites()
const { t } = useLanguage()

const stationId = computed(() => parseInt(route.params.id as string))
const station = ref<Station | null>(null)
const routes = ref<Route[]>([])
const liveETAs = ref<LiveETA[]>([])
const loading = ref(true)
const selectedLine = ref<string | null>(null)
const allScheduleLines = ref<string[]>([])
const favoriteAdded = ref(false)

// Liniile care opresc aici: din orarul real (StopTimes) plus rutele asociate stației.
// RouteStations vine dintr-o singură cursă reprezentativă, deci poate omite linii.
const stationLines = computed(() => {
  const lines = new Set<string>([...routes.value.map(r => r.routeNumber), ...allScheduleLines.value])
  return [...lines].sort((a, b) => a.localeCompare(b, 'ro', { numeric: true }))
})

const visibleETAs = computed(() =>
  selectedLine.value ? liveETAs.value.filter(eta => eta.routeNumber === selectedLine.value) : liveETAs.value
)
const error = ref<string | null>(null)

let countdownInterval: number | null = null
let scheduleRefreshInterval: number | null = null

const notificationsActive = ref(false)
const notificationMessage = ref('')
const notificationMessageType = ref<'success' | 'error' | 'info'>('info')

const loadStationData = async () => {
  loading.value = true
  error.value = null
  
  try {
    // Load station details
    station.value = await apiService.getStation(stationId.value)
    
    // Load routes passing through this station directly from DB
    routes.value = await apiService.getStationRoutes(stationId.value)
    
    // Load real schedule from DB and build upcoming ETAs
    const schedule = await apiService.getStationSchedule(stationId.value)
    buildETAsFromSchedule(schedule)
    
  } catch (err: any) {
    if (err?.response?.status === 404) {
      error.value = t('stationNotFound')
    } else {
      console.error('Error loading station data:', err)
      error.value = t('stationLoadError')
    }
  } finally {
    loading.value = false
  }
}

const buildETAsFromSchedule = (schedule: StationScheduleEntry[]) => {
  allScheduleLines.value = [...new Set(schedule.map(entry => entry.routeNumber))]
  const now = new Date()
  const currentMinutes = now.getHours() * 60 + now.getMinutes()
  const etas: LiveETA[] = []

  for (const entry of schedule) {
    // Parse "HH:MM:SS" – GTFS allows hours >= 24 for trips past midnight
    const parts = entry.departureTime.split(':')
    if (parts.length < 2) continue
    const hoursPart = parts[0]
    const minutesPart = parts[1]
    if (hoursPart === undefined || minutesPart === undefined) continue
    const hours = parseInt(hoursPart)
    const minutes = parseInt(minutesPart)
    const entryMinutes = hours * 60 + minutes

    const diffSeconds = (entryMinutes - currentMinutes) * 60
    // Show only buses arriving in the next 60 minutes
    if (diffSeconds < 0 || diffSeconds > 3600) continue

    etas.push({
      routeNumber: entry.routeNumber,
      routeName: entry.routeName,
      direction: entry.direction || (entry.directionId === 0 ? t('outbound') : t('inbound')),
      arrivalTime: Date.now() + diffSeconds * 1000,
      countdown: diffSeconds,
      color: entry.routeColor || '#3b82f6'
    })
  }

  liveETAs.value = etas.sort((a, b) => a.arrivalTime - b.arrivalTime).slice(0, 10)
}

const updateCountdowns = () => {
  const now = Date.now()
  
  liveETAs.value = liveETAs.value.map(eta => {
    const countdown = Math.max(0, Math.floor((eta.arrivalTime - now) / 1000))
    return { ...eta, countdown }
  }).filter(eta => eta.countdown > 0) // Remove expired ETAs
}

const formatCountdown = (seconds: number): string => {
  if (seconds < 60) return '<1 min'
  return `${Math.floor(seconds / 60)} min`
}

const formatClock = (timestamp: number): string =>
  new Date(timestamp).toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })

const goBack = () => {
  router.back()
}

const showMessage = (message: string, type: 'success' | 'error' | 'info') => {
  notificationMessage.value = message
  notificationMessageType.value = type
  setTimeout(() => { notificationMessage.value = '' }, 5000)
}

const addToFavorites = async () => {
  if (!station.value) return
  if (!authService.isAuthenticated()) {
    showMessage(t('loginRequired'), 'error')
    return
  }

  const added = await addFavorite({
    name: station.value.name,
    address: station.value.name,
    lat: station.value.latitude,
    lon: station.value.longitude,
    type: 'custom',
    icon: '🚏'
  })
  if (added) {
    favoriteAdded.value = true
    showMessage(t('stationAddedFavorite'), 'success')
  }
}


const toggleNotifications = async () => {
  if (!station.value) return

  if (notificationsActive.value) {
    disableNotifications()
    notificationsActive.value = false
    showMessage(t('notifDisabledMsg'), 'info')
    return
  }

  if (!('Notification' in window)) {
    showMessage(t('notifUnsupported'), 'error')
    return
  }

  const success = await enableNotificationsComposable(stationId.value)
  if (success) {
    notificationsActive.value = true
    showMessage(t('notifEnabledForStation', '', { station: station.value.name }), 'success')
  } else {
    showMessage(t('notifPermissionNeeded'), 'error')
  }
}

onMounted(() => {
  loadStationData()

  // Actualizează countdown-ul la fiecare secundă
  countdownInterval = window.setInterval(updateCountdowns, 1000)

  // Reîncarcă orarul la fiecare 5 minute (ore noi devin disponibile)
  scheduleRefreshInterval = window.setInterval(async () => {
    try {
      const schedule = await apiService.getStationSchedule(stationId.value)
      buildETAsFromSchedule(schedule)
    } catch {
      // ignorăm erori silențioase la refresh
    }
  }, 5 * 60 * 1000)
})

onUnmounted(() => {
  if (countdownInterval) clearInterval(countdownInterval)
  if (scheduleRefreshInterval) clearInterval(scheduleRefreshInterval)
})
</script>

<style scoped>
.station-page {
  display: flex;
  flex-direction: column;
  font-variant-numeric: tabular-nums;
}

.board-header {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px 16px 20px;
  background: #16181d;
  color: #fff;
}

.board-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.board-spacer {
  flex: 1;
}

.board-btn {
  width: 44px;
  height: 44px;
  border: 1px solid #3a3e48;
  border-radius: var(--radius-md);
  background: transparent;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.board-btn:first-child {
  border-color: transparent;
}

.board-btn:hover:not(:disabled) {
  background: #2b2e36;
}

.board-btn.active {
  background: #fff;
  color: #16181d;
}

.board-btn:disabled {
  opacity: 0.5;
}

.is-starred {
  fill: #e8a100;
  stroke: #e8a100;
}

.board-eyebrow {
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: #b9bcc4;
}

.board-title {
  margin: 0;
  font-size: 32px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.02em;
  line-height: 1.1;
}

.board-sub {
  font-size: 14px;
  color: #d5d7dc;
}

.line-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.line-filter button {
  min-width: 44px;
  min-height: 36px;
  padding: 0 12px;
  border-radius: 18px;
  border: 1px solid #3a3e48;
  background: transparent;
  color: #fff;
  font: inherit;
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: var(--fw-heavy);
  cursor: pointer;
}

.line-filter button.active {
  background: #fff;
  border-color: #fff;
  color: #16181d;
}

.notification-feedback {
  margin: 12px 16px 0;
  padding: 12px 14px;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

.notification-feedback.success { background: var(--color-success-soft); color: var(--color-success); }
.notification-feedback.error { background: var(--color-danger-soft); color: var(--color-danger); }
.notification-feedback.info { background: var(--bg-tertiary); color: var(--text-primary); }

.loading-state,
.error-state {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px;
}

.error-state {
  align-items: flex-start;
  color: var(--color-danger);
}

.departures-section {
  display: flex;
  flex-direction: column;
  padding: 0 12px 24px;
}

.departures-head {
  display: flex;
  justify-content: space-between;
  padding: 14px 8px 8px;
  font-size: 12px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.empty-state {
  margin: 0;
  padding: 24px 8px;
  color: var(--text-secondary);
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
  padding: 12px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
}

.departure-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.departure-dest {
  font-size: 16px;
  font-weight: var(--fw-bold);
}

.departure-sub {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.departure-when {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
}

.departure-minutes {
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: var(--fw-heavy);
  white-space: nowrap;
}

.departure-clock {
  font-size: 12px;
  color: var(--text-secondary);
  white-space: nowrap;
}

.fade-enter-active,
.fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from,
.fade-leave-to { opacity: 0; }
</style>
