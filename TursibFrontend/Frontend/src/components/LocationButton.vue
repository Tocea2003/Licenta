<template>
  <button 
    class="location-button" 
    @click="findMyLocation"
    :disabled="loading"
    :title="loading ? 'Găsesc locația...' : 'Locația mea'"
  >
    <span v-if="loading" class="spinner">⌛</span>
    <span v-else>📍</span>
  </button>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{
  locationFound: [lat: number, lon: number]
}>()

const loading = ref(false)

const findMyLocation = () => {
  if (!navigator.geolocation) {
    alert('❌ Browserul tău nu suportă geolocation!')
    return
  }

  loading.value = true

  navigator.geolocation.getCurrentPosition(
    (position) => {
      const lat = position.coords.latitude
      const lon = position.coords.longitude
      
      console.log(`✅ Locația ta: [${lat}, ${lon}]`)
      emit('locationFound', lat, lon)
      
      loading.value = false
    },
    (error) => {
      console.error('❌ Eroare geolocation:', error)
      alert(`❌ Nu pot găsi locația: ${error.message}`)
      loading.value = false
    },
    {
      enableHighAccuracy: true,
      timeout: 5000,
      maximumAge: 0
    }
  )
}
</script>

<style scoped>
.location-button {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: var(--bg-primary);
  border: none;
  cursor: pointer;
  font-size: 20px;
  box-shadow: var(--shadow-md);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  display: flex;
  align-items: center;
  justify-content: center;
}

.location-button:hover:not(:disabled) {
  background: var(--bg-secondary);
  box-shadow: var(--shadow-lg);
  transform: scale(1.05);
}

.location-button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.spinner {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
