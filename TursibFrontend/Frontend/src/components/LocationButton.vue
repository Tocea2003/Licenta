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
  position: absolute;
  top: 20px;
  right: 20px;
  z-index: 1000;
  width: 50px;
  height: 50px;
  border-radius: 50%;
  background: white;
  border: 2px solid #3b82f6;
  cursor: pointer;
  font-size: 24px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Mobile responsive */
@media (max-width: 768px) {
  .location-button {
    top: 80px; /* Below search bar */
    right: 10px;
    width: 44px;
    height: 44px;
    font-size: 20px;
  }
}

.location-button:hover:not(:disabled) {
  background: #eff6ff;
  border-color: #2563eb;
  transform: scale(1.05);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.2);
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
