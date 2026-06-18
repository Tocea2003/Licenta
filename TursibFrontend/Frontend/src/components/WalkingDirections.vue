<template>
  <div v-if="isVisible" class="directions-panel">
    <div class="directions-header">
      <div class="header-content">
        <h3>🚶 {{ t('walkingDirections') }}</h3>
        <button @click="close" class="close-btn">✕</button>
      </div>
      
      <div v-if="!loading && walkingData" class="summary">
        <div class="summary-item">
          <span class="icon">📏</span>
          <div class="summary-text">
            <strong>{{ (walkingData.distance / 1000).toFixed(2) }} km</strong>
            <small>{{ t('distanceLabel') }}</small>
          </div>
        </div>
        <div class="summary-item">
          <span class="icon">⏱️</span>
          <div class="summary-text">
            <strong>{{ Math.ceil(walkingData.duration / 60) }} min</strong>
            <small>{{ t('estimatedTime') }}</small>
          </div>
        </div>
      </div>
    </div>
    
    <div class="directions-body">
      <div v-if="loading" class="loading-state">
        <div class="spinner-large">🔄</div>
        <p>{{ t('calculatingRoute') }}</p>
      </div>
      
      <div v-else-if="error" class="error-state">
        <span class="error-icon">⚠️</span>
        <p>{{ error }}</p>
        <button @click="retry" class="retry-btn">{{ t('retry') }}</button>
      </div>
      
      <div v-else-if="walkingData" class="directions-list">
        <div class="route-info">
          <div class="location-item start">
            <span class="marker">📍</span>
            <span class="location-name">{{ startName }}</span>
          </div>
          
          <div class="steps-container">
            <div
              v-for="(step, index) in walkingData.steps"
              :key="index"
              class="step-item"
              :class="{ 'step-active': activeStep === index }"
              @click="activeStep = index"
            >
              <div class="step-number">{{ index + 1 }}</div>
              <div class="step-content">
                <div class="step-instruction">
                  <span class="step-icon">{{ getStepIcon(step.maneuver.type) }}</span>
                  <span class="step-text">{{ getStepInstruction(step) }}</span>
                </div>
                <div class="step-details">
                  <span class="step-distance">{{ step.distance > 1000 ? (step.distance / 1000).toFixed(2) + ' km' : step.distance.toFixed(0) + ' m' }}</span>
                  <span class="step-time">{{ Math.ceil(step.duration / 60) }} min</span>
                </div>
              </div>
            </div>
          </div>
          
          <div class="location-item end">
            <span class="marker">🚏</span>
            <span class="location-name">{{ endName }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

interface Props {
  visible: boolean
  startLat?: number
  startLon?: number
  endLat?: number
  endLon?: number
  startName?: string
  endName?: string
}

const props = withDefaults(defineProps<Props>(), {
  visible: false,
  startName: 'Poziția ta',
  endName: 'Destinație'
})

interface WalkingStep {
  distance: number
  duration: number
  name: string
  maneuver: {
    type: string
    modifier?: string
    location: [number, number]
  }
}

interface WalkingData {
  distance: number
  duration: number
  steps: WalkingStep[]
  geometry: [number, number][]
}

const emit = defineEmits<{
  close: []
  routeCalculated: [geometry: [number, number][]]
  snappedCoordinates: [start: {lat: number, lon: number}, end: {lat: number, lon: number}]
}>()

const isVisible = ref(false)
const loading = ref(false)
const error = ref<string | null>(null)
const walkingData = ref<WalkingData | null>(null)
const activeStep = ref<number | null>(null)

// Watch pentru vizibilitate
watch(() => props.visible, (newVal) => {
  isVisible.value = newVal
  if (newVal && props.startLat && props.startLon && props.endLat && props.endLon) {
    calculateWalkingRoute()
  }
})

// Calculează traseul de mers pe jos folosind OSRM
const calculateWalkingRoute = async () => {
  if (!props.startLat || !props.startLon || !props.endLat || !props.endLon) {
    error.value = t('missingCoordinates')
    return
  }
  
  loading.value = true
  error.value = null
  
  try {
    // Calculează distanța directă (linie dreaptă)
    const straightDistance = calculateDistance(props.startLat, props.startLon, props.endLat, props.endLon)
    
    console.log('📏 Distanță directă:', (straightDistance * 1000).toFixed(0), 'm')
    
    // Calculează traseul cu Valhalla - permite ignorarea restricțiilor de sens unic
    const valhallaRequest = {
      locations: [
        { lat: props.startLat, lon: props.startLon },
        { lat: props.endLat, lon: props.endLon }
      ],
      costing: "auto",
      costing_options: {
        auto: {
          ignore_restrictions: true,
          ignore_oneways: true
        }
      },
      directions_options: {
        language: "ro-RO"
      }
    }
    
    const response = await fetch('https://valhalla1.openstreetmap.de/route', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(valhallaRequest)
    })
    
    const data = await response.json()
    
    console.log('📦 Valhalla response:', data)
    
    if (data.trip && data.trip.legs && data.trip.legs.length > 0) {
      const leg = data.trip.legs[0]
      
      console.log('✅ Traseu găsit:', (leg.summary.length).toFixed(2), 'km')
      
      // Valhalla returnează geometrie encoded, trebuie decodat
      const geometry = decodePolyline(leg.shape).map((coord: number[]) => 
        [coord[0], coord[1]] as [number, number]
      )
      
      // Convertim maneuvers Valhalla la formatul nostru
      const steps = leg.maneuvers.map((maneuver: any) => ({
        distance: maneuver.length * 1000, // km -> metri
        duration: maneuver.time,
        name: maneuver.street_names ? maneuver.street_names.join(', ') : 'traseu',
        maneuver: {
          type: convertValhallaManeuverType(maneuver.type),
          location: [0, 0] // Valhalla nu dă coordonate exact
        }
      }))
      
      walkingData.value = {
        distance: leg.summary.length * 1000, // convertim din km în metri
        duration: leg.summary.time,
        steps: steps,
        geometry
      }
      
      // Emite geometria pentru a fi afișată pe hartă
      emit('routeCalculated', geometry)
      
      // Emite coordonatele originale pentru markere
      emit('snappedCoordinates', 
        { lat: props.startLat, lon: props.startLon },
        { lat: props.endLat, lon: props.endLon }
      )
      
      console.log('✅ Traseu calculat cu Valhalla (fără restricții)')
      console.log('📏 Distanță:', (leg.summary.length).toFixed(2), 'km')
      console.log('⏱️ Durată:', Math.ceil(leg.summary.time / 60), 'minute')
    } else {
      console.error('❌ Valhalla error:', data)
      console.log('🔄 Folosesc traseu direct ca fallback')
      createDirectRoute(props.startLat, props.startLon, props.endLat, props.endLon, straightDistance)
    }
  } catch (err) {
    console.error('❌ Eroare la calcularea traseului:', err)
    // Fallback la traseu direct
    const straightDistance = calculateDistance(props.startLat!, props.startLon!, props.endLat!, props.endLon!)
    createDirectRoute(props.startLat!, props.startLon!, props.endLat!, props.endLon!, straightDistance)
  } finally {
    loading.value = false
  }
}

// Creează un traseu direct (linie dreaptă) când OSRM nu funcționează bine
const createDirectRoute = (startLat: number, startLon: number, endLat: number, endLon: number, distanceKm: number) => {
  // Creează o geometrie simplă cu linie dreaptă
  const geometry: [number, number][] = [
    [startLat, startLon],
    [endLat, endLon]
  ]
  
  // Creează un singur step simplu
  const steps: WalkingStep[] = [
    {
      distance: distanceKm * 1000,
      duration: (distanceKm / 5) * 3600, // ~5 km/h walking speed
      name: 'traseu direct',
      maneuver: {
        type: 'depart',
        location: [startLon, startLat]
      }
    },
    {
      distance: 0,
      duration: 0,
      name: 'destinație',
      maneuver: {
        type: 'arrive',
        location: [endLon, endLat]
      }
    }
  ]
  
  walkingData.value = {
    distance: distanceKm * 1000,
    duration: (distanceKm / 5) * 3600,
    steps,
    geometry
  }
  
  emit('routeCalculated', geometry)
  emit('snappedCoordinates', 
    { lat: startLat, lon: startLon },
    { lat: endLat, lon: endLon }
  )
  
  console.log('✅ Traseu direct creat:', (distanceKm * 1000).toFixed(0), 'm')
}

// Snap coordonatele la cel mai apropiat punct pe stradă
const snapToNearestRoad = async (lon: number, lat: number): Promise<{lon: number, lat: number} | null> => {
  try {
    // OSRM Nearest API - găsește punctul cel mai apropiat pe rețeaua de drumuri
    // Folosim radiusul de 50m pentru a evita snap-uri greșite
    const url = `https://router.project-osrm.org/nearest/v1/foot/${lon},${lat}?number=1`
    
    const response = await fetch(url)
    const data = await response.json()
    
    if (data.code === 'Ok' && data.waypoints && data.waypoints.length > 0) {
      const waypoint = data.waypoints[0]
      const snappedLon = waypoint.location[0]
      const snappedLat = waypoint.location[1]
      
      // Calculează distanța de snap
      const distance = calculateDistance(lat, lon, snappedLat, snappedLon)
      
      // Dacă distanța de snap este mai mare de 30m, nu accepta - e prea departe
      if (distance > 0.03) {
        console.warn('⚠️ Distanță prea mare la snap:', (distance * 1000).toFixed(0), 'm - folosesc coordonate originale')
        return null
      }
      
      console.log('✅ Snap la drum:', (distance * 1000).toFixed(0), 'm')
      
      return {
        lon: snappedLon,
        lat: snappedLat
      }
    }
    
    return null
  } catch (err) {
    console.error('❌ Eroare la snap to road:', err)
    return null
  }
}

// Calculează distanța între două puncte (Haversine)
const calculateDistance = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371 // km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a = 
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}

// Decodează polyline Valhalla (format encoded)
const decodePolyline = (encoded: string, precision = 6): number[][] => {
  const inv = 1.0 / Math.pow(10, precision)
  const decoded: number[][] = []
  let previous = [0, 0]
  let i = 0
  
  while (i < encoded.length) {
    const ll = [0, 0]
    for (let j = 0; j < 2; j++) {
      let shift = 0
      let byte = 0x20
      let result = 0
      
      while (byte >= 0x20) {
        byte = encoded.charCodeAt(i++) - 63
        result |= (byte & 0x1f) << shift
        shift += 5
      }
      
      const prevValue = previous[j] || 0
      ll[j] = prevValue + (result & 1 ? ~(result >> 1) : result >> 1)
      previous[j] = ll[j] || 0
    }
    
    decoded.push([(ll[0] || 0) * inv, (ll[1] || 0) * inv])
  }
  
  return decoded
}

// Convertește tipurile de manevre Valhalla la OSRM
const convertValhallaManeuverType = (valhallaType: number): string => {
  // https://valhalla.github.io/valhalla/api/turn-by-turn/api-reference/#maneuvers
  const typeMap: Record<number, string> = {
    0: 'depart',
    1: 'turn right',
    2: 'turn right',
    3: 'turn right',
    4: 'turn left',
    5: 'turn left',
    6: 'turn left',
    7: 'continue',
    8: 'continue',
    9: 'continue',
    10: 'uturn',
    15: 'arrive',
    16: 'arrive',
    17: 'arrive',
    18: 'continue',
    19: 'continue',
    20: 'roundabout',
    21: 'roundabout',
    22: 'continue'
  }
  return typeMap[valhallaType] || 'continue'
}

// Obține iconița pentru tipul de manevră
const getStepIcon = (type: string): string => {
  const icons: Record<string, string> = {
    'depart': '🚶',
    'arrive': '🎯',
    'turn': '↪️',
    'new name': '➡️',
    'continue': '⬆️',
    'end of road': '🔚',
    'fork': '🔱',
    'roundabout': '🔄',
    'rotary': '🔄'
  }
  return icons[type] || '➡️'
}

// Generează instrucțiunea pentru step
const getStepInstruction = (step: WalkingStep): string => {
  const type = step.maneuver.type
  const modifier = step.maneuver.modifier
  const name = step.name || 'drumul'
  
  if (type === 'depart') {
    return `Pleacă ${modifier ? 'spre ' + translateModifier(modifier) : ''}`
  }
  
  if (type === 'arrive') {
    return 'Ai ajuns la destinație'
  }
  
  if (type === 'turn') {
    return `Cotește ${translateModifier(modifier || '')} pe ${name}`
  }
  
  if (type === 'new name') {
    return `Continuă pe ${name}`
  }
  
  if (type === 'continue') {
    return `Mergi înainte pe ${name}`
  }
  
  return `Mergi pe ${name}`
}

// Traducere modificatori
const translateModifier = (modifier: string): string => {
  const translations: Record<string, string> = {
    'left': 'la stânga',
    'right': 'la dreapta',
    'sharp left': 'brusc la stânga',
    'sharp right': 'brusc la dreapta',
    'slight left': 'ușor la stânga',
    'slight right': 'ușor la dreapta',
    'straight': 'înainte',
    'uturn': 'înapoi'
  }
  return translations[modifier] || modifier
}

const close = () => {
  isVisible.value = false
  emit('close')
}

const retry = () => {
  calculateWalkingRoute()
}
</script>

<style scoped>
.directions-panel {
  position: absolute;
  top: 90px;
  left: 370px;
  width: 380px;
  max-width: calc(100vw - 340px);
  max-height: calc(100vh - 120px);
  background: white;
  border-radius: 16px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.12);
  z-index: 550;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.2);
}

/* Mobile responsive */
@media (max-width: 768px) {
  .directions-panel {
    top: auto;
    bottom: 0;
    left: 0;
    right: 0;
    width: 100%;
    max-width: 100%;
    max-height: 70vh;
    border-radius: 16px 16px 0 0;
    box-shadow: 0 -4px 24px rgba(0, 0, 0, 0.15);
  }
}

.directions-header {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  color: white;
  padding: 20px;
}

/* Mobile header */
@media (max-width: 768px) {
  .directions-header {
    padding: 16px;
    position: relative;
  }
  
  .directions-header::before {
    content: '';
    position: absolute;
    top: 8px;
    left: 50%;
    transform: translateX(-50%);
    width: 40px;
    height: 4px;
    background: rgba(255, 255, 255, 0.3);
    border-radius: 2px;
  }
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.directions-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}
.summary {
  display: flex;
  gap: 20px;
}

/* Mobile summary */
@media (max-width: 768px) {
    .summary {
        gap: 16px;
        flex-wrap: wrap;
    }

    .summary-item {
        flex: 1;
        min-width: 120px;
    }
}

/* Close button styles */
.close-btn {
    background: transparent;
    border: none;
    width: 32px;
    height: 32px;
    border-radius: 50%;
    color: white;
    font-size: 20px;
    cursor: pointer;
    transition: background 0.2s;
    display: flex;
    align-items: center;
    justify-content: center;
}

.close-btn:hover {
    background: rgba(255, 255, 255, 0.3);
}

.summary {
  display: flex;
  gap: 20px;
}

.summary-item {
  display: flex;
  align-items: center;
  gap: 10px;
}

.summary-item .icon {
  font-size: 24px;
}

.summary-text {
  display: flex;
  flex-direction: column;
}

.summary-text strong {
  font-size: 18px;
  font-weight: 600;
}

.summary-text small {
  font-size: 12px;
  opacity: 0.9;
}

.directions-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.loading-state, .error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  text-align: center;
  color: #6b7280;
}

.spinner-large {
  font-size: 48px;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.error-icon {
  font-size: 48px;
  margin-bottom: 12px;
}

.error-state p {
  margin: 8px 0 16px;
  color: #ef4444;
}

.retry-btn {
  background: #3b82f6;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: background 0.2s;
}

.retry-btn:hover {
  background: #2563eb;
}

.directions-list {
  padding: 0;
}

.route-info {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.location-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: #f3f4f6;
  border-radius: 8px;
}

.location-item.start .marker {
  font-size: 24px;
}

.location-item.end .marker {
  font-size: 24px;
}

.location-name {
  font-weight: 500;
  color: #1f2937;
  flex: 1;
}

.steps-container {
  position: relative;
}

.step-item {
  position: relative;
  padding: 12px 16px;
  margin-bottom: 8px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.step-item:hover {
  border-color: #3b82f6;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.1);
}

/* Mobile touch */
@media (max-width: 768px) {
  .step-item {
    padding: 14px;
    margin-bottom: 10px;
    min-height: 48px; /* Touch target */
  }
  
  .step-item:active {
    border-color: #2563eb;
    background: #eff6ff;
  }
}

.step-item.step-active {
  border-color: #3b82f6;
  background: #eff6ff;
}

.step-number {
  position: absolute;
  left: -32px;
  top: 50%;
  transform: translateY(-50%);
  width: 20px;
  height: 20px;
  background: white;
  border: 2px solid #3b82f6;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: 600;
  color: #3b82f6;
}

.step-content {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.step-instruction {
  display: flex;
  align-items: center;
  gap: 8px;
}

.step-icon {
  font-size: 18px;
}

.step-text {
  font-size: 14px;
  color: #1f2937;
  font-weight: 500;
}

.step-details {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #6b7280;
}

.step-distance, .step-time {
  display: flex;
  align-items: center;
}

.step-distance::before {
  content: '📏';
  margin-right: 4px;
}

.step-time::before {
  content: '⏱️';
  margin-right: 4px;
}

/* Scrollbar styling */
.directions-body::-webkit-scrollbar {
  width: 6px;
}

.directions-body::-webkit-scrollbar-track {
  background: #f3f4f6;
}

.directions-body::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 3px;
}

.directions-body::-webkit-scrollbar-thumb:hover {
  background: #9ca3af;
}
</style>
