<template>
  <div class="routes-management">
    <div class="page-header">
      <h2>Gestionare Trasee</h2>
      <button @click="showCreateModal = true" class="btn-primary">
        + Adaugă Traseu Nou
      </button>
    </div>

    <div v-if="isLoading" class="loading">Se încarcă...</div>

    <div v-else class="routes-table-container">
      <table class="routes-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Număr Traseu</th>
            <th>Nume</th>
            <th>Culoare</th>
            <th>Acțiuni</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="route in routes" :key="route.id">
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
                <button @click="editRoute(route)" class="btn-edit" title="Editează">
                  ✏️
                </button>
                <button @click="editColor(route)" class="btn-color" title="Schimbă culoare">
                  🎨
                </button>
                <button @click="deleteRoute(route)" class="btn-delete" title="Șterge">
                  🗑️
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Modal Create/Edit Route -->
    <div v-if="showCreateModal || showEditModal" class="modal-overlay" @click.self="closeModals">
      <div class="modal">
        <h3>{{ showEditModal ? 'Editează Traseu' : 'Adaugă Traseu Nou' }}</h3>
        <form @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>Număr Traseu:</label>
            <input v-model="currentRoute.routeNumber" required />
          </div>
          <div class="form-group">
            <label>Nume:</label>
            <input v-model="currentRoute.name" required />
          </div>
          <div class="form-group">
            <label>Culoare (hex):</label>
            <input v-model="currentRoute.color" type="color" />
          </div>
          <div class="modal-actions">
            <button type="button" @click="closeModals" class="btn-secondary">Anulează</button>
            <button type="submit" class="btn-primary">{{ showEditModal ? 'Salvează' : 'Creează' }}</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal Edit Color -->
    <div v-if="showColorModal" class="modal-overlay" @click.self="closeModals">
      <div class="modal modal-small">
        <h3>Schimbă Culoarea</h3>
        <form @submit.prevent="handleColorUpdate">
          <div class="form-group">
            <label>Culoare nouă:</label>
            <input v-model="newColor" type="color" required />
          </div>
          <div class="modal-actions">
            <button type="button" @click="closeModals" class="btn-secondary">Anulează</button>
            <button type="submit" class="btn-primary">Salvează</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { adminRoutesService, type Route } from '@/services/adminService'

const routes = ref<Route[]>([])
const isLoading = ref(true)

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
  try {
    routes.value = await adminRoutesService.getRoutes()
  } catch (error) {
    console.error('Error loading routes:', error)
    alert('Eroare la încărcarea traseelor')
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
  if (!confirm(`Sigur vrei să ștergi traseul ${route.routeNumber}?`)) return

  try {
    await adminRoutesService.deleteRoute(route.id)
    await loadRoutes()
  } catch (error) {
    console.error('Error deleting route:', error)
    alert('Eroare la ștergerea traseului')
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
    alert('Eroare la salvarea traseului')
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
    alert('Eroare la actualizarea culorii')
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
  background: white;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
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
  color: #000000;
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
  color: #6b7280;
}

.routes-table-container {
  overflow-x: auto;
}

.routes-table {
  width: 100%;
  border-collapse: collapse;
}

.routes-table thead {
  background: #f9fafb;
}

.routes-table th {
  padding: 12px 16px;
  text-align: left;
  font-weight: 600;
  color: #000000;
  border-bottom: 2px solid #e5e7eb;
}

.routes-table td {
  padding: 12px 16px;
  border-bottom: 1px solid #e5e7eb;
  color: #000000;
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
  border: 2px solid #e5e7eb;
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
  background: white;
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
  color: #000000;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  font-weight: 600;
  color: #000000;
}

.form-group input {
  width: 100%;
  padding: 10px 12px;
  border: 2px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
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
  background: #e5e7eb;
  color: #000000;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-secondary:hover {
  background: #d1d5db;
}
</style>
