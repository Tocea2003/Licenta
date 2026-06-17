<template>
  <button 
    class="location-button" 
    @click="findMyLocation"
    :disabled="loading"
    :title="loading ? t('findingLocation') : t('myLocation')"
  >
    <span v-if="loading" class="spinner">⌛</span>
    <span v-else>📍</span>
  </button>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const emit = defineEmits<{
  locationFound: [lat: number, lon: number]
}>()

const loading = ref(false)

const requestCurrentPosition = (options: PositionOptions) => {
  return new Promise<GeolocationPosition>((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, options)
  })
}

const findMyLocation = async () => {
  if (!navigator.geolocation) {
    alert(t('geolocationNotSupported'))
    return
  }

  loading.value = true

  try {
    let position = await requestCurrentPosition({
      enableHighAccuracy: true,
      timeout: 12000,
      maximumAge: 0
    })

    // Dacă precizia este slabă, mai facem o încercare cu timeout mai mare.
    if (position.coords.accuracy > 1000) {
      try {
        position = await requestCurrentPosition({
          enableHighAccuracy: true,
          timeout: 18000,
          maximumAge: 0
        })
      } catch (retryError) {
        // Păstrăm prima poziție dacă retry-ul eșuează.
      }
    }

    const lat = position.coords.latitude
    const lon = position.coords.longitude
    console.log(
      `✅ Locația ta: [${lat}, ${lon}] (accuracy: ${Math.round(position.coords.accuracy)}m)`
    )
    emit('locationFound', lat, lon)
  } catch (error: any) {
    console.error('❌ Eroare geolocation:', error)
    alert(`${t('cannotGetLocation')}: ${error?.message || ''}`)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.location-button {
  width: 44px;
  height: 44px;
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
