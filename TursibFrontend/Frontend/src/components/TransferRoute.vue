<template>
  <div v-if="visible" class="transfer-route-panel">
    <div class="panel-header">
      <h3 class="panel-title">🚌 Traseu cu Transfer</h3>
      <button @click="$emit('close')" class="close-btn">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path d="M15 5L5 15M5 5l10 10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
      </button>
    </div>

    <div class="panel-content">
      <!-- Segment 1: Mers pe jos către prima stație -->
      <div class="route-segment walk">
        <div class="segment-icon">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
            <path d="M13.5 5.5C13.5 6.88071 12.3807 8 11 8C9.61929 8 8.5 6.88071 8.5 5.5C8.5 4.11929 9.61929 3 11 3C12.3807 3 13.5 4.11929 13.5 5.5Z" fill="currentColor"/>
            <path d="M11 9L8 13L9 14L11 12.5L11 21H13V14L16 16L17 15L13 11L11 9Z" fill="currentColor"/>
          </svg>
        </div>
        <div class="segment-details">
          <div class="segment-header">
            <span class="segment-type">Mers pe jos</span>
            <span class="segment-duration">{{ formatDuration(firstWalkTime) }}</span>
          </div>
          <div class="segment-distance">{{ formatDistance(firstWalkDistance) }}</div>
          <div class="segment-from">De la <strong>{{ startName }}</strong></div>
          <div class="segment-to">Către <strong>{{ boardingStation?.name }}</strong></div>
        </div>
      </div>

      <!-- Segment 2: Prima linie de autobuz -->
      <div class="route-segment bus" :style="{ '--route-color': route1Color }">
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
              <span class="route-badge" :style="{ backgroundColor: route1Color }">
                {{ route1Number }}
              </span>
            </span>
            <span class="segment-duration">{{ formatDuration(busTime1) }}</span>
          </div>
          <div class="segment-stations">{{ route1StationsCount }} stații</div>
          <div class="segment-from">
            Îmbarcă la <strong>{{ boardingStation?.name }}</strong>
            <span v-if="formatTime(bus1StartTime)" class="segment-time">{{ formatTime(bus1StartTime) }}</span>
          </div>
          <div class="segment-to">
            Coboară la <strong>{{ transferStation?.name }}</strong>
            <span v-if="formatTime(bus1EndTime)" class="segment-time">{{ formatTime(bus1EndTime) }}</span>
          </div>
        </div>
      </div>

      <!-- Transfer Station Indicator -->
      <div class="transfer-indicator">
        <div class="transfer-icon">
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <path d="M10 2v16M4 8l6-6 6 6M4 12l6 6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
          </svg>
        </div>
        <span class="transfer-text">Schimbă autobuzul</span>
      </div>

      <!-- Segment 3: A doua linie de autobuz -->
      <div class="route-segment bus" :style="{ '--route-color': route2Color }">
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
              <span class="route-badge" :style="{ backgroundColor: route2Color }">
                {{ route2Number }}
              </span>
            </span>
            <span class="segment-duration">{{ formatDuration(busTime2) }}</span>
          </div>
          <div class="segment-stations">{{ route2StationsCount }} stații</div>
          <div class="segment-from">
            Îmbarcă la <strong>{{ transferStation?.name }}</strong>
            <span v-if="formatTime(bus2StartTime)" class="segment-time">{{ formatTime(bus2StartTime) }}</span>
          </div>
          <div class="segment-to">
            Coboară la <strong>{{ secondBusDropoffStation?.name }}</strong>
            <span v-if="formatTime(bus2EndTime)" class="segment-time">{{ formatTime(bus2EndTime) }}</span>
          </div>
        </div>
      </div>

      <!-- Transfer Station Indicator 2 (pentru 3 autobuze) -->
      <div v-if="hasThirdBus" class="transfer-indicator">
        <div class="transfer-icon">
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
            <path d="M10 2v16M4 8l6-6 6 6M4 12l6 6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
          </svg>
        </div>
        <span class="transfer-text">Schimbă autobuzul</span>
      </div>

      <!-- Segment 4: A treia linie de autobuz -->
      <div v-if="hasThirdBus" class="route-segment bus" :style="{ '--route-color': route3Color }">
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
              <span class="route-badge" :style="{ backgroundColor: route3Color }">
                {{ route3Number }}
              </span>
            </span>
            <span class="segment-duration">{{ formatDuration(busTime3 || 0) }}</span>
          </div>
          <div class="segment-stations">{{ route3StationsCount }} stații</div>
          <div class="segment-from">
            Îmbarcă la <strong>{{ transferStation2?.name }}</strong>
            <span v-if="formatTime(bus3StartTime)" class="segment-time">{{ formatTime(bus3StartTime) }}</span>
          </div>
          <div class="segment-to">
            Coboară la <strong>{{ alightingStation?.name }}</strong>
            <span v-if="formatTime(bus3EndTime)" class="segment-time">{{ formatTime(bus3EndTime) }}</span>
          </div>
        </div>
      </div>

      <!-- Ultimul segment: Mers pe jos către destinație -->
      <div class="route-segment walk">
        <div class="segment-icon">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
            <path d="M13.5 5.5C13.5 6.88071 12.3807 8 11 8C9.61929 8 8.5 6.88071 8.5 5.5C8.5 4.11929 9.61929 3 11 3C12.3807 3 13.5 4.11929 13.5 5.5Z" fill="currentColor"/>
            <path d="M11 9L8 13L9 14L11 12.5L11 21H13V14L16 16L17 15L13 11L11 9Z" fill="currentColor"/>
          </svg>
        </div>
        <div class="segment-details">
          <div class="segment-header">
            <span class="segment-type">Mers pe jos</span>
            <span class="segment-duration">{{ formatDuration(secondWalkTime) }}</span>
          </div>
          <div class="segment-distance">{{ formatDistance(secondWalkDistance) }}</div>
          <div class="segment-from">De la <strong>{{ alightingStation?.name }}</strong></div>
          <div class="segment-to">Către <strong>{{ endName }}</strong></div>
        </div>
      </div>

      <!-- Ore plecare/sosire -->
      <div v-if="formatTime(departureTime) && formatTime(arrivalTime)" class="route-times">
        <div class="time-item departure">
          <span class="time-label">Plecare</span>
          <span class="time-value">{{ formatTime(departureTime) }}</span>
        </div>
        <div class="time-arrow">→</div>
        <div class="time-item arrival">
          <span class="time-label">Sosire</span>
          <span class="time-value">{{ formatTime(arrivalTime) }}</span>
        </div>
      </div>

      <!-- Rezumat total -->
      <div class="route-summary">
        <div class="summary-item">
          <span class="summary-label">Durată totală</span>
          <span class="summary-value">{{ formatDuration(totalTime) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">Distanță pe jos</span>
          <span class="summary-value">{{ formatDistance(totalWalkDistance) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-label">Transferuri</span>
          <span class="summary-value">{{ transferCount }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

const props = defineProps<{
  visible: boolean
  startName: string
  endName: string
  boardingStation: Station | null
  transferStation: Station | null
  transferStation2?: Station | null
  alightingStation: Station | null
  route1Number: string
  route1Color: string
  route1StationsCount: number
  route2Number: string
  route2Color: string
  route2StationsCount: number
  route3Number?: string
  route3Color?: string
  route3StationsCount?: number
  firstWalkDistance: number
  firstWalkTime: number
  busTime1: number
  busTime2: number
  busTime3?: number
  secondWalkDistance: number
  secondWalkTime: number
  bus1StartTime?: string
  bus1EndTime?: string
  bus2StartTime?: string
  bus2EndTime?: string
  bus3StartTime?: string
  bus3EndTime?: string
  departureTime?: string
  arrivalTime?: string
}>()

defineEmits<{
  close: []
}>()

const totalTime = computed(() => 
  props.firstWalkTime + props.busTime1 + props.busTime2 + (props.busTime3 || 0) + props.secondWalkTime
)

const hasThirdBus = computed(() => Boolean(props.route3Number && props.transferStation2))

const secondBusDropoffStation = computed(() => {
  if (hasThirdBus.value && props.transferStation2) {
    return props.transferStation2
  }
  return props.alightingStation
})

const transferCount = computed(() => (hasThirdBus.value ? 2 : 1))

const totalWalkDistance = computed(() => 
  props.firstWalkDistance + props.secondWalkDistance
)

const formatTime = (isoString?: string): string => {
  if (!isoString) return ''
  const date = new Date(isoString)
  if (isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })
}

const formatDistance = (meters: number): string => {
  if (meters < 1000) {
    return `${Math.round(meters)} m`
  }
  return `${(meters / 1000).toFixed(1)} km`
}

const formatDuration = (minutes: number): string => {
  if (minutes < 60) {
    return `${Math.round(minutes)} min`
  }
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

@media (prefers-color-scheme: dark) {
  .transfer-route-panel {
    background: var(--bg-primary);
  }
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
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
  .segment-type {
    color: #e5e7eb;
  }
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
  .segment-distance,
  .segment-stations {
    color: #9ca3af;
  }
  
  .segment-from,
  .segment-to {
    color: #d1d5db;
  }
  
  .segment-from strong,
  .segment-to strong {
    color: #f3f4f6;
  }
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

.time-item {
  text-align: center;
}

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
  .time-label {
    color: #9ca3af;
  }
  .time-value {
    color: #f3f4f6;
  }
  .time-arrow {
    color: #6b7280;
  }
}

.transfer-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  margin: 6px 0;
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
  border-radius: 8px;
  color: white;
}

.transfer-icon {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 8px;
}

.transfer-text {
  font-size: 14px;
  font-weight: 700;
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

.summary-item {
  text-align: center;
}

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
  .summary-label {
    color: #9ca3af;
  }
  
  .summary-value {
    color: #f3f4f6;
  }
}

/* Scrollbar styling */
.panel-content::-webkit-scrollbar {
  width: 6px;
}

.panel-content::-webkit-scrollbar-track {
  background: transparent;
}

.panel-content::-webkit-scrollbar-thumb {
  background: rgba(209, 213, 219, 0.8);
  border-radius: 10px;
}

.panel-content::-webkit-scrollbar-thumb:hover {
  background: rgba(156, 163, 175, 0.9);
}

@media (prefers-color-scheme: dark) {
  .panel-content::-webkit-scrollbar-thumb {
    background: rgba(255, 255, 255, 0.2);
  }
  
  .panel-content::-webkit-scrollbar-thumb:hover {
    background: rgba(255, 255, 255, 0.3);
  }
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
