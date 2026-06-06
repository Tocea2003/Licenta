<template>
  <div class="stations-management">
    <div class="page-header">
      <h2>{{ t('manageStations') }}</h2>
      <button @click="showCreateModal = true" class="btn-primary">
        + {{ t('addNewStation') }}
      </button>
    </div>

    <div v-if="isLoading" class="loading">{{ t('loading') }}</div>

    <div v-else class="stations-table-container">
      <table class="stations-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>{{ t('stationName') }}</th>
            <th>{{ t('latitude') }}</th>
            <th>{{ t('longitude') }}</th>
            <th>{{ t('actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="station in paginatedStations" :key="station.id">
            <td>{{ station.id }}</td>
            <td><strong>{{ station.name }}</strong></td>
            <td>{{ station.latitude.toFixed(6) }}</td>
            <td>{{ station.longitude.toFixed(6) }}</td>
            <td>
              <div class="action-buttons">
                <button @click="editStation(station)" class="btn-edit" :title="t('edit')">
                  ✏️
                </button>
                <button @click="viewOnMap(station)" class="btn-map" :title="t('viewOnMap')">
                  🗺️
                </button>
                <button @click="deleteStation(station)" class="btn-delete" :title="t('delete')">
                  🗑️
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="totalPages > 1" class="pagination">
        <button @click="prevPage" :disabled="currentPage === 1" class="page-btn">&laquo;</button>
        <button v-for="p in visiblePages" :key="p" @click="goToPage(p)" :class="['page-btn', { active: p === currentPage }]">{{ p }}</button>
        <button @click="nextPage" :disabled="currentPage === totalPages" class="page-btn">&raquo;</button>
        <span class="page-info">{{ stations.length }} {{ t('total') || 'total' }}</span>
      </div>
    </div>

    <!-- Modal Create/Edit Station -->
    <div v-if="showCreateModal || showEditModal" class="modal-overlay" @click.self="closeModals">
      <div class="modal">
        <h3>{{ showEditModal ? t('editStationTitle') : t('createStationTitle') }}</h3>
        <form @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>{{ t('stationName') }}:</label>
            <input v-model="currentStation.name" required :placeholder="t('stationExample')" />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>{{ t('latitude') }}:</label>
              <input 
                v-model.number="currentStation.latitude" 
                type="number" 
                step="0.000001" 
                required 
                placeholder="45.7970"
              />
            </div>
            <div class="form-group">
              <label>{{ t('longitude') }}:</label>
              <input 
                v-model.number="currentStation.longitude" 
                type="number" 
                step="0.000001" 
                required 
                placeholder="24.1525"
              />
            </div>
          </div>
          <div class="form-help">
            💡 {{ t('mapTip') }}
          </div>
          <div class="modal-actions">
            <button type="button" @click="closeModals" class="btn-secondary">{{ t('cancel') }}</button>
            <button type="submit" class="btn-primary">{{ showEditModal ? t('save') : t('create') }}</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { adminStationsService, type Station } from '@/services/adminService'
import { usePagination } from '@/composables/usePagination'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const stations = ref<Station[]>([])
const isLoading = ref(true)
const { paginatedItems: paginatedStations, currentPage, totalPages, visiblePages, goToPage, prevPage, nextPage } = usePagination(stations, 10)

const showCreateModal = ref(false)
const showEditModal = ref(false)

const currentStation = ref<Partial<Station>>({
  name: '',
  latitude: 45.797,
  longitude: 24.1525
})

const loadStations = async () => {
  isLoading.value = true
  console.log('📍 Loading stations...')
  try {
    stations.value = await adminStationsService.getStations()
    console.log('✅ Stations loaded successfully:', stations.value.length, 'stations')
  } catch (error: any) {
    console.error('❌ Error loading stations:', error)
    console.error('Error details:', error.response?.data)
    const errorMsg = error.response?.data?.message || error.message || t('loading')
    alert(t('stationsLoadErrorWithMessage', 'Error loading stations: {message}', { message: errorMsg }))
  } finally {
    isLoading.value = false
  }
}

const editStation = (station: Station) => {
  currentStation.value = { ...station }
  showEditModal.value = true
}

const viewOnMap = (station: Station) => {
  const url = `https://www.google.com/maps?q=${station.latitude},${station.longitude}`
  window.open(url, '_blank')
}

const deleteStation = async (station: Station) => {
  if (!confirm(t('confirmDeleteStation', 'Are you sure you want to delete station "{name}"?', { name: station.name }))) return

  try {
    await adminStationsService.deleteStation(station.id)
    await loadStations()
  } catch (error) {
    console.error('Error deleting station:', error)
    alert(t('stationDeleteError'))
  }
}

const handleSubmit = async () => {
  try {
    if (showEditModal.value && currentStation.value.id) {
      await adminStationsService.updateStation(currentStation.value.id, currentStation.value)
    } else {
      await adminStationsService.createStation(currentStation.value as Omit<Station, 'id'>)
    }
    await loadStations()
    closeModals()
  } catch (error) {
    console.error('Error saving station:', error)
    alert(t('stationSaveError'))
  }
}

const closeModals = () => {
  showCreateModal.value = false
  showEditModal.value = false
  currentStation.value = { name: '', latitude: 45.797, longitude: 24.1525 }
}

onMounted(() => {
  loadStations()
})
</script>

<style scoped>
.stations-management {
  background: var(--bg-primary);
  border-radius: 12px;
  padding: 24px;
  box-shadow: var(--shadow-sm);
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h2 {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.btn-primary {
  padding: 10px 20px;
  background: #10b981;
  color: white;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary:hover {
  background: #059669;
}

.loading {
  text-align: center;
  padding: 40px;
  color: var(--text-secondary);
}

.stations-table-container {
  overflow-x: auto;
}

.stations-table {
  width: 100%;
  border-collapse: collapse;
}

.stations-table thead {
  background: var(--bg-secondary);
}

.stations-table th {
  padding: 12px 16px;
  text-align: left;
  font-weight: 600;
  color: var(--text-primary);
  border-bottom: 2px solid var(--border-color);
}

.stations-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color);
  color: var(--text-primary);
}

.action-buttons {
  display: flex;
  gap: 8px;
}

.action-buttons button {
  padding: 6px 12px;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 16px;
}

.btn-edit {
  background: #fbbf24;
}

.btn-edit:hover {
  background: #f59e0b;
}

.btn-map {
  background: #3b82f6;
}

.btn-map:hover {
  background: #2563eb;
}

.btn-delete {
  background: #ef4444;
}

.btn-delete:hover {
  background: #dc2626;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--bg-primary);
  border-radius: 12px;
  padding: 24px;
  width: 100%;
  max-width: 500px;
  max-height: 90vh;
  overflow-y: auto;
}

.modal h3 {
  margin: 0 0 20px 0;
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
}

.form-group {
  margin-bottom: 16px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  font-weight: 600;
  color: var(--text-primary);
}

.form-group input {
  width: 100%;
  padding: 10px 12px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.form-group input:focus {
  outline: none;
  border-color: #10b981;
}

.form-help {
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 16px;
  font-size: 14px;
  color: var(--text-secondary);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 24px;
}

.btn-secondary {
  padding: 10px 20px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-secondary:hover {
  background: var(--bg-tertiary);
}

.pagination { display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 16px; padding: 12px 0; }
.page-btn { min-width: 36px; height: 36px; border: 1px solid var(--border-color); background: var(--bg-primary); color: var(--text-primary); border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 500; transition: all 0.15s; }
.page-btn:hover:not(:disabled) { background: var(--bg-secondary); }
.page-btn.active { background: #3b82f6; color: white; border-color: #3b82f6; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.page-info { margin-left: 12px; font-size: 13px; color: var(--text-secondary); }
</style>
