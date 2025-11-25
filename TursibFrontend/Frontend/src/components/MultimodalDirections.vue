<template>
  <div v-if="visible" class="multimodal-panel">
    <div class="panel-header">
      <div class="header-top">
        <h3>🚌 Traseu complet</h3>
        <button @click="$emit('close')" class="close-btn">✕</button>
      </div>
      
      <div class="summary-info">
        <div class="summary-item">
          <span class="icon">📏</span>
          <div>
            <strong>{{ totalDistance }} km</strong>
            <small>Distanță totală</small>
          </div>
        </div>
        <div class="summary-item">
          <span class="icon">⏱️</span>
          <div>
            <strong>{{ totalTime }} min</strong>
            <small>Timp estimat</small>
          </div>
        </div>
      </div>
    </div>
    
    <div class="panel-body">
      <!-- Pas 1: Mers pe jos la stația de urcare -->
      <div class="route-section">
        <div class="section-header walking">
          <span class="icon">🚶</span>
          <div class="section-title">
            <strong>Mergi pe jos</strong>
            <small>{{ firstWalkDistance }} m · {{ firstWalkTime }} min</small>
          </div>
        </div>
        
        <div class="section-content">
          <div class="location-point start">
            <span class="marker">📍</span>
            <span>{{ startLocation }}</span>
          </div>
          
          <div v-for="(step, index) in firstWalkingSteps" :key="`walk1-${index}`" class="step-detail">
            <span class="step-icon">{{ getStepIcon(step.type) }}</span>
            <div class="step-text">
              <strong>{{ step.instruction }}</strong>
              <small>{{ step.distance }} m</small>
            </div>
          </div>
          
          <div class="location-point station">
            <span class="marker">🚏</span>
            <span>{{ boardingStation }}</span>
          </div>
        </div>
      </div>
      
      <!-- Pas 2: Mergi cu autobuzul -->
      <div class="route-section">
        <div class="section-header bus" :style="{ borderLeftColor: busColor }">
          <span class="icon">🚌</span>
          <div class="section-title">
            <strong>Linia {{ busLine }}</strong>
            <small>{{ busStations }} stații · {{ busTime }} min</small>
          </div>
        </div>
        
        <div class="section-content">
          <div class="bus-instructions">
            <p>💺 Urcă în autobuzul Linia <strong>{{ busLine }}</strong></p>
            <p>📍 Coboară la <strong>{{ alightingStation }}</strong></p>
          </div>
          
          <div v-if="showBusStations" class="bus-stations-list">
            <div v-for="(station, idx) in busStationsList" :key="`bus-station-${idx}`" class="bus-station-item">
              <span class="station-dot"></span>
              <span>{{ station }}</span>
            </div>
          </div>
          
          <button @click="showBusStations = !showBusStations" class="toggle-stations-btn">
            {{ showBusStations ? '▲ Ascunde stațiile' : '▼ Arată toate stațiile' }}
          </button>
        </div>
      </div>
      
      <!-- Pas 3: Mers pe jos la destinație -->
      <div class="route-section">
        <div class="section-header walking">
          <span class="icon">🚶</span>
          <div class="section-title">
            <strong>Mergi pe jos</strong>
            <small>{{ secondWalkDistance }} m · {{ secondWalkTime }} min</small>
          </div>
        </div>
        
        <div class="section-content">
          <div class="location-point station">
            <span class="marker">🚏</span>
            <span>{{ alightingStation }}</span>
          </div>
          
          <div v-for="(step, index) in secondWalkingSteps" :key="`walk2-${index}`" class="step-detail">
            <span class="step-icon">{{ getStepIcon(step.type) }}</span>
            <div class="step-text">
              <strong>{{ step.instruction }}</strong>
              <small>{{ step.distance }} m</small>
            </div>
          </div>
          
          <div class="location-point destination">
            <span class="marker">🎯</span>
            <span>{{ endLocation }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface Props {
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
  firstWalkingSteps?: any[]
  secondWalkingSteps?: any[]
}

const props = withDefaults(defineProps<Props>(), {
  visible: false,
  firstWalkingSteps: () => [],
  secondWalkingSteps: () => []
})

defineEmits<{
  close: []
}>()

const showBusStations = ref(false)

const totalDistance = computed(() => {
  return ((props.firstWalkDistance + props.secondWalkDistance) / 1000).toFixed(2)
})

const totalTime = computed(() => {
  return props.firstWalkTime + props.busTime + props.secondWalkTime
})

const busStations = computed(() => {
  return props.busStationsList.length
})

const getStepIcon = (type: string) => {
  const icons: Record<string, string> = {
    'turn left': '↰',
    'turn right': '↱',
    'continue': '↑',
    'depart': '🚶',
    'arrive': '🎯',
    'straight': '↑'
  }
  return icons[type] || '→'
}
</script>

<style scoped>
.multimodal-panel {
  position: absolute;
  top: 90px;
  left: 320px;
  width: 400px;
  max-height: calc(100vh - 120px);
  background: white;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  z-index: 1000;
  font-family: 'Inter', system-ui, sans-serif;
}

.panel-header {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  color: white;
  padding: 20px;
}

.header-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.header-top h3 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
}

.close-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  font-size: 20px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: scale(1.1);
}

.summary-info {
  display: flex;
  gap: 24px;
}

.summary-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.summary-item .icon {
  font-size: 24px;
}

.summary-item strong {
  display: block;
  font-size: 18px;
}

.summary-item small {
  font-size: 12px;
  opacity: 0.9;
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.route-section {
  margin-bottom: 24px;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: #f3f4f6;
  border-radius: 8px;
  border-left: 4px solid;
  margin-bottom: 12px;
}

.section-header.walking {
  border-left-color: #10b981;
}

.section-header.bus {
  border-left-color: #3b82f6;
  background: #eff6ff;
}

.section-header .icon {
  font-size: 24px;
}

.section-title {
  flex: 1;
}

.section-title strong {
  display: block;
  font-size: 16px;
  color: #1f2937;
}

.section-title small {
  font-size: 13px;
  color: #6b7280;
}

.section-content {
  padding-left: 20px;
  border-left: 3px dashed #e5e7eb;
  margin-left: 20px;
}

.location-point {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  margin-bottom: 8px;
  background: white;
  border-radius: 8px;
  font-weight: 500;
}

.location-point.start {
  background: #dcfce7;
  color: #166534;
}

.location-point.station {
  background: #dbeafe;
  color: #1e40af;
}

.location-point.destination {
  background: #fef3c7;
  color: #92400e;
}

.location-point .marker {
  font-size: 20px;
}

.step-detail {
  display: flex;
  gap: 12px;
  padding: 10px;
  margin-bottom: 4px;
  align-items: start;
}

.step-icon {
  font-size: 20px;
  min-width: 24px;
  text-align: center;
}

.step-text {
  flex: 1;
}

.step-text strong {
  display: block;
  font-size: 14px;
  color: #1f2937;
  margin-bottom: 2px;
}

.step-text small {
  font-size: 12px;
  color: #6b7280;
}

.bus-instructions {
  padding: 16px;
  background: #eff6ff;
  border-radius: 8px;
  margin-bottom: 12px;
}

.bus-instructions p {
  margin: 8px 0;
  color: #1e40af;
  font-size: 14px;
}

.bus-stations-list {
  margin: 12px 0;
  padding: 12px;
  background: #f9fafb;
  border-radius: 8px;
}

.bus-station-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 6px 0;
  font-size: 13px;
  color: #4b5563;
}

.station-dot {
  width: 8px;
  height: 8px;
  background: #3b82f6;
  border-radius: 50%;
}

.toggle-stations-btn {
  width: 100%;
  padding: 10px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  color: #3b82f6;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.toggle-stations-btn:hover {
  background: #f3f4f6;
  border-color: #3b82f6;
}

/* Mobile responsive */
@media (max-width: 768px) {
  .multimodal-panel {
    left: 0;
    bottom: 0;
    top: auto;
    width: 100%;
    max-height: 70vh;
    border-radius: 16px 16px 0 0;
  }
}

/* Scrollbar styling */
.panel-body::-webkit-scrollbar {
  width: 6px;
}

.panel-body::-webkit-scrollbar-track {
  background: #f3f4f6;
}

.panel-body::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 3px;
}

.panel-body::-webkit-scrollbar-thumb:hover {
  background: #9ca3af;
}
</style>
