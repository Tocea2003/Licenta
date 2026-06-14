<template>
  <div class="station-details-page">
    <div class="header">
      <button @click="goBack" class="back-btn">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
          <path d="M19 12H5M5 12L12 19M5 12L12 5" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      <div class="station-info">
        <h1>{{ station?.name || t('stationLoading') }}</h1>
        <p v-if="station" class="coordinates">📍 {{ station.latitude.toFixed(6) }}, {{ station.longitude.toFixed(6) }}</p>
      </div>
    </div>

    <div v-if="loading" class="loading-state">
      <div class="skeleton-container">
        <div class="skeleton-header">
          <SkeletonLoader variant="circular" :width="60" :height="60" />
          <div class="skeleton-text">
            <SkeletonLoader variant="text" width="70%" :height="24" />
            <SkeletonLoader variant="text" width="50%" :height="16" />
          </div>
        </div>
        
        <div class="skeleton-section">
          <SkeletonLoader variant="text" width="40%" :height="20" />
          <div class="skeleton-cards">
            <div v-for="i in 3" :key="i" class="skeleton-eta-card">
              <SkeletonLoader variant="rounded" :width="60" :height="60" />
              <div class="skeleton-eta-info">
                <SkeletonLoader variant="text" width="100%" :height="18" />
                <SkeletonLoader variant="text" width="70%" :height="14" />
              </div>
              <SkeletonLoader variant="text" width="60px" :height="32" />
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="error" class="error-state">
      <p>❌ {{ error }}</p>
      <button @click="loadStationData" class="retry-btn">{{ t('retry') }}</button>
    </div>

    <div v-else class="content">
      <!-- Live ETAs Section -->
      <div class="etas-section">
        <h2>🚌 {{ t('busesNearby') }}</h2>
        <div v-if="liveETAs.length === 0" class="empty-state">
          <p>{{ t('noBusesNextHour') }}</p>
        </div>
        <div v-else class="etas-list">
          <div 
            v-for="eta in liveETAs" 
            :key="`${eta.routeNumber}-${eta.arrivalTime}`"
            class="eta-card"
            :style="{ borderLeftColor: eta.color }"
          >
            <div class="route-badge" :style="{ background: eta.color }">
              {{ eta.routeNumber }}
            </div>
            <div class="eta-info">
              <h3>{{ eta.routeName }}</h3>
              <p class="direction">{{ eta.direction }}</p>
            </div>
            <div class="countdown">
              <span class="time">{{ formatCountdown(eta.countdown) }}</span>
              <span class="label">{{ eta.countdown < 60 ? t('seconds') : t('minutes') }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Routes Passing Through -->
      <div class="routes-section">
        <h2>🔄 {{ t('routesPassing') }}</h2>
        <div v-if="routes.length === 0" class="empty-state">
          <p>{{ t('noRoutesAvailable') }}</p>
        </div>
        <div v-else class="routes-grid">
          <div 
            v-for="route in routes" 
            :key="route.id"
            class="route-card"
            @click="viewRoute(route)"
          >
            <div class="route-number" :style="{ background: route.color }">
              {{ route.routeNumber }}
            </div>
            <div class="route-name">{{ route.name }}</div>
          </div>
        </div>
      </div>

      <!-- Quick Actions -->
      <div class="actions-section">
        <button @click="addToFavorites" class="action-btn favorite">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path d="M20.84 4.61C20.3292 4.099 19.7228 3.69364 19.0554 3.41708C18.3879 3.14052 17.6725 2.99817 16.95 2.99817C16.2275 2.99817 15.5121 3.14052 14.8446 3.41708C14.1772 3.69364 13.5708 4.099 13.06 4.61L12 5.67L10.94 4.61C9.9083 3.57831 8.50903 2.99871 7.05 2.99871C5.59096 2.99871 4.19169 3.57831 3.16 4.61C2.1283 5.64169 1.54871 7.04097 1.54871 8.5C1.54871 9.95903 2.1283 11.3583 3.16 12.39L4.22 13.45L12 21.23L19.78 13.45L20.84 12.39C21.351 11.8792 21.7563 11.2728 22.0329 10.6053C22.3095 9.93789 22.4518 9.22248 22.4518 8.5C22.4518 7.77752 22.3095 7.06211 22.0329 6.39469C21.7563 5.72728 21.351 5.12084 20.84 4.61V4.61Z" 
              stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
          {{ t('addToFavorites') }}
        </button>
        <button @click="toggleNotifications" class="action-btn notify" :class="{ active: notificationsActive }">
          {{ notificationsActive ? '🔕' : '🔔' }} {{ notificationsActive ? 'Dezactivează Notificări' : t('enableNotifications') }}
        </button>
      </div>

      <!-- Notification feedback -->
      <Transition name="fade">
        <div v-if="notificationMessage" class="notification-feedback" :class="notificationMessageType">
          {{ notificationMessage }}
        </div>
      </Transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import apiService, { type Station, type Route, type StationScheduleEntry } from '@/services/apiService'
import { useFavorites } from '@/composables/useFavorites'
import { enableNotifications as enableNotificationsComposable, disableNotifications, useNotifications } from '@/composables/useNotifications'
import SkeletonLoader from '@/components/SkeletonLoader.vue'
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
const error = ref<string | null>(null)

let countdownInterval: number | null = null
let scheduleRefreshInterval: number | null = null

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
  if (seconds < 60) {
    return seconds.toString()
  }
  const minutes = Math.floor(seconds / 60)
  return minutes.toString()
}

const goBack = () => {
  router.back()
}

const viewRoute = (routeData: Route) => {
  router.push('/')
  // TODO: trigger route selection in main map
}

const addToFavorites = () => {
  if (!station.value) return
  
  addFavorite({
    name: station.value.name,
    address: `Stație autobuz ${station.value.name}`,
    lat: station.value.latitude,
    lon: station.value.longitude,
    type: 'custom',
    icon: '🚏'
  })
  
  alert(`✅ ${t('stationAddedFavorite')}`)
}

const { settings: notifSettings } = useNotifications()
const notificationsActive = ref(false)
const notificationMessage = ref('')
const notificationMessageType = ref<'success' | 'error' | 'info'>('info')

const toggleNotifications = async () => {
  if (!station.value) return

  if (notificationsActive.value) {
    disableNotifications()
    notificationsActive.value = false
    notificationMessage.value = 'Notificările au fost dezactivate.'
    notificationMessageType.value = 'info'
  } else {
    if (!('Notification' in window)) {
      notificationMessage.value = 'Browserul nu suportă notificări. Încearcă Chrome, Firefox sau Edge.'
      notificationMessageType.value = 'error'
      return
    }

    const success = await enableNotificationsComposable(stationId.value)
    if (success) {
      notificationsActive.value = true
      notificationMessage.value = `Notificări active pentru stația ${station.value.name}.`
      notificationMessageType.value = 'success'
    } else {
      notificationMessage.value = 'Nu s-au putut activa notificările. Permite notificările în browser.'
      notificationMessageType.value = 'error'
    }
  }

  setTimeout(() => {
    notificationMessage.value = ''
  }, 5000)
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
.station-details-page {
  min-height: 100vh;
  background: var(--bg-secondary);
  padding-bottom: 100px;
}

.header {
  background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
  padding: 20px 24px;
  color: white;
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-btn {
  background: rgba(255, 255, 255, 0.15);
  border: 1.5px solid rgba(255, 255, 255, 0.25);
  border-radius: 10px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
}

.back-btn:hover {
  background: rgba(255, 255, 255, 0.25);
  transform: scale(1.05);
}

.back-btn svg { color: white; }

.station-info h1 {
  margin: 0;
  font-size: 1.35rem;
  font-weight: 700;
}

.coordinates {
  margin: 4px 0 0 0;
  font-size: 0.8rem;
  opacity: 0.85;
}

.loading-state,
.error-state {
  text-align: center;
  padding: 80px 24px;
  color: var(--text-secondary);
}

.skeleton-container { padding: 24px; }

.skeleton-header {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-bottom: 32px;
}

.skeleton-text {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.skeleton-section { margin-bottom: 24px; }

.skeleton-cards {
  margin-top: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.skeleton-eta-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  background: var(--bg-primary);
  border-radius: 12px;
  border: 1.5px solid var(--border-primary);
}

.skeleton-eta-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.retry-btn {
  margin-top: 16px;
  padding: 12px 24px;
  background: var(--accent-primary);
  color: white;
  border: none;
  border-radius: 10px;
  font-weight: 600;
  cursor: pointer;
}

.content {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.etas-section h2,
.routes-section h2 {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.etas-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.eta-card {
  background: var(--bg-primary);
  border-radius: 10px;
  padding: 12px 14px;
  display: flex;
  align-items: center;
  gap: 12px;
  border: 1.5px solid var(--border-primary);
  transition: border-color 0.15s ease, background 0.15s ease;
}

.eta-card:hover {
  border-color: var(--accent-primary);
  background: var(--accent-primary-soft);
}

.route-badge {
  width: 42px;
  height: 42px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-weight: 700;
  font-size: 0.95rem;
  flex-shrink: 0;
}

.eta-info {
  flex: 1;
  min-width: 0;
}

.eta-info h3 {
  margin: 0 0 2px 0;
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.direction {
  margin: 0;
  font-size: 0.75rem;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.02em;
}

.countdown {
  text-align: right;
  flex-shrink: 0;
}

.countdown .time {
  display: block;
  font-size: 1.5rem;
  font-weight: 800;
  color: var(--accent-primary);
  line-height: 1;
}

.countdown .label {
  display: block;
  font-size: 0.7rem;
  color: var(--text-secondary);
  margin-top: 2px;
}

.routes-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 10px;
}

@media (max-width: 360px) {
  .routes-grid {
    grid-template-columns: repeat(auto-fill, minmax(90px, 1fr));
    gap: 8px;
  }
}

.route-card {
  background: var(--bg-primary);
  border: 1.5px solid var(--border-primary);
  border-radius: 10px;
  padding: 14px 10px;
  text-align: center;
  cursor: pointer;
  transition: border-color 0.15s ease, background 0.15s ease;
}

.route-card:hover {
  border-color: var(--accent-primary);
  background: var(--accent-primary-soft);
}

.route-number {
  width: 44px;
  height: 44px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-weight: 700;
  font-size: 1rem;
  margin: 0 auto 8px;
}

.route-name {
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
  line-height: 1.3;
}

.actions-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.action-btn {
  padding: 13px 20px;
  border-radius: 10px;
  border: 1.5px solid transparent;
  font-weight: 600;
  font-size: 0.9rem;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.2s;
}

.action-btn.favorite {
  background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
  color: white;
}

.action-btn.favorite:hover {
  filter: brightness(1.1);
  transform: translateY(-1px);
}

.action-btn.notify {
  background: var(--bg-primary);
  border-color: var(--border-primary);
  color: var(--text-primary);
}

.action-btn.notify:hover {
  border-color: var(--accent-primary);
  color: var(--accent-primary);
}

.action-btn.notify.active {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  border-color: #3b82f6;
  color: white;
}

.action-btn.notify.active:hover {
  background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
  color: white;
}

.notification-feedback {
  padding: 10px 14px;
  border-radius: 10px;
  font-size: 0.85rem;
  font-weight: 500;
  text-align: center;
  margin-top: 4px;
}

.notification-feedback.success {
  background: rgba(16, 185, 129, 0.1);
  color: #10b981;
  border: 1px solid rgba(16, 185, 129, 0.2);
}

.notification-feedback.error {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
  border: 1px solid rgba(239, 68, 68, 0.2);
}

.notification-feedback.info {
  background: rgba(59, 130, 246, 0.1);
  color: var(--accent-primary);
  border: 1px solid rgba(59, 130, 246, 0.2);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.empty-state {
  text-align: center;
  padding: 40px 20px;
  color: var(--text-secondary);
  font-size: 0.9rem;
}
</style>
