<template>
  <div class="trip-planner-page">
    <div class="trip-header">
      <h1>Planificare Călătorie</h1>
      <p class="subtitle">Alege punctele de plecare și destinație</p>
    </div>

    <div class="search-wrapper">
      <EnhancedSearch
        :stations="allStations"
        :user-location="userLocation"
        :trip-mode="true"
        @route-search-requested="handleRouteSearchRequested"
      />
    </div>

    <!-- Loading skeletons -->
    <div v-if="isLoading" class="route-results">
      <div v-for="i in 3" :key="i" class="route-card skeleton-card">
        <div class="sk-header">
          <div class="sk-block sk-w40"></div>
          <div class="sk-block sk-w20"></div>
        </div>
        <div class="sk-segments">
          <div class="sk-segment">
            <div class="sk-circle"></div>
            <div class="sk-block sk-w70"></div>
            <div class="sk-block sk-w15"></div>
          </div>
          <div class="sk-segment">
            <div class="sk-circle"></div>
            <div class="sk-block sk-w50"></div>
            <div class="sk-block sk-w15"></div>
          </div>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-if="errorMessage" class="results-error">
      <p>{{ errorMessage }}</p>
    </div>

    <!-- Route Results -->
    <div v-if="!isLoading && routeResults.length > 0" class="route-results">
      <h2>Rute disponibile</h2>
      <div
        v-for="route in routeResults"
        :key="route.routeRank"
        class="route-card"
      >
        <div class="route-card-header">
          <span class="route-category">{{ route.routeCategory }}</span>
          <span class="route-duration">{{ route.totalDuration }} min</span>
        </div>
        <div class="route-segments">
          <div
            v-for="(segment, idx) in route.segments"
            :key="idx"
            class="segment"
            :class="segment.type"
          >
            <span class="segment-icon">{{ segmentIcon(segment.type) }}</span>
            <div class="segment-info">
              <span v-if="segment.type === 'bus'" class="segment-name">
                Linia {{ segment.routeNumber }} — {{ segment.stationCount }} stații
              </span>
              <span v-else-if="segment.type === 'walk'" class="segment-name">
                Mers pe jos {{ segment.distance?.toFixed(2) }} km
              </span>
              <span v-else class="segment-name">Transfer</span>
              <span class="segment-duration">{{ segment.duration }} min</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import EnhancedSearch from '@/components/EnhancedSearch.vue'
import apiService, { type Station } from '@/services/apiService'

const allStations = ref<Station[]>([])
const userLocation = ref<{ lat: number; lon: number } | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
const routeResults = ref<any[]>([])

const segmentIcon = (type: string) => {
  if (type === 'bus') return '🚌'
  if (type === 'walk') return '🚶'
  return '🔄'
}

const findNearestStation = (lat: number, lon: number): Station | null => {
  if (!allStations.value.length) return null
  let nearest: Station | null = null
  let minDist = Infinity
  for (const s of allStations.value) {
    const d = Math.hypot(s.latitude - lat, s.longitude - lon)
    if (d < minDist) { minDist = d; nearest = s }
  }
  return nearest
}

const handleRouteSearchRequested = async (
  origin: { lat: number; lon: number; name: string },
  destination: { lat: number; lon: number; name: string }
) => {
  errorMessage.value = ''
  routeResults.value = []

  const startStation = findNearestStation(origin.lat, origin.lon)
  const endStation = findNearestStation(destination.lat, destination.lon)

  if (!startStation || !endStation) {
    errorMessage.value = 'Nu s-au găsit stații în apropiere.'
    return
  }

  if (startStation.id === endStation.id) {
    errorMessage.value = 'Stația de plecare și destinație sunt identice. Alege locații diferite.'
    return
  }

  isLoading.value = true
  try {
    const routes = await apiService.getRouteAlternatives(startStation.id, endStation.id)
    routeResults.value = routes
    if (routes.length === 0) {
      errorMessage.value = 'Nu s-au găsit rute între aceste stații.'
    }
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message ?? 'Eroare la calcularea rutei. Încearcă din nou.'
  } finally {
    isLoading.value = false
  }
}

const loadStations = async () => {
  try {
    allStations.value = await apiService.getStations()
  } catch (error) {
    console.error('Error loading stations:', error)
  }
}

onMounted(() => {
  loadStations()
  if ('geolocation' in navigator) {
    navigator.geolocation.getCurrentPosition(
      (pos) => { userLocation.value = { lat: pos.coords.latitude, lon: pos.coords.longitude } },
      () => {}
    )
  }
})
</script>

<style scoped>
.trip-planner-page {
  min-height: 100vh;
  background: var(--gradient-bg);
  padding: 40px 20px;
  padding-bottom: 100px;
}

.trip-header {
  text-align: center;
  margin-bottom: 24px;
}

.trip-header h1 {
  font-size: 1.5rem;
  font-weight: 800;
  color: var(--text-primary);
  margin: 0 0 6px 0;
}

.subtitle {
  color: var(--text-secondary);
  font-size: 0.875rem;
  margin: 0;
}

.search-wrapper {
  max-width: 600px;
  margin: 0 auto 24px;
  position: relative;
  /* Override the absolute positioning of EnhancedSearch so it fits in the page flow */
}

.search-wrapper :deep(.enhanced-search-container) {
  position: relative;
  top: auto;
  left: auto;
  transform: none;
  width: 100%;
  max-width: 100%;
}

/* ── Skeleton cards ── */
@keyframes shimmer {
  0%   { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.skeleton-card { pointer-events: none; }

.sk-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 14px;
}

.sk-segments {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.sk-segment {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--bg-secondary);
}

.sk-block, .sk-circle {
  background: linear-gradient(90deg, var(--border-color) 0%, var(--bg-secondary) 50%, var(--border-color) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.4s infinite;
  border-radius: 6px;
  height: 12px;
}

.sk-circle {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sk-w15 { width: 15%; }
.sk-w20 { width: 20%; }
.sk-w40 { width: 40%; }
.sk-w50 { width: 50%; }
.sk-w70 { flex: 1; }

.results-error {
  max-width: 600px;
  margin: 16px auto;
  padding: 14px 16px;
  background: #fee2e2;
  color: #991b1b;
  border-radius: 12px;
  font-size: 0.9rem;
}

.route-results {
  max-width: 600px;
  margin: 0 auto;
}

.route-results h2 {
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 14px;
}

.route-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 14px;
  padding: 16px;
  margin-bottom: 12px;
  box-shadow: var(--shadow-md);
}

.route-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.route-category {
  font-weight: 700;
  font-size: 0.9rem;
  color: var(--text-primary);
}

.route-duration {
  font-weight: 700;
  font-size: 1rem;
  color: var(--color-primary, #2563eb);
}

.route-segments {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.segment {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--bg-secondary);
}

.segment-icon {
  font-size: 1.1rem;
}

.segment-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex: 1;
}

.segment-name {
  font-size: 0.875rem;
  color: var(--text-primary);
}

.segment-duration {
  font-size: 0.8rem;
  color: var(--text-secondary);
}
</style>
