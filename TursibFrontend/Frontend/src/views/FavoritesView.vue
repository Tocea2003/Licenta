<template>
  <div class="favorites-page">
    <header>
      <h1 class="page-title">{{ t('favLocationsTitle') }}</h1>
      <p class="page-subtitle">{{ t('favLocationsSubtitle') }}</p>
    </header>

    <p v-if="notificationMessage" class="feedback" :class="notificationMessageType" role="status">
      {{ notificationMessage }}
    </p>

    <div class="quick-add">
      <button v-if="!homeFavorite" type="button" class="quick-btn" @click="openAddDialog('home')">
        <Home :size="18" aria-hidden="true" />{{ t('addHome') }}
      </button>
      <button v-if="!workFavorite" type="button" class="quick-btn" @click="openAddDialog('work')">
        <Briefcase :size="18" aria-hidden="true" />{{ t('addWork') }}
      </button>
      <button type="button" class="quick-btn quick-btn--primary" @click="openAddDialog('custom')">
        <Plus :size="18" aria-hidden="true" />{{ t('addNewLocation') }}
      </button>
    </div>

    <ul v-if="orderedFavorites.length > 0" class="favorites-list">
      <li v-for="favorite in orderedFavorites" :key="favorite.id" class="favorite-item">
        <button type="button" class="favorite-main" @click="navigateToFavorite(favorite)">
          <span class="favorite-icon" :class="`is-${favorite.type}`">
            <component :is="favoriteIconComponent(favorite.icon, favorite.type)" :size="20" aria-hidden="true" />
          </span>
          <span class="favorite-info">
            <span class="favorite-name">{{ favorite.name }}</span>
            <span class="favorite-address">{{ favorite.address }}</span>
          </span>
        </button>
        <div class="favorite-actions">
          <button
            type="button"
            class="icon-btn"
            :disabled="notifyingFavoriteId === favorite.id"
            :title="t('enableAlertsForStation')"
            :aria-label="`${t('enableAlertsForStation')}: ${favorite.name}`"
            @click="enableNotificationsForFavorite(favorite)"
          >
            <Bell :size="18" aria-hidden="true" />
          </button>
          <button type="button" class="icon-btn" :aria-label="`${t('editLocation')}: ${favorite.name}`" @click="editFavorite(favorite)">
            <Pencil :size="18" aria-hidden="true" />
          </button>
          <button type="button" class="icon-btn icon-btn--danger" :aria-label="`${t('delete')}: ${favorite.name}`" @click="confirmDelete(favorite)">
            <Trash2 :size="18" aria-hidden="true" />
          </button>
        </div>
      </li>
    </ul>

    <div v-else class="empty-state">
      <MapPin :size="36" class="empty-icon" aria-hidden="true" />
      <h2>{{ t('noSavedLocations') }}</h2>
      <p>{{ t('addFrequentLocations') }}</p>
    </div>

    <!-- Adaugă / editează -->
    <div v-if="showDialog" class="dialog-overlay" @click="closeDialog">
      <div class="dialog" role="dialog" aria-modal="true" :aria-label="editingFavorite ? t('editLocation') : t('addLocation')" @click.stop>
        <h2 class="dialog-title">{{ editingFavorite ? t('editLocation') : t('addLocation') }}</h2>

        <label class="field">
          <span class="field-label">{{ t('locationName') }}</span>
          <input v-model="form.name" type="text" class="input" :placeholder="`${t('homeName')}, ${t('workName')}…`" maxlength="50" />
        </label>

        <div class="field">
          <label class="field-label" for="fav-address">{{ t('locationAddress') }}</label>
          <input id="fav-address" v-model="form.address" type="text" class="input" :placeholder="t('geoSearchPlaceholder')" @input="searchAddress" autocomplete="off" />
          <div v-if="addressSuggestions.length > 0" class="suggestions">
            <button
              v-for="suggestion in addressSuggestions"
              :key="suggestion.place_id"
              type="button"
              class="suggestion-item"
              @click="selectAddress(suggestion)"
            >
              <MapPin :size="16" aria-hidden="true" />
              <span>{{ suggestion.display_name }}</span>
            </button>
          </div>
        </div>

        <div class="field">
          <span class="field-label">{{ t('icon') }}</span>
          <div class="icon-picker" role="radiogroup" :aria-label="t('icon')">
            <button
              v-for="option in FAVORITE_ICONS"
              :key="option.key"
              type="button"
              role="radio"
              :aria-checked="favoriteIconKey(form.icon) === option.key"
              :aria-label="option.key"
              class="icon-option"
              :class="{ selected: favoriteIconKey(form.icon) === option.key }"
              @click="form.icon = option.key"
            >
              <component :is="option.component" :size="20" aria-hidden="true" />
            </button>
          </div>
        </div>

        <div class="dialog-actions">
          <button type="button" class="btn btn-secondary" @click="closeDialog">{{ t('cancel') }}</button>
          <button type="button" class="btn btn-primary" :disabled="!canSave" @click="saveFavorite">
            {{ editingFavorite ? t('save') : t('add') }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useFavorites, type FavoriteLocation } from '@/composables/useFavorites'
import { useLanguage } from '@/composables/useLanguage'
import apiService from '@/services/apiService'
import { enableNotifications } from '@/composables/useNotifications'
import { Bell, Briefcase, Home, MapPin, Pencil, Plus, Trash2 } from 'lucide-vue-next'
import { FAVORITE_ICONS, favoriteIconComponent, favoriteIconKey } from '@/utils/favoriteIcons'

const router = useRouter()
const { t } = useLanguage()

const navigateToFavorite = (favorite: FavoriteLocation) => {
  router.push({
    path: '/',
    query: {
      lat: String(favorite.lat),
      lon: String(favorite.lon),
      zoom: '17',
      label: favorite.name
    }
  })
}

const {
  homeFavorite,
  workFavorite,
  customFavorites,
  addFavorite,
  removeFavorite,
  updateFavorite
} = useFavorites()

const showDialog = ref(false)
const editingFavorite = ref<FavoriteLocation | null>(null)
const notifyingFavoriteId = ref<string | null>(null)
const notificationMessage = ref('')
const notificationMessageType = ref<'success' | 'error'>('success')
const cachedStations = ref<Array<{ id: number; name: string; latitude: number; longitude: number }>>([])
const form = ref({
  name: '',
  address: '',
  lat: 0,
  lon: 0,
  type: 'custom' as 'home' | 'work' | 'custom',
  icon: 'pin'
})

const addressSuggestions = ref<any[]>([])
let searchTimeout: number | null = null

// Acasă, serviciu, apoi restul, în ordinea adăugării
const orderedFavorites = computed(() => [
  ...(homeFavorite.value ? [homeFavorite.value] : []),
  ...(workFavorite.value ? [workFavorite.value] : []),
  ...customFavorites.value
])

const canSave = computed(() => {
  return form.value.name.trim() !== '' && 
         form.value.address.trim() !== '' &&
         form.value.lat !== 0 &&
         form.value.lon !== 0
})

const openAddDialog = (type: 'home' | 'work' | 'custom') => {
  editingFavorite.value = null
  form.value = {
    name: type === 'home' ? t('homeName') : type === 'work' ? t('workName') : '',
    address: '',
    lat: 0,
    lon: 0,
    type,
    icon: type === 'home' ? 'home' : type === 'work' ? 'work' : 'pin'
  }
  showDialog.value = true
}

const editFavorite = (favorite: FavoriteLocation) => {
  editingFavorite.value = favorite
  form.value = {
    name: favorite.name,
    address: favorite.address,
    lat: favorite.lat,
    lon: favorite.lon,
    type: favorite.type,
    icon: favorite.icon
  }
  showDialog.value = true
}

const closeDialog = () => {
  showDialog.value = false
  editingFavorite.value = null
  addressSuggestions.value = []
}

const searchAddress = () => {
  if (searchTimeout) clearTimeout(searchTimeout)
  
  const query = form.value.address.trim()
  if (query.length < 3) {
    addressSuggestions.value = []
    return
  }
  
  searchTimeout = setTimeout(async () => {
    try {
      const response = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}, Sibiu&limit=5`,
        {
          headers: {
            'Accept': 'application/json'
          }
        }
      )
      const data = await response.json()
      addressSuggestions.value = data
    } catch (error) {
      console.error('❌ Error searching address:', error)
      addressSuggestions.value = []
    }
  }, 300)
}

const selectAddress = (suggestion: any) => {
  form.value.address = suggestion.display_name
  form.value.lat = parseFloat(suggestion.lat)
  form.value.lon = parseFloat(suggestion.lon)
  addressSuggestions.value = []
}

const saveFavorite = async () => {
  if (!canSave.value) return
  
  if (editingFavorite.value) {
    // Update existing
    await updateFavorite(editingFavorite.value.id, {
      name: form.value.name,
      address: form.value.address,
      lat: form.value.lat,
      lon: form.value.lon,
      icon: form.value.icon
    })
  } else {
    // Add new
    await addFavorite({
      name: form.value.name,
      address: form.value.address,
      lat: form.value.lat,
      lon: form.value.lon,
      type: form.value.type,
      icon: form.value.icon
    })
  }
  
  closeDialog()
}

const confirmDelete = async (favorite: FavoriteLocation) => {
  if (confirm(t('deleteLocationConfirm', 'Delete location?', { name: favorite.name }))) {
    await removeFavorite(favorite.id)
  }
}

const calculateDistanceKm = (lat1: number, lon1: number, lat2: number, lon2: number): number => {
  const R = 6371
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) *
    Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}

const findNearestStation = (lat: number, lon: number) => {
  if (cachedStations.value.length === 0) {
    return null
  }

  let nearest = cachedStations.value[0]
  if (!nearest) {
    return null
  }

  let minDistance = calculateDistanceKm(lat, lon, nearest.latitude, nearest.longitude)
  for (const station of cachedStations.value) {
    const distance = calculateDistanceKm(lat, lon, station.latitude, station.longitude)
    if (distance < minDistance) {
      minDistance = distance
      nearest = station
    }
  }

  return { station: nearest, distanceKm: minDistance }
}

const setNotificationFeedback = (message: string, type: 'success' | 'error') => {
  notificationMessage.value = message
  notificationMessageType.value = type

  window.setTimeout(() => {
    notificationMessage.value = ''
  }, 5000)
}

const enableNotificationsForFavorite = async (favorite: FavoriteLocation) => {
  notifyingFavoriteId.value = favorite.id
  try {
    if (cachedStations.value.length === 0) {
      cachedStations.value = await apiService.getStations()
    }

    const nearest = findNearestStation(favorite.lat, favorite.lon)
    if (!nearest) {
      setNotificationFeedback(t('notifNoNearbyStation'), 'error')
      return
    }

    const success = await enableNotifications(nearest.station.id, undefined, { etaThresholdMinutes: 3 })
    if (!success) {
      setNotificationFeedback(t('notifPermissionError'), 'error')
      return
    }

    const distanceMeters = Math.round(nearest.distanceKm * 1000)
    setNotificationFeedback(
      t('notifActivatedFor', '', { name: favorite.name, station: nearest.station.name, distance: String(distanceMeters) }),
      'success'
    )
  } catch {
    setNotificationFeedback(t('notifGenericError'), 'error')
  } finally {
    notifyingFavoriteId.value = null
  }
}
</script>

<style scoped>
.favorites-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px 16px 32px;
}

.page-subtitle {
  margin-top: 4px;
}

.feedback {
  margin: 0;
  padding: 12px 14px;
  border-radius: var(--radius-md);
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

.feedback.success { background: var(--color-success-soft); color: var(--color-success); }
.feedback.error { background: var(--color-danger-soft); color: var(--color-danger); }

.quick-add {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.quick-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 44px;
  padding: 0 14px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-full);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  font-size: 14px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.quick-btn:hover {
  background: var(--bg-tertiary);
}

.quick-btn--primary {
  background: var(--accent-primary);
  border-color: var(--accent-primary);
  color: var(--text-on-accent);
}

.quick-btn--primary:hover {
  background: var(--accent-primary-hover);
}

.favorites-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.favorite-item {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 6px 6px 8px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
}

.favorite-main {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 52px;
  padding: 4px;
  border: none;
  border-radius: var(--radius-md);
  background: none;
  color: var(--text-primary);
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.favorite-main:hover .favorite-name {
  text-decoration: underline;
}

.favorite-main:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

.favorite-icon {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.favorite-icon.is-home,
.favorite-icon.is-work {
  background: var(--bg-inverse);
  color: var(--text-inverse);
}

.favorite-info {
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.favorite-name {
  font-size: 15px;
  font-weight: var(--fw-bold);
}

.favorite-address {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.favorite-actions {
  display: flex;
  gap: 2px;
}

.icon-btn {
  width: 40px;
  height: 40px;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.icon-btn:hover:not(:disabled) {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.icon-btn--danger:hover:not(:disabled) {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.icon-btn:disabled {
  opacity: 0.5;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 40px 16px;
  text-align: center;
  color: var(--text-secondary);
}

.empty-state h2 {
  margin: 0;
  font-size: 18px;
  color: var(--text-primary);
}

.empty-state p {
  margin: 0;
}

.empty-icon {
  color: var(--text-tertiary);
}

/* Dialog */
.dialog-overlay {
  position: fixed;
  inset: 0;
  z-index: 2000;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  background: var(--bg-overlay);
}

.dialog {
  width: 100%;
  max-width: 480px;
  max-height: 90dvh;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px 16px 24px;
  border-radius: var(--radius-xl) var(--radius-xl) 0 0;
  background: var(--bg-primary);
  color: var(--text-primary);
}

@media (min-width: 768px) {
  .dialog-overlay {
    align-items: center;
  }

  .dialog {
    border-radius: var(--radius-xl);
  }
}

.dialog-title {
  margin: 0;
  font-size: 20px;
  font-weight: var(--fw-bold);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 14px;
  font-weight: var(--fw-bold);
}

.suggestions {
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  overflow: hidden;
}

.suggestion-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  width: 100%;
  min-height: 44px;
  padding: 10px 12px;
  border: none;
  border-bottom: 1px solid var(--border-subtle);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  font-size: 14px;
  text-align: left;
  cursor: pointer;
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: var(--bg-secondary);
}

.icon-picker {
  display: grid;
  grid-template-columns: repeat(8, minmax(0, 1fr));
  gap: 6px;
}

.icon-option {
  aspect-ratio: 1;
  min-height: 40px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-primary);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.icon-option:hover {
  background: var(--bg-tertiary);
}

.icon-option.selected {
  background: var(--bg-inverse);
  border-color: var(--bg-inverse);
  color: var(--text-inverse);
}

.dialog-actions {
  display: flex;
  gap: 8px;
}

.dialog-actions .btn {
  flex: 1;
}
</style>
