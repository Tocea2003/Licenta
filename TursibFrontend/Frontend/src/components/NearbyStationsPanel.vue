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
        
        <!-- Buton notificări -->
        <button 
          @click="$emit('toggleNotification', station.id)"
          class="notification-btn"
          :class="{ active: isNotificationActive(station.id) }"
        >
          <span v-if="isNotificationActive(station.id)">
            🔕 Dezactivează
          </span>
          <span v-else>
            🔔 Activează alerte
          </span>
        </button>
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
  getStationETAs: (stationId: number) => Array<{
    busId: string
    routeNumber: string
    eta: string
    color: string
  }>
}

const props = defineProps<Props>()

const emit = defineEmits<{
  close: []
  toggleNotification: [stationId: number]
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
</script>

<style scoped>
.nearby-panel {
  position: fixed;
  right: 20px;
  top: 80px;
  width: 340px;
  max-height: calc(100vh - 100px);
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
  z-index: 1000;
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
  border-bottom: 1px solid #e5e7eb;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 12px 12px 0 0;
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
  background: #f1f1f1;
  border-radius: 3px;
}

.stations-list::-webkit-scrollbar-thumb {
  background: #667eea;
  border-radius: 3px;
}

.station-item {
  background: #f9fafb;
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 10px;
  border: 1px solid #e5e7eb;
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
  color: #1f2937;
  font-size: 14px;
  flex: 1;
}

.station-distance {
  color: #6b7280;
  font-size: 12px;
  background: white;
  padding: 2px 8px;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.station-etas {
  margin: 8px 0;
  padding: 8px;
  background: white;
  border-radius: 6px;
  border: 1px solid #e5e7eb;
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
  border: 2px solid #667eea;
  background: white;
  color: #667eea;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.2s;
  margin-top: 8px;
}

.notification-btn:hover {
  background: #667eea;
  color: white;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.2);
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

.no-stations {
  text-align: center;
  padding: 40px 20px;
  color: #6b7280;
  font-size: 14px;
}

@media (max-width: 768px) {
  .nearby-panel {
    right: 10px;
    top: 70px;
    width: calc(100% - 20px);
    max-width: 340px;
  }
}
</style>
