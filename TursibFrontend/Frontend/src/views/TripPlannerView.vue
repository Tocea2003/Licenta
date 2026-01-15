<template>
  <div class="trip-planner-page">
    <TripSearch 
      :stations="allStations" 
      :user-location="userLocation"
      @route-selected="handleRouteSelected" 
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import TripSearch from '@/components/TripSearch.vue'
import { useRouter } from 'vue-router'
import apiService, { type Station } from '@/services/apiService'

const router = useRouter()
const allStations = ref<Station[]>([])
const userLocation = ref<{ lat: number; lon: number } | null>(null)

const loadStations = async () => {
  try {
    allStations.value = await apiService.getStations()
    console.log('✅ Loaded stations for trip planner:', allStations.value.length)
  } catch (error) {
    console.error('❌ Error loading stations:', error)
  }
}

const handleRouteSelected = (result: any) => {
  console.log('Route selected:', result)
  // Traseul se afișează direct în TripSearch component cu harta
}

onMounted(() => {
  loadStations()
  
  // Get user location if available
  if ('geolocation' in navigator) {
    navigator.geolocation.getCurrentPosition(
      (position) => {
        userLocation.value = {
          lat: position.coords.latitude,
          lon: position.coords.longitude
        }
      },
      (error) => console.log('Geolocation error:', error)
    )
  }
})
</script>

<style scoped>
.trip-planner-page {
  min-height: 100vh;
  background: var(--gradient-bg);
  padding: 40px 20px;
  padding-bottom: 100px; /* Space pentru bottom nav */
}
</style>
