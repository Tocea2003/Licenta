<template>
  <div v-if="visible" class="transfer-route-panel">
    <div class="panel-header">
      <h3 class="panel-title">{{ busLine ? '🚌' : '🚶' }} {{ t('completeRoute') }}</h3>
      <button @click="$emit('close')" class="close-btn">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path d="M15 5L5 15M5 5l10 10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
      </button>
    </div>

    <div class="panel-content">
      <!-- Segment 1: Mers pe jos către prima stație -->
      <div v-if="showFirstWalk" class="route-segment walk">
        <div class="segment-icon">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
            <path d="M13.5 5.5C13.5 6.88071 12.3807 8 11 8C9.61929 8 8.5 6.88071 8.5 5.5C8.5 4.11929 9.61929 3 11 3C12.3807 3 13.5 4.11929 13.5 5.5Z" fill="currentColor"/>
            <path d="M11 9L8 13L9 14L11 12.5L11 21H13V14L16 16L17 15L13 11L11 9Z" fill="currentColor"/>
          </svg>
        </div>
        <div class="segment-details">
          <div class="segment-header">
            <span class="segment-type">{{ t('walkOnFoot') }}</span>
            <span class="segment-duration">{{ formatDuration(firstWalkTime) }}</span>
          </div>
          <div class="segment-distance">{{ formatDistance(firstWalkDistance) }}</div>
          <div class="segment-from">{{ t('fromLabel') }} <strong>{{ startLocation }}</strong></div>
          <div class="segment-to">{{ t('toLabel') }} <strong>{{ boardingStation || endLocation }}</strong></div>
        </div>
      </div>

      <!-- Segment 2: Linia de autobuz -->
      <div v-if="busLine" class="route-segment bus" :style="{ '--route-color': busColor }">
        <div class="segment-icon">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
            <rect x="4" y="4" width="16" height="16" rx="2" stroke="currentColor" stroke-width="2"/>
            <path d="M4 10h16M8 18v2M16 18v2M8 4v2M16 4v2" stroke="currentColor" stroke-width="2"/>
            <circle cx="8" cy="15" r="1" fill="currentColor"/>
            <circle cx="16" cy="15" r="1" fill="currentColor"/>
          </svg>
        </div>
        <div class="segment-details">
          <div class="segment-header">
            <span class="segment-type">
              <span class="route-badge" :style="{ backgroundColor: busColor }">
                {{ busLine }}
              </span>
            </span>
            <span class="segment-duration">{{ formatDuration(busTime) }}</span>
          </div>
          <div class="segment-stations">{{ displayStationsCount }} {{ t('stationsLabel') }}</div>
          <div class="segment-from">
            {{ t('boarding') }} <strong>{{ boardingStation }}</strong>
            <span v-if="formatTime(busStartTime)" class="segment-time">{{ formatTime(busStartTime) }}</span>
          </div>
          <div class="segment-to">
            {{ t('alighting') }} <strong>{{ alightingStation }}</strong>
            <span v-if="formatTime(busEndTime)" class="segment-time">{{ formatTime(busEndTime) }}</span>
          </div>

          <div v-if="busStationsList.length > 2" class="stations-toggle">
            <button @click="showAllStations = !showAllStations" class="toggle-stations-btn">
              {{ showAllStations ? `▲ ${t('hideStations')}` : `▼ ${t('showAllStations')}` }}
            </button>
            <div v-if="showAllStations" class="bus-stations-list">
              <div v-for="(station, idx) in busStationsList" :key="`bus-station-${idx}`" class="bus-station-item">
                <span class="station-dot" :style="{ background: busColor }"></span>
                <span>{{ station }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Segment 3: Mers pe jos către destinație -->
      <div v-if="showSecondWalk" class="route-segment walk">
        <div class="segment-icon">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
            <path d="M13.5 5.5C13.5 6.88071 12.3807 8 11 8C9.61929 8 8.5 6.88071 8.5 5.5C8.5 4.11929 9.61929 3 11 3C12.3807 3 13.5 4.11929 13.5 5.5Z" fill="currentColor"/>
            <path d="M11 9L8 13L9 14L11 12.5L11 21H13V14L16 16L17 15L13 11L11 9Z" fill="currentColor"/>
          </svg>
        </div>
        <div class="segment-details">
          <div class="segment-header">
            <span class="segment-type">{{ t('walkOnFoot') }}</span>
            <span class="segment-duration">{{ formatDuration(secondWalkTime) }}</span>
          </div>
          <div class="segment-distance">{{ formatDistance(secondWalkDistance) }}</div>
          <div class="segment-from">{{ t('fromLabel') }} <strong>{{ alightingStation }}</strong></div>
          <div class="segment-to">{{ t('toLabel') }} <strong>{{ endLocation }}</strong></div>
        </div>
      </div>

      <!-- Ore plecare/sosire -->
      <div v-if="formatTime(departureTime) && formatTime(arrivalTime)" class="route-times">
        <div class="time-item departure">
          <span class="time-label">{{ t('departureLabel') }}</span>
          <span class="time-value">{{ formatTime(departureTime) }}</span>
        </div>
        <div class="time-arrow">→</div>
        <div class="time-item arrival">
          <span class="time-label">{{ t('arrivalLabel') }}</span>
          <span class="time-value">{{ formatTime(arrivalTime) }}</span>
        </div>
      </div>

      <!-- Rezumat total -->
      <div class="route-summary">
        <div class="summary-item">
          <span class="summary-label">{{ t('totalDuration') }}</span>
          <span class="summary-value">{{ formatDuration(totalTime) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{ t('walkingDistance') }}</span>
          <span class="summary-value">{{ formatDistance(totalWalkDistance) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">{{ t('transfers') }}</span>
          <span class="summary-value">0</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const props = defineProps<{
  visible: boolean
  startLocation: string
  endLocation: string
  boardingStation: string
  alightingStation: string
  busLine: string
  busColor: string
  busStationsList: string[]
  firstWalkDistance: number
  firstWalkTime: number
  secondWalkDistance: number
  secondWalkTime: number
  busTime: number
  busStationsCount?: number
  busStartTime?: string
  busEndTime?: string
  departureTime?: string
  arrivalTime?: string
}>()

defineEmits<{
  close: []
}>()

const showAllStations = ref(false)

const showFirstWalk = computed(() => props.firstWalkDistance > 0 || props.firstWalkTime > 0)
const showSecondWalk = computed(() => props.secondWalkDistance > 0 || props.secondWalkTime > 0)
const totalTime = computed(() => props.firstWalkTime + props.busTime + props.secondWalkTime)
const totalWalkDistance = computed(() => props.firstWalkDistance + props.secondWalkDistance)
const displayStationsCount = computed(() => props.busStationsCount || props.busStationsList.length)

const formatTime = (isoString?: string): string => {
  if (!isoString) return ''
  const date = new Date(isoString)
  if (isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })
}

const formatDistance = (meters: number): string => {
  if (meters < 1000) return `${Math.round(meters)} m`
  return `${(meters / 1000).toFixed(1)} km`
}

const formatDuration = (minutes: number): string => {
  if (minutes < 60) return `${Math.round(minutes)} min`
  const hours = Math.floor(minutes / 60)
  const mins = Math.round(minutes % 60)
  return `${hours}h ${mins}min`
}
</script>

<style scoped>
.transfer-route-panel {
  position: fixed;
  bottom: 20px;
  left: 370px;
  width: 340px;
  max-width: calc(100vw - 390px);
  max-height: calc(100vh - 100px);
  background: var(--bg-primary);
  backdrop-filter: blur(16px);
  border-radius: 18px;
  box-shadow: var(--shadow-xl);
  z-index: 500;
  overflow: hidden;
  overflow-y: auto;
  animation: slideUp 0.3s ease-out;
  border: 1px solid var(--border-color);
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: var(--gradient-primary);
  color: white;
  border-bottom: 1px solid rgba(255, 255, 255, 0.12);
}

.panel-title {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
}

.close-btn {
  background: rgba(255, 255, 255, 0.16);
  border: none;
  border-radius: 8px;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: white;
  transition: all 0.2s;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.28);
  transform: scale(1.05);
}

.panel-content {
  padding: 10px 14px;
  max-height: calc(100vh - 180px);
  overflow-y: auto;
}

.route-segment {
  display: flex;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 10px;
  margin-bottom: 6px;
  background: white;
  border: 1.5px solid #e5e7eb;
  transition: all 0.2s;
}

.route-segment:hover {
  border-color: #d1d5db;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
}

@media (prefers-color-scheme: dark) {
  .route-segment {
    background: rgba(40, 40, 50, 0.6);
    border: 1.5px solid rgba(255, 255, 255, 0.15);
  }
  .route-segment:hover {
    border-color: rgba(255, 255, 255, 0.25);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  }
}

.route-segment.walk {
  border-color: #3b82f6;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.05) 0%, rgba(59, 130, 246, 0.02) 100%);
}

.route-segment.walk .segment-icon {
  color: #3b82f6;
}

.route-segment.bus {
  border-color: var(--route-color);
  background: linear-gradient(135deg,
    color-mix(in srgb, var(--route-color) 8%, transparent) 0%,
    color-mix(in srgb, var(--route-color) 2%, transparent) 100%);
}

.route-segment.bus .segment-icon {
  color: var(--route-color);
}

@media (prefers-color-scheme: dark) {
  .route-segment.walk {
    border-color: #3b82f6;
    background: linear-gradient(135deg, rgba(59, 130, 246, 0.15) 0%, rgba(59, 130, 246, 0.05) 100%);
  }
  .route-segment.bus {
    border-color: var(--route-color);
    background: linear-gradient(135deg,
      color-mix(in srgb, var(--route-color) 20%, transparent) 0%,
      color-mix(in srgb, var(--route-color) 5%, transparent) 100%);
  }
}

.segment-icon {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.05);
}

@media (prefers-color-scheme: dark) {
  .segment-icon {
    background: rgba(255, 255, 255, 0.1);
  }
}

.segment-icon svg {
  width: 20px;
  height: 20px;
}

.segment-details {
  flex: 1;
}

.segment-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.segment-type {
  font-size: 13px;
  font-weight: 700;
  color: #1f2937;
  display: flex;
  align-items: center;
  gap: 6px;
}

@media (prefers-color-scheme: dark) {
  .segment-type { color: #e5e7eb; }
}

.route-badge {
  display: inline-block;
  padding: 3px 8px;
  border-radius: 5px;
  color: white;
  font-size: 12px;
  font-weight: 800;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.segment-duration {
  font-size: 12px;
  font-weight: 700;
  color: #059669;
  background: rgba(5, 150, 105, 0.1);
  padding: 3px 8px;
  border-radius: 5px;
}

.segment-distance,
.segment-stations {
  font-size: 11px;
  color: #6b7280;
  margin-bottom: 3px;
}

.segment-from,
.segment-to {
  font-size: 12px;
  color: #4b5563;
  margin-bottom: 2px;
}

.segment-from strong,
.segment-to strong {
  color: #1f2937;
  font-weight: 600;
}

@media (prefers-color-scheme: dark) {
  .segment-distance, .segment-stations { color: #9ca3af; }
  .segment-from, .segment-to { color: #d1d5db; }
  .segment-from strong, .segment-to strong { color: #f3f4f6; }
}

.segment-time {
  display: inline-block;
  font-size: 11px;
  font-weight: 700;
  color: #3b82f6;
  background: rgba(59, 130, 246, 0.1);
  padding: 1px 6px;
  border-radius: 4px;
  margin-left: 6px;
}

@media (prefers-color-scheme: dark) {
  .segment-time {
    color: #60a5fa;
    background: rgba(96, 165, 250, 0.15);
  }
}

.stations-toggle {
  margin-top: 6px;
}

.toggle-stations-btn {
  width: 100%;
  padding: 6px;
  background: transparent;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  color: #3b82f6;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.toggle-stations-btn:hover {
  background: rgba(59, 130, 246, 0.05);
  border-color: #3b82f6;
}

@media (prefers-color-scheme: dark) {
  .toggle-stations-btn {
    border-color: rgba(255, 255, 255, 0.15);
    color: #60a5fa;
  }
  .toggle-stations-btn:hover {
    background: rgba(96, 165, 250, 0.1);
  }
}

.bus-stations-list {
  margin-top: 6px;
  padding: 8px;
  background: rgba(0, 0, 0, 0.02);
  border-radius: 6px;
}

@media (prefers-color-scheme: dark) {
  .bus-stations-list {
    background: rgba(255, 255, 255, 0.05);
  }
}

.bus-station-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 0;
  font-size: 12px;
  color: #4b5563;
}

@media (prefers-color-scheme: dark) {
  .bus-station-item { color: #d1d5db; }
}

.station-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  flex-shrink: 0;
}

.route-times {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin-top: 12px;
  padding: 12px 16px;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.08) 0%, rgba(16, 185, 129, 0.08) 100%);
  border-radius: 12px;
  border: 1px solid rgba(59, 130, 246, 0.15);
}

@media (prefers-color-scheme: dark) {
  .route-times {
    background: linear-gradient(135deg, rgba(59, 130, 246, 0.15) 0%, rgba(16, 185, 129, 0.15) 100%);
    border-color: rgba(59, 130, 246, 0.25);
  }
}

.time-item { text-align: center; }

.time-label {
  display: block;
  font-size: 10px;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 2px;
}

.time-value {
  display: block;
  font-size: 18px;
  font-weight: 800;
  color: #1f2937;
}

.time-arrow {
  font-size: 18px;
  color: #9ca3af;
  font-weight: 700;
}

@media (prefers-color-scheme: dark) {
  .time-label { color: #9ca3af; }
  .time-value { color: #f3f4f6; }
  .time-arrow { color: #6b7280; }
}

.route-summary {
  margin-top: 20px;
  padding: 16px;
  background: color-mix(in srgb, var(--bg-secondary) 88%, transparent);
  border-radius: 12px;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

@media (prefers-color-scheme: dark) {
  .route-summary {
    background: color-mix(in srgb, var(--bg-secondary) 82%, transparent);
    border: 1px solid var(--border-color);
  }
}

.summary-item { text-align: center; }

.summary-label {
  display: block;
  font-size: 11px;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 6px;
  font-weight: 600;
}

.summary-value {
  display: block;
  font-size: 16px;
  font-weight: 800;
  color: #1f2937;
}

@media (prefers-color-scheme: dark) {
  .summary-label { color: #9ca3af; }
  .summary-value { color: #f3f4f6; }
}

.panel-content::-webkit-scrollbar { width: 6px; }
.panel-content::-webkit-scrollbar-track { background: transparent; }
.panel-content::-webkit-scrollbar-thumb { background: rgba(209, 213, 219, 0.8); border-radius: 10px; }
.panel-content::-webkit-scrollbar-thumb:hover { background: rgba(156, 163, 175, 0.9); }

@media (prefers-color-scheme: dark) {
  .panel-content::-webkit-scrollbar-thumb { background: rgba(255, 255, 255, 0.2); }
  .panel-content::-webkit-scrollbar-thumb:hover { background: rgba(255, 255, 255, 0.3); }
}

@media (max-width: 1100px) {
  .transfer-route-panel {
    left: 290px;
    width: 300px;
    max-width: calc(100vw - 310px);
  }
}

@media (max-width: 768px) {
  .transfer-route-panel {
    position: fixed;
    bottom: 70px;
    left: 10px;
    right: 10px;
    width: auto;
    max-width: none;
    max-height: 55vh;
    border-radius: 16px;
  }

  .panel-content {
    max-height: calc(55vh - 60px);
  }

  .route-summary {
    grid-template-columns: repeat(3, 1fr);
    gap: 8px;
    padding: 10px;
  }

  .summary-label {
    font-size: 9px;
  }

  .summary-value {
    font-size: 14px;
  }

  .route-times {
    padding: 8px 12px;
  }

  .time-value {
    font-size: 16px;
  }
}
</style>
