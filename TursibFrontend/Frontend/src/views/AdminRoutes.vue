<template>
  <div class="routes-management">
    <div class="page-header">
      <h2>{{ t('manageRoutes') }}</h2>
      <button @click="showCreateModal = true" class="btn-primary">
        + {{ t('addNewRoute') }}
      </button>
    </div>

    <div v-if="isLoading" class="loading">{{ t('loading') }}</div>

    <div v-else class="routes-table-container">
      <table class="routes-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>{{ t('routeNumber') }}</th>
            <th>{{ t('name') }}</th>
            <th>{{ t('color') }}</th>
            <th>{{ t('actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="route in paginatedRoutes" :key="route.id">
            <td>{{ route.id }}</td>
            <td><strong>{{ route.routeNumber }}</strong></td>
            <td>{{ route.name }}</td>
            <td>
              <div class="color-display">
                <div 
                  class="color-box" 
                  :style="{ backgroundColor: route.color || '#3b82f6' }"
                ></div>
                <span>{{ route.color || 'N/A' }}</span>
              </div>
            </td>
            <td>
              <div class="action-buttons">
                <button @click="editRoute(route)" class="btn-edit" :title="t('edit')">
                  ✏️
                </button>
                <button @click="editColor(route)" class="btn-color" :title="t('changeColor')">
                  🎨
                </button>
                <button @click="deleteRoute(route)" class="btn-delete" :title="t('delete')">
                  🗑️
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="pagination">
        <button @click="prevPage" :disabled="currentPage === 1" class="page-btn">&laquo;</button>
        <button v-for="p in visiblePages" :key="p" @click="goToPage(p)" :class="['page-btn', { active: p === currentPage }]">{{ p }}</button>
        <button @click="nextPage" :disabled="currentPage === totalPages" class="page-btn">&raquo;</button>
        <span class="page-info">{{ routes.length }} {{ t('total') || 'total' }}</span>
      </div>
    </div>

    <!-- Modal Create/Edit Route -->
    <div v-if="showCreateModal || showEditModal" class="modal-overlay" @click.self="closeModals">
      <div class="modal">
        <h3>{{ showEditModal ? t('editRouteTitle') : t('createRouteTitle') }}</h3>
        <form @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>{{ t('routeNumber') }}:</label>
            <input v-model="currentRoute.routeNumber" required />
          </div>
          <div class="form-group">
            <label>{{ t('name') }}:</label>
            <input v-model="currentRoute.name" required />
          </div>
          <div class="form-group">
            <label>{{ t('colorHex') }}:</label>
            <input v-model="currentRoute.color" type="color" />
          </div>
          <div class="modal-actions">
            <button type="button" @click="closeModals" class="btn-secondary">{{ t('cancel') }}</button>
            <button type="submit" class="btn-primary">{{ showEditModal ? t('save') : t('create') }}</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Edit Color -->
    <div v-if="showColorModal" class="modal-overlay" @click.self="closeModals">
      <div class="modal modal-small">
        <h3>{{ t('changeColorTitle') }}</h3>
        <form @submit.prevent="handleColorUpdate">
          <div class="form-group">
            <label>{{ t('newColor') }}:</label>
            <input v-model="newColor" type="color" required />
          </div>
          <div class="modal-actions">
            <button type="button" @click="closeModals" class="btn-secondary">{{ t('cancel') }}</button>
            <button type="submit" class="btn-primary">{{ t('save') }}</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { adminRoutesService, type Route } from '@/services/adminService'
import { useLanguage } from '@/composables/useLanguage'
import { usePagination } from '@/composables/usePagination'

const { t } = useLanguage()

const routes = ref<Route[]>([])
const isLoading = ref(true)
const { paginatedItems: paginatedRoutes, currentPage, totalPages, visiblePages, goToPage, prevPage, nextPage } = usePagination(routes, 10)

const showCreateModal = ref(false)
const showEditModal = ref(false)
const showColorModal = ref(false)

const currentRoute = ref<Partial<Route>>({
  routeNumber: '',
  name: '',
  color: '#3b82f6'
})

const selectedRoute = ref<Route | null>(null)
const newColor = ref('#3b82f6')

const loadRoutes = async () => {
  isLoading.value = true
  console.log('📋 Loading routes...')
  try {
    routes.value = await adminRoutesService.getRoutes()
    console.log('✅ Routes loaded successfully:', routes.value.length, 'routes')
  } catch (error: any) {
    console.error('❌ Error loading routes:', error)
    console.error('Error details:', error.response?.data)
    const errorMsg = error.response?.data?.message || error.message || t('routesLoadError')
    alert(t('routesLoadErrorWithMessage', 'Error loading routes: {message}', { message: errorMsg }))
  } finally {
    isLoading.value = false
  }
}

const editRoute = (route: Route) => {
  currentRoute.value = { ...route }
  showEditModal.value = true
}

const editColor = (route: Route) => {
  selectedRoute.value = route
  newColor.value = route.color || '#3b82f6'
  showColorModal.value = true
}

const deleteRoute = async (route: Route) => {
  if (!confirm(t('confirmDeleteRoute', 'Are you sure you want to delete route {route}?', { route: route.routeNumber }))) return

  try {
    await adminRoutesService.deleteRoute(route.id)
    await loadRoutes()
  } catch (error) {
    console.error('Error deleting route:', error)
    alert(t('routeDeleteError'))
  }
}

const handleSubmit = async () => {
  try {
    if (showEditModal.value && currentRoute.value.id) {
      await adminRoutesService.updateRoute(currentRoute.value.id, currentRoute.value)
    } else {
      await adminRoutesService.createRoute(currentRoute.value as Omit<Route, 'id'>)
    }
    await loadRoutes()
    closeModals()
  } catch (error) {
    console.error('Error saving route:', error)
    alert(t('routeSaveError'))
  }
}

const handleColorUpdate = async () => {
  if (!selectedRoute.value) return

  try {
    await adminRoutesService.updateRouteColor(selectedRoute.value.id, newColor.value)
    await loadRoutes()
    closeModals()
  } catch (error) {
    console.error('Error updating color:', error)
    alert(t('routeColorUpdateError'))
  }
}

const closeModals = () => {
  showCreateModal.value = false
  showEditModal.value = false
  showColorModal.value = false
  currentRoute.value = { routeNumber: '', name: '', color: '#3b82f6' }
  selectedRoute.value = null
}

onMounted(() => {
  loadRoutes()
})
</script>

<style scoped>
.routes-management {
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
  background: #3b82f6;
  color: white;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-primary:hover {
  background: #2563eb;
}

.loading {
  text-align: center;
  padding: 40px;
  color: var(--text-secondary);
}

.routes-table-container {
  overflow-x: auto;
}

.routes-table {
  width: 100%;
  border-collapse: collapse;
}

.routes-table thead {
  background: var(--bg-secondary);
}

.routes-table th {
  padding: 12px 16px;
  text-align: left;
  font-weight: 600;
  color: var(--text-primary);
  border-bottom: 2px solid var(--border-color);
}

.routes-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color);
  color: var(--text-primary);
}

.color-display {
  display: flex;
  align-items: center;
  gap: 8px;
}

.color-box {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  border: 2px solid var(--border-color);
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

.btn-color {
  background: #8b5cf6;
}

.btn-color:hover {
  background: #7c3aed;
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

.modal-small {
  max-width: 400px;
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
  border-color: #3b82f6;
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

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  margin-top: 16px;
  padding: 12px 0;
}

.page-btn {
  min-width: 36px;
  height: 36px;
  border: 1px solid var(--border-color);
  background: var(--bg-primary);
  color: var(--text-primary);
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.15s;
}

.page-btn:hover:not(:disabled) {
  background: var(--bg-secondary);
}

.page-btn.active {
  background: #3b82f6;
  color: white;
  border-color: #3b82f6;
}

.page-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.page-info {
  margin-left: 12px;
  font-size: 13px;
  color: var(--text-secondary);
}
</style>
