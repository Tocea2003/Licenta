<template>
  <button
    type="button"
    class="location-button"
    @click="findMyLocation"
    :disabled="loading"
    :title="loading ? t('findingLocation') : t('myLocation')"
    :aria-label="loading ? t('findingLocation') : t('myLocation')"
  >
    <span v-if="loading" class="spinner" aria-hidden="true"></span>
    <LocateFixed v-else :size="20" aria-hidden="true" />
  </button>
</template>

<script setup lang="ts">
import { LocateFixed } from 'lucide-vue-next'
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
      } catch {
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
  border-radius: var(--radius-md);
  background: var(--bg-primary);
  color: var(--accent-primary);
  border: none;
  cursor: pointer;
  box-shadow: var(--shadow-md);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: var(--transition-colors);
}

.location-button:hover:not(:disabled) {
  background: var(--bg-tertiary);
}

.location-button:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus), var(--shadow-md);
}

.location-button:disabled {
  cursor: progress;
}

.spinner {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  border: 2px solid var(--border-primary);
  border-top-color: var(--accent-primary);
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
