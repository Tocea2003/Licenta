<template>
  <Transition name="slide-up">
    <div v-if="routes.length > 0" class="alternatives-panel">
      <div class="panel-header">
        <div class="header-content">
          <div class="header-icon">🗺️</div>
          <div>
            <h3>Rute Alternative</h3>
            <p class="subtitle">{{ routes.length }} {{ routes.length === 1 ? 'opțiune disponibilă' : 'opțiuni disponibile' }}</p>
          </div>
        </div>
        <button @click="$emit('close')" class="close-btn" aria-label="Închide">
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
            class="route-card"
            :class="{ 
              'selected': selectedRouteIndex === index,
              'fastest': route.routeRank === 1
            }"
            @click="selectRoute(index)"
          >
            <!-- Badge pentru cea mai rapidă -->
            <div v-if="route.routeRank === 1" class="fastest-badge">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                <path d="M13 2L3 14h8l-1 8 10-12h-8l1-8z"/>
              </svg>
              Cea mai rapidă
            </div>
            <div v-else-if="route.routeType === 'direct'" class="category-badge direct">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M5 12h14M12 5l7 7-7 7"/>
              </svg>
              Direct
            </div>
            <div v-else class="category-badge transfer">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M7 16V4M7 4L3 8M7 4l4 4M17 8v12M17 20l4-4M17 20l-4-4"/>
              </svg>
              Cu transfer
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
                  1 transfer
                </div>
              </div>
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
                    <span class="transfer-label">Transfer la {{ segment.station?.name }}</span>
                    <span class="transfer-duration">⏱️ Așteptare ~{{ segment.duration }} min</span>
                  </div>
                </div>

                <div v-else-if="segment.type === 'walk'" class="walk-segment">
                  <div class="walk-icon">🚶</div>
                  <div class="walk-details">
                    <span class="walk-label">Mers pe jos</span>
                    <span class="walk-duration">{{ segment.duration }} min</span>
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
                {{ selectedRouteIndex === index ? 'Rută selectată' : 'Selectează ruta' }}
              </button>
            </div>
          </div>
        </TransitionGroup>
      </div>

      <!-- Comparison Footer -->
      <div class="comparison-footer">
        <div class="comparison-stat">
          <span class="stat-label">Cea mai rapidă</span>
          <span class="stat-value">{{ fastestRoute?.totalDuration }} min</span>
        </div>
        <div class="comparison-stat">
          <span class="stat-label">Fără transfer</span>
          <span class="stat-value">{{ directRoutesCount }} {{ directRoutesCount === 1 ? 'opțiune' : 'opțiuni' }}</span>
        </div>
        <div class="comparison-stat">
          <span class="stat-label">Salvează timp</span>
          <span class="stat-value">{{ timeSavings }} min</span>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

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

const getRouteTypeLabel = (type: string) => {
  const labels: Record<string, string> = {
    'direct': 'Direct',
    'transfer': 'Cu transfer',
    'walk': 'Cu mers pe jos'
  }
  return labels[type] || type
}

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
  top: 80px;
  right: 20px;
  width: 380px;
  background: linear-gradient(to bottom, var(--bg-primary), var(--bg-secondary));
  border-radius: 16px;
  box-shadow: 
    0 4px 16px rgba(0, 0, 0, 0.1),
    0 8px 40px rgba(0, 0, 0, 0.15);
  max-height: calc(100vh - 120px);
  display: flex;
  flex-direction: column;
  z-index: 1000;
  backdrop-filter: blur(10px);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 20px;
  border-bottom: 2px solid var(--border-color);
  background: var(--bg-primary);
}

.header-content {
  display: flex;
  align-items: center;
  gap: 10px;
}

.header-icon {
  font-size: 24px;
  filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.1));
}

.header-content h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 800;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.subtitle {
  margin: 2px 0 0 0;
  font-size: 12px;
  color: var(--text-secondary);
  font-weight: 500;
}

.close-btn {
  background: var(--bg-secondary);
  border: 2px solid var(--border-color);
  width: 34px;
  height: 34px;
  border-radius: 50%;
  cursor: pointer;
  color: var(--text-secondary);
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  display: flex;
  align-items: center;
  justify-content: center;
}

.close-btn:hover {
  background: #fee;
  border-color: #f66;
  color: #f44;
  transform: scale(1.1);
}

.close-btn:active {
  transform: scale(0.95);
}

.routes-container {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  scrollbar-width: thin;
  scrollbar-color: var(--border-color) transparent;
}

.routes-container::-webkit-scrollbar {
  width: 6px;
}

.routes-container::-webkit-scrollbar-thumb {
  background: var(--border-color);
  border-radius: 3px;
}

.routes-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.route-card {
  background: var(--bg-primary);
  border: 2px solid var(--border-color);
  border-radius: 16px;
  padding: 14px;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: visible;
}

.route-card:hover {
  border-color: #667eea;
  transform: translateY(-4px);
  box-shadow: 
    0 8px 24px rgba(102, 126, 234, 0.2),
    0 16px 48px rgba(102, 126, 234, 0.15);
}

.route-card.selected {
  border-color: #667eea;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.05), rgba(118, 75, 162, 0.05));
  box-shadow: 0 4px 16px rgba(102, 126, 234, 0.15);
}

.route-card.fastest {
  border-color: #48bb78;
  background: linear-gradient(135deg, rgba(72, 187, 120, 0.03), rgba(56, 161, 105, 0.05));
}

.fastest-badge {
  position: absolute;
  top: -8px;
  right: 12px;
  background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
  color: white;
  padding: 5px 12px;
  border-radius: 20px;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.6px;
  box-shadow: 
    0 3px 8px rgba(72, 187, 120, 0.4),
    0 1px 3px rgba(0, 0, 0, 0.1);
  display: flex;
  align-items: center;
  gap: 4px;
}

.category-badge {
  position: absolute;
  top: -8px;
  right: 12px;
  padding: 5px 12px;
  border-radius: 20px;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  display: flex;
  align-items: center;
  gap: 4px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
}

.category-badge.direct {
  background: linear-gradient(135deg, #4299e1 0%, #3182ce 100%);
  color: white;
}

.category-badge.transfer {
  background: linear-gradient(135deg, #ed8936 0%, #dd6b20 100%);
  color: white;
}

.route-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding-top: 4px;
}

.duration-info {
  display: flex;
  align-items: center;
  gap: 10px;
}

.time-circle {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  box-shadow: 0 3px 8px rgba(102, 126, 234, 0.3);
}

.time-circle svg {
  width: 18px;
  height: 18px;
}

.duration {
  font-size: 28px;
  font-weight: 800;
  color: var(--text-primary);
  line-height: 1;
  letter-spacing: -0.5px;
}

.duration-unit {
  font-size: 14px;
  color: var(--text-secondary);
  font-weight: 600;
  margin-left: 3px;
}

.route-stats {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.stat-pill {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 10px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 16px;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-primary);
}

.stat-pill svg {
  width: 14px;
  height: 14px;
}

.stat-pill.transfer {
  background: rgba(237, 137, 54, 0.1);
  border-color: rgba(237, 137, 54, 0.3);
  color: #dd6b20;
}

.route-segments {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 12px;
}

.bus-segment {
  display: flex;
  gap: 10px;
  width: 100%;
}

.route-badge {
  min-width: 44px;
  height: 44px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-weight: 800;
  font-size: 15px;
  flex-shrink: 0;
  box-shadow: 0 3px 8px rgba(0, 0, 0, 0.2);
}

.segment-details {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  justify-content: center;
}

.segment-line {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.station-name {
  color: var(--text-primary);
  font-weight: 600;
}

.arrow {
  color: var(--text-secondary);
  font-size: 12px;
  opacity: 0.6;
}

.segment-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: var(--text-secondary);
}

.route-name {
  font-weight: 500;
}

.separator {
  color: var(--border-color);
  font-weight: 600;
}

.segment-duration {
  font-weight: 600;
}

.transfer-segment,
.walk-segment {
  display: flex;
  gap: 10px;
  align-items: center;
  padding: 8px 12px;
  background: linear-gradient(135deg, var(--bg-secondary), var(--bg-tertiary));
  border: 1px solid var(--border-color);
  border-radius: 12px;
  width: 100%;
}

.transfer-icon,
.walk-icon {
  width: 32px;
  height: 32px;
  background: rgba(237, 137, 54, 0.15);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #dd6b20;
  flex-shrink: 0;
}

.transfer-icon svg,
.walk-icon svg {
  width: 16px;
  height: 16px;
}

.transfer-details,
.walk-details {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.transfer-label,
.walk-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.transfer-duration,
.walk-duration {
  font-size: 11px;
  color: var(--text-secondary);
  font-weight: 500;
}

.route-actions {
  margin-top: 10px;
  padding-top: 10px;
  border-top: 1px solid var(--border-color);
}

.select-route-btn {
  width: 100%;
  padding: 10px 18px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 3px 8px rgba(102, 126, 234, 0.3);
}

.select-route-btn svg {
  width: 16px;
  height: 16px;
}

.select-route-btn:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 24px rgba(102, 126, 234, 0.4);
}

.select-route-btn:active {
  transform: translateY(-1px);
}

.select-route-btn.selected-btn {
  background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
  box-shadow: 0 4px 12px rgba(72, 187, 120, 0.3);
}

.select-route-btn.selected-btn:hover {
  box-shadow: 0 8px 24px rgba(72, 187, 120, 0.4);
}

.comparison-footer {
  display: flex;
  justify-content: space-around;
  padding: 12px 20px;
  border-top: 2px solid var(--border-color);
  background: var(--bg-primary);
  gap: 10px;
}

.comparison-stat {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  flex: 1;
}

.stat-label {
  font-size: 10px;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.6px;
  font-weight: 700;
}

.stat-value {
  font-size: 18px;
  font-weight: 800;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

/* Animations */
.slide-up-enter-active {
  animation: slideUp 0.4s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-up-leave-active {
  animation: slideDown 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes slideUp {
  from {
    transform: translateY(100%);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

@keyframes slideDown {
  from {
    transform: translateY(0);
    opacity: 1;
  }
  to {
    transform: translateY(100%);
    opacity: 0;
  }
}

.route-list-move,
.route-list-enter-active,
.route-list-leave-active {
  transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
}

.route-list-enter-from {
  opacity: 0;
  transform: translateX(-40px);
}

.route-list-leave-to {
  opacity: 0;
  transform: translateX(40px);
}

.route-list-leave-active {
  position: absolute;
}

/* Responsive */
@media (max-width: 768px) {
  .alternatives-panel {
    top: auto;
    bottom: 0;
    right: 0;
    left: 0;
    width: 100%;
    max-height: 85vh;
    border-radius: 24px 24px 0 0;
  }

  .route-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .route-stats {
    width: 100%;
    justify-content: flex-start;
  }

  .fastest-badge,
  .category-badge {
    top: 12px;
    right: 12px;
  }

  .duration {
    font-size: 32px;
  }

  .time-circle {
    width: 44px;
    height: 44px;
  }
}
</style>
