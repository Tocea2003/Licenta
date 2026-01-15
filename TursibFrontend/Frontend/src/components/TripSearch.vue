<template>
  <div class="trip-search-container">
    <div class="trip-header">
      <h1>📍 Planificare Călătorie</h1>
      <p class="subtitle">Alege punctele de plecare și destinație</p>
    </div>

    <div class="search-wrapper">
      <!-- Origin Search -->
      <EnhancedSearch
        :stations="stations"
        :user-location="userLocation"
        :trip-mode="true"
        placeholder="De unde pleci?"
        class="origin-search"
        @station-selected="handleOriginSelected"
        @address-selected="handleOriginAddress"
      />

      <!-- Destination Search -->
      <EnhancedSearch
        :stations="stations"
        :user-location="userLocation"
        :trip-mode="true"
        placeholder="Unde mergi?"
        class="destination-search"
        @station-selected="handleDestinationSelected"
        @address-selected="handleDestinationAddress"
      />
    </div>

    <!-- Results would go here -->
    <div v-if="origin && destination" class="results-placeholder">
      <p>✅ Plecare: {{ origin.name }}</p>
      <p>🎯 Destinație: {{ destination.name }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import EnhancedSearch from './EnhancedSearch.vue'
import type { Station } from '@/services/apiService'
import { useStatistics } from '@/composables/useStatistics'

const props = defineProps<{
  stations?: Station[]
  userLocation?: { lat: number; lon: number } | null
}>()

const emit = defineEmits(['route-selected'])

// Statistics tracking
const { incrementTrips, recordStationUsage } = useStatistics()

const origin = ref<any>(null)
const destination = ref<any>(null)

const handleOriginSelected = (station: Station) => {
  origin.value = station
  recordStationUsage(station.id)
  console.log('✅ Origin selected:', station)
}

const handleOriginAddress = (address: any) => {
  origin.value = address
  console.log('✅ Origin address:', address)
}

const handleDestinationSelected = (station: Station) => {
  destination.value = station
  recordStationUsage(station.id)
  console.log('🎯 Destination selected:', station)
  
  if (origin.value) {
    // Track trip completion
    incrementTrips()
    emit('route-selected', { origin: origin.value, destination: destination.value })
  }
}

const handleDestinationAddress = (address: any) => {
  destination.value = address
  console.log('🎯 Destination address:', address)
  
  if (origin.value) {
    // Track trip completion
    incrementTrips()
    emit('route-selected', { origin: origin.value, destination: destination.value })
  }
}
</script>

<style scoped>
.trip-search-container {
  max-width: 600px;
  margin: 0 auto;
  padding: 16px;
}

.trip-header {
  text-align: center;
  margin-bottom: 20px;
}

.trip-header h1 {
  font-size: 1.5rem;
  font-weight: 800;
  color: var(--text-primary);
  margin: 0 0 6px 0;
  text-shadow: var(--shadow-sm);
}

.subtitle {
  color: var(--text-secondary);
  font-size: 0.875rem;
  margin: 0;
}

.search-wrapper {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.origin-search,
.destination-search {
  background: var(--bg-primary);
  border-radius: 12px;
  padding: 6px;
  box-shadow: var(--shadow-md);
}

.results-placeholder {
  margin-top: 16px;
  padding: 14px;
  background: var(--bg-secondary);
  border-radius: 12px;
  box-shadow: var(--shadow-md);
}

.results-placeholder p {
  margin: 8px 0;
  font-weight: 600;
  color: var(--text-primary);
}
</style>