<template>
  <Transition name="slide-up">
    <div v-if="routes.length > 0" class="alternatives-panel">
      <div class="panel-header">
        <div class="header-content">
          <div>
            <h3>{{ t('alternativeRoutes') }}</h3>
            <p class="subtitle">{{ routes.length }} {{ routes.length === 1 ? t('optionAvailable') : t('optionsAvailable') }}</p>
          </div>
        </div>
        <button @click="$emit('close')" class="close-btn" :aria-label="t('close')">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M18 6L6 18M6 6l12 12"/>
          </svg>
        </button>
      </div>

      <div class="routes-container">
        <TransitionGroup name="route-list" tag="div" class="routes-list">
          <div
            v-for="(route, index) in routes"
            :key="`route-${index}`"
            role="button"
            tabindex="0"
            @keydown.enter.prevent="selectRoute(index)"
            @keydown.space.prevent="selectRoute(index)"
            class="route-card"
            :class="{ 
              'selected': selectedRouteIndex === index,
              'fastest': route.routeRank === 1
            }"
            @click="selectRoute(index)"
          >
            <!-- Badge pentru cea mai rapidă -->
            <div v-if="route.routeType === 'walk-only'" class="category-badge walk">
              <Footprints :size="14" aria-hidden="true" />
              {{ t('walkOnFoot') }}
            </div>
            <div v-else-if="route.routeRank === 1" class="fastest-badge">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                <path d="M13 2L3 14h8l-1 8 10-12h-8l1-8z"/>
              </svg>
              {{ t('fastest') }}
            </div>
            <div v-else-if="route.routeType === 'direct'" class="category-badge direct">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M5 12h14M12 5l7 7-7 7"/>
              </svg>
              {{ t('directRoute') }}
            </div>
            <div v-else class="category-badge transfer">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M7 16V4M7 4L3 8M7 4l4 4M17 8v12M17 20l4-4M17 20l-4-4"/>
              </svg>
              {{ t('withTransfer') }}
            </div>

            <!-- Header cu timp și tip -->
            <div class="route-header">
              <div class="duration-info">
                <div class="time-circle">
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"/>
                    <path d="M12 6v6l4 2"/>
                  </svg>
                </div>
                <div>
                  <span class="duration">{{ route.totalDuration }}</span>
                  <span class="duration-unit">min</span>
                </div>
              </div>
              <div class="route-stats">
                <div class="stat-pill">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="10" r="3"/>
                    <path d="M12 21.7C17.3 17 20 13 20 10a8 8 0 1 0-16 0c0 3 2.7 7 8 11.7z"/>
                  </svg>
                  {{ getTotalStations(route) }}
                </div>
                <div v-if="route.routeType === 'transfer'" class="stat-pill transfer">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M7 16V4M7 4L3 8M7 4l4 4M17 8v12M17 20l4-4M17 20l-4-4"/>
                  </svg>
                  1 {{ t('transferLabel') }}
                </div>
              </div>
            </div>

            <div v-if="route.routeCategory" class="route-reason">
              {{ route.routeCategory }}
            </div>

            <!-- Segments -->
            <div class="route-segments">
              <div
                v-for="(segment, segIndex) in route.segments"
                :key="`seg-${index}-${segIndex}`"
                class="segment"
                :class="`segment-${segment.type}`"
              >
                <div v-if="segment.type === 'bus'" class="bus-segment">
                  <div class="route-badge" :style="{ backgroundColor: segment.color || '#667eea' }">
                    {{ segment.routeNumber }}
                  </div>
                  <div class="segment-details">
                    <div class="segment-line">
                      <span class="station-name">{{ segment.startStation?.name }}</span>
                      <span class="arrow">→</span>
                      <span class="station-name">{{ segment.endStation?.name }}</span>
                    </div>
                    <div class="segment-meta">
                      <span class="route-name">{{ segment.routeName }}</span>
                      <span class="separator">•</span>
                      <span class="segment-duration">{{ segment.duration }} min</span>
                    </div>
                  </div>
                </div>

                <div v-else-if="segment.type === 'transfer'" class="transfer-segment">
                  <div class="transfer-icon">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M7 16V4M7 4L3 8M7 4l4 4M17 8v12M17 20l4-4M17 20l-4-4"/>
                    </svg>
                  </div>
                  <div class="transfer-details">
                    <span class="transfer-label">{{ t('transferAt') }} {{ segment.station?.name }}</span>
                    <span class="transfer-duration">{{ t('waitApprox') }} {{ segment.duration }} min</span>
                  </div>
                </div>

                <div v-else-if="segment.type === 'walk'" class="walk-segment">
                  <div class="walk-icon"><Footprints :size="18" aria-hidden="true" /></div>
                  <div class="walk-details">
                    <span class="walk-label">{{ t('walkOnFoot') }}</span>
                    <span class="walk-duration">{{ segment.duration }} min{{ segment.distance ? ` · ${segment.distance} m` : '' }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Actions -->
            <div class="route-actions">
              <button 
                @click.stop="selectRouteForNavigation(index)" 
                class="select-route-btn"
                :class="{ 'selected-btn': selectedRouteIndex === index }"
              >
                <svg v-if="selectedRouteIndex === index" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                  <path d="M20 6L9 17l-5-5"/>
                </svg>
                <svg v-else width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M5 12h14M12 5l7 7-7 7"/>
                </svg>
                {{ selectedRouteIndex === index ? t('routeSelected') : t('selectRoute') }}
              </button>
            </div>
          </div>
        </TransitionGroup>
      </div>

      <!-- Comparison Footer -->
      <div class="comparison-footer">
        <div class="comparison-stat">
          <span class="stat-label">{{ t('fastest') }}</span>
          <span class="stat-value">{{ fastestRoute?.totalDuration }} min</span>
        </div>
        <div class="comparison-stat">
          <span class="stat-label">{{ t('noTransfer') }}</span>
          <span class="stat-value">{{ directRoutesCount }} {{ directRoutesCount === 1 ? t('optionSingular') : t('optionPlural') }}</span>
        </div>
        <div class="comparison-stat">
          <span class="stat-label">{{ t('saveTime') }}</span>
          <span class="stat-value">{{ timeSavings }} min</span>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { Footprints } from 'lucide-vue-next'
import { ref, computed } from 'vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

interface RouteSegment {
  type: string
  routeNumber?: string
  routeName?: string
  color?: string
  startStation?: Station
  endStation?: Station
  station?: Station
  duration: number
  stationCount?: number
  distance?: number
}

interface CalculatedRoute {
  routeType: string
  totalDuration: number
  segments: RouteSegment[]
  routeRank: number
  routeCategory: string
}

interface Props {
  routes: CalculatedRoute[]
}

const props = defineProps<Props>()
const emit = defineEmits(['close', 'selectRoute'])

const selectedRouteIndex = ref<number | null>(null)

const fastestRoute = computed(() => {
  return props.routes.length > 0 ? props.routes[0] : null
})

const directRoutesCount = computed(() => {
  return props.routes.filter(r => r.routeType === 'direct').length
})

const timeSavings = computed(() => {
  if (props.routes.length < 2) return 0
  const slowest = props.routes[props.routes.length - 1]
  const fastest = props.routes[0]
  return (slowest?.totalDuration ?? 0) - (fastest?.totalDuration ?? 0)
})

const getTotalStations = (route: CalculatedRoute) => {
  return route.segments
    .filter(s => s.type === 'bus')
    .reduce((sum, s) => sum + (s.stationCount || 0), 0)
}

const selectRoute = (index: number) => {
  selectedRouteIndex.value = index
}

const selectRouteForNavigation = (index: number) => {
  selectedRouteIndex.value = index
  emit('selectRoute', props.routes[index])
}
</script>

<style scoped>
.alternatives-panel {
  position: fixed;
  top: 84px; /* sub bara de căutare */
  right: 76px;
  z-index: 1200;
  width: 400px;
  max-height: calc(100dvh - 100px);
  display: flex;
  flex-direction: column;
  background: var(--bg-primary);
  color: var(--text-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-xl);
  font-family: var(--font-sans);
  font-variant-numeric: tabular-nums;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 14px 8px 12px 20px;
  border-bottom: 1px solid var(--border-subtle);
}

.header-content {
  flex: 1;
  min-width: 0;
}

.panel-header h3 {
  margin: 0;
  font-size: 20px;
  font-weight: var(--fw-bold);
}

.subtitle {
  margin: 2px 0 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.close-btn {
  width: 44px;
  height: 44px;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.close-btn:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.routes-container {
  overflow-y: auto;
  overscroll-behavior: contain;
  padding: 16px;
}

.routes-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.route-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  cursor: pointer;
  transition: border-color 0.15s ease;
}

.route-card:hover {
  border-color: var(--border-secondary);
}

.route-card.selected {
  border: 2px solid var(--text-primary);
  padding: 15px;
}

.route-card:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

.fastest-badge,
.category-badge {
  align-self: flex-start;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 9px;
  border-radius: var(--radius-full);
  font-size: 12px;
  font-weight: var(--fw-bold);
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.fastest-badge {
  background: var(--color-success-soft);
  color: var(--color-success);
}

.category-badge.transfer {
  background: var(--live-soft);
  color: var(--live-ink);
}

.route-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.duration-info {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.time-circle {
  display: none;
}

.duration {
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: var(--fw-bold);
  letter-spacing: -0.02em;
}

.duration-unit {
  margin-left: 4px;
  font-size: 15px;
  font-weight: var(--fw-bold);
}

.route-stats {
  display: flex;
  gap: 6px;
}

.stat-pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  font-weight: var(--fw-semibold);
  color: var(--text-secondary);
}

.route-reason {
  font-size: 13px;
  font-weight: var(--fw-bold);
  color: var(--color-success);
}

.route-segments {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding-top: 12px;
  border-top: 1px solid var(--border-subtle);
}

.bus-segment,
.transfer-segment,
.walk-segment {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.route-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 34px;
  height: 26px;
  padding: 0 7px;
  border-radius: var(--radius-sm);
  color: #fff;
  font-family: var(--font-display);
  font-weight: var(--fw-heavy);
  font-size: 14px;
  flex-shrink: 0;
}

.segment-details,
.transfer-details,
.walk-details {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.segment-line {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

.arrow,
.separator {
  color: var(--text-tertiary);
}

.segment-meta,
.transfer-duration,
.walk-duration {
  font-size: 13px;
  color: var(--text-secondary);
}

.route-name {
  overflow: hidden;
  text-overflow: ellipsis;
}

.transfer-icon,
.walk-icon {
  width: 34px;
  height: 26px;
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.transfer-icon {
  background: var(--live-soft);
  color: var(--live-ink);
}

.transfer-label,
.walk-label {
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

.route-actions {
  display: flex;
}

.select-route-btn {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 44px;
  border: none;
  border-radius: var(--radius-md);
  background: var(--accent-primary);
  color: var(--text-on-accent);
  font: inherit;
  font-size: 15px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.select-route-btn:hover {
  background: var(--accent-primary-hover);
}

.select-route-btn.selected-btn {
  background: var(--bg-inverse);
  color: var(--text-inverse);
}

.comparison-footer {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  padding: 12px 16px 16px;
  border-top: 1px solid var(--border-subtle);
}

.comparison-stat {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.stat-label {
  font-size: 12px;
  color: var(--text-secondary);
}

.stat-value {
  font-size: 15px;
  font-weight: var(--fw-bold);
}

.slide-up-enter-active,
.slide-up-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.slide-up-enter-from,
.slide-up-leave-to {
  opacity: 0;
  transform: translateY(12px);
}

@media (max-width: 767px) {
  .alternatives-panel {
    top: auto;
    left: 0;
    right: 0;
    bottom: var(--nav-height);
    width: auto;
    max-height: 70dvh;
    border-radius: var(--radius-xl) var(--radius-xl) 0 0;
  }
}
</style>
