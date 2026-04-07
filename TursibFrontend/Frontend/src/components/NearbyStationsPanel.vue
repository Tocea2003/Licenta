<template>
  <div v-if="visible" class="nearby-panel">
    <div class="panel-header">
      <h3>🚏 Stații apropiate</h3>
      <button @click="$emit('close')" class="close-btn">✕</button>
    </div>
    
    <div class="stations-list">
      <div 
        v-for="station in stations" 
        :key="station.id"
        class="station-item"
      >
        <div class="station-info">
          <div class="station-name">{{ station.name }}</div>
          <div class="station-distance">{{ formatDistance(station.distance) }}</div>
        </div>
        
        <!-- ETA pentru autobuze -->
        <div v-if="getStationETAs(station.id).length > 0" class="station-etas">
          <div v-for="eta in getStationETAs(station.id)" :key="eta.busId" class="eta-item">
            <span class="eta-bus" :style="{ color: eta.color }">🚌 {{ eta.routeNumber }}</span>
            <span class="eta-time">{{ eta.eta }}</span>
          </div>
        </div>
        
        <div class="notification-summary" v-if="isNotificationActive(station.id)">
          <span v-if="isRouteNotificationActive(station.id)">
            Alerte active pentru traseul {{ getActiveRouteForStation(station.id) }}
          </span>
          <span v-else>
            Alerte active pentru toate autobuzele din stație
          </span>
        </div>

        <!-- Buton notificări pentru toată stația -->
        <button
          @click="$emit('toggleNotification', { stationId: station.id })"
          class="notification-btn"
          :class="{ active: isNotificationActive(station.id) && !isRouteNotificationActive(station.id) }"
        >
          <span v-if="isNotificationActive(station.id) && !isRouteNotificationActive(station.id)">
            🔕 Dezactivează
          </span>
          <span v-else>
            🔔 Alarme pentru stație
          </span>
        </button>

        <div v-if="getStationETAs(station.id).length > 0" class="route-quick-actions">
          <div class="route-quick-title">Rapid pe traseu:</div>
          <div class="route-chip-list">
            <button
              v-for="eta in getUniqueRouteETAs(station.id)"
              :key="`${station.id}-${eta.routeId}`"
              class="route-chip"
              :class="{ active: isSpecificRouteActive(station.id, eta.routeId) }"
              :style="{ borderColor: eta.color, color: eta.color }"
              @click="$emit('toggleNotification', { stationId: station.id, routeId: eta.routeId })"
            >
              <span>🔔 {{ eta.routeNumber }}</span>
            </button>
          </div>
        </div>
      </div>
      
      <div v-if="stations.length === 0" class="no-stations">
        Nu sunt stații în apropiere
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { Station } from '@/services/apiService'

interface StationWithDistance extends Station {
  distance: number
}

interface Props {
  visible: boolean
  stations: StationWithDistance[]
  activeNotificationStationId: number | null
  activeNotificationRouteId: number | null
  getStationETAs: (stationId: number) => Array<{
    busId: string
    routeId: number
    routeNumber: string
    eta: string
    color: string
  }>
}

const props = defineProps<Props>()

const emit = defineEmits<{
  close: []
  toggleNotification: [payload: { stationId: number; routeId?: number }]
}>()

const formatDistance = (distance: number): string => {
  if (distance < 1) {
    return `${Math.round(distance * 1000)} m`
  }
  return `${distance.toFixed(1)} km`
}

const isNotificationActive = (stationId: number): boolean => {
  return props.activeNotificationStationId === stationId
}

const isRouteNotificationActive = (stationId: number): boolean => {
  return isNotificationActive(stationId) && props.activeNotificationRouteId !== null
}

const isSpecificRouteActive = (stationId: number, routeId: number): boolean => {
  return isNotificationActive(stationId) && props.activeNotificationRouteId === routeId
}

const getUniqueRouteETAs = (stationId: number) => {
  const etas = props.getStationETAs(stationId)
  const seen = new Set<number>()
  const unique: typeof etas = []

  for (const eta of etas) {
    if (!seen.has(eta.routeId)) {
      seen.add(eta.routeId)
      unique.push(eta)
    }
  }

  return unique.slice(0, 4)
}

const getActiveRouteForStation = (stationId: number): string | null => {
  if (!isRouteNotificationActive(stationId) || props.activeNotificationRouteId === null) {
    return null
  }

  const matchingETA = props.getStationETAs(stationId).find(
    eta => eta.routeId === props.activeNotificationRouteId
  )

  return matchingETA?.routeNumber ?? `Linia ${props.activeNotificationRouteId}`
}
</script>

<style scoped>
.nearby-panel {
  position: fixed;
  right: 20px;
  top: 80px;
  width: 340px;
  max-height: calc(100vh - 100px);
  background: var(--bg-primary);
  border-radius: 16px;
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--border-color);
  z-index: 1200;
  display: flex;
  flex-direction: column;
  animation: slideInRight 0.3s ease-out;
}

@keyframes slideInRight {
  from {
    transform: translateX(100%);
    opacity: 0;
  }
  to {
    transform: translateX(0);
    opacity: 1;
  }
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  background: var(--gradient-primary);
  color: white;
  border-radius: 16px 16px 0 0;
  backdrop-filter: blur(12px);
}

.panel-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.close-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: white;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  font-size: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: rotate(90deg);
}

.stations-list {
  overflow-y: auto;
  padding: 12px;
  max-height: calc(100vh - 180px);
}

.stations-list::-webkit-scrollbar {
  width: 6px;
}

.stations-list::-webkit-scrollbar-track {
  background: var(--bg-tertiary);
  border-radius: 3px;
}

.stations-list::-webkit-scrollbar-thumb {
  background: #667eea;
  border-radius: 3px;
}

.station-item {
  background: var(--bg-secondary);
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 10px;
  border: 1px solid var(--border-color);
  transition: all 0.2s;
}

.station-item:hover {
  border-color: #667eea;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.1);
}

.station-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.station-name {
  font-weight: 600;
  color: var(--text-primary);
  font-size: 14px;
  flex: 1;
}

.station-distance {
  color: var(--text-secondary);
  font-size: 12px;
  background: var(--bg-primary);
  padding: 2px 8px;
  border-radius: 12px;
  border: 1px solid var(--border-color);
}

.station-etas {
  margin: 8px 0;
  padding: 8px;
  background: var(--bg-primary);
  border-radius: 6px;
  border: 1px solid var(--border-color);
}

.eta-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 0;
  font-size: 13px;
}

.eta-bus {
  font-weight: 600;
}

.eta-time {
  color: #059669;
  font-weight: 600;
  background: #d1fae5;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 12px;
}

.notification-btn {
  width: 100%;
  padding: 8px 12px;
  border: 2px solid var(--accent-primary);
  background: var(--bg-primary);
  color: var(--accent-primary);
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.2s;
  margin-top: 8px;
}

.notification-btn:hover {
  background: var(--accent-primary);
  color: var(--text-on-accent);
  transform: translateY(-1px);
  box-shadow: var(--shadow-sm);
}

.notification-btn.active {
  background: #ef4444;
  border-color: #ef4444;
  color: white;
}

.notification-btn.active:hover {
  background: #dc2626;
  border-color: #dc2626;
}

.notification-summary {
  margin-top: 8px;
  margin-bottom: 4px;
  font-size: 12px;
  color: var(--accent-primary);
  background: var(--accent-primary-soft);
  border: 1px solid var(--border-secondary);
  border-radius: 6px;
  padding: 6px 8px;
}

.route-quick-actions {
  margin-top: 8px;
}

.route-quick-title {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 6px;
}

.route-chip-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.route-chip {
  border: 1px solid var(--border-secondary);
  background: var(--bg-primary);
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
  padding: 4px 10px;
  cursor: pointer;
  transition: all 0.2s;
}

.route-chip:hover {
  transform: translateY(-1px);
  box-shadow: var(--shadow-sm);
}

.route-chip.active {
  background: #dc2626;
  color: white !important;
  border-color: #dc2626 !important;
}

.no-stations {
  text-align: center;
  padding: 40px 20px;
  color: var(--text-secondary);
  font-size: 14px;
}

@media (max-width: 900px) {
  .nearby-panel {
    right: 10px;
    top: 116px;
    width: calc(100% - 20px);
    max-width: 340px;
    max-height: calc(100vh - 130px);
  }
}
</style>
