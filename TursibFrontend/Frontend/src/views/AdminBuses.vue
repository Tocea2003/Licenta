<template>
  <div class="buses-management">
    <div class="page-header">
      <h2>Flotă Autobuze</h2>
      <button @click="openCreate" class="btn-primary">+ Adaugă autobuz</button>
    </div>

    <!-- Summary cards -->
    <div class="summary-cards">
      <div class="summary-card">
        <div class="card-value">{{ buses.length }}</div>
        <div class="card-label">Total autobuze</div>
      </div>
      <div class="summary-card green">
        <div class="card-value">{{ assignedCount }}</div>
        <div class="card-label">Asignate pe traseu</div>
      </div>
      <div class="summary-card amber">
        <div class="card-value">{{ buses.length - assignedCount }}</div>
        <div class="card-label">În garaj</div>
      </div>
    </div>

    <div v-if="isLoading" class="loading">Se încarcă...</div>

    <div v-else class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Nr. înmatriculare</th>
            <th>Denumire internă</th>
            <th>Traseu curent</th>
            <th>Status</th>
            <th>Acțiuni</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="bus in buses" :key="bus.id">
            <td class="id-col">{{ bus.id }}</td>
            <td><strong>{{ bus.licensePlate }}</strong></td>
            <td>{{ bus.internalName }}</td>
            <td>
              <span v-if="bus.currentRouteNumber" class="route-chip">
                <span class="route-dot"></span>
                Linia {{ bus.currentRouteNumber }} — {{ bus.currentRouteName }}
              </span>
              <span v-else class="no-route">Neasignat</span>
            </td>
            <td>
              <span :class="['status-badge', bus.currentRouteId ? 'active' : 'garage']">
                {{ bus.currentRouteId ? 'În serviciu' : 'Garaj' }}
              </span>
            </td>
            <td>
              <div class="action-buttons">
                <button @click="openEdit(bus)" class="btn-icon btn-edit" title="Editează">✏️</button>
                <button @click="deleteBus(bus)" class="btn-icon btn-delete" title="Șterge">🗑️</button>
              </div>
            </td>
          </tr>
          <tr v-if="buses.length === 0">
            <td colspan="6" class="empty-row">Niciun autobuz înregistrat.</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Create / Edit modal -->
    <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <h3>{{ editingBus ? 'Editează autobuz' : 'Adaugă autobuz nou' }}</h3>
        <form @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>Nr. înmatriculare</label>
            <input v-model="form.licensePlate" required placeholder="ex: SB-01-ABC" />
          </div>
          <div class="form-group">
            <label>Denumire internă</label>
            <input v-model="form.internalName" required placeholder="ex: Bus 045" />
          </div>
          <div class="form-group">
            <label>Traseu asignat</label>
            <select v-model="form.currentRouteId">
              <option :value="null">— Neasignat (garaj) —</option>
              <option v-for="route in routes" :key="route.id" :value="route.id">
                Linia {{ route.routeNumber }} — {{ route.name }}
              </option>
            </select>
          </div>
          <div class="modal-actions">
            <button type="button" @click="closeModal" class="btn-secondary">Anulează</button>
            <button type="submit" class="btn-primary">{{ editingBus ? 'Salvează' : 'Creează' }}</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { adminBusesService, adminRoutesService, type Bus, type Route } from '@/services/adminService'

const buses = ref<Bus[]>([])
const routes = ref<Route[]>([])
const isLoading = ref(true)
const showModal = ref(false)
const editingBus = ref<Bus | null>(null)

const form = ref({
  licensePlate: '',
  internalName: '',
  currentRouteId: null as number | null
})

const assignedCount = computed(() => buses.value.filter(b => b.currentRouteId !== null).length)

const loadData = async () => {
  isLoading.value = true
  try {
    const [busData, routeData] = await Promise.all([
      adminBusesService.getBuses(),
      adminRoutesService.getRoutes()
    ])
    buses.value = busData
    routes.value = routeData
  } catch {
    alert('Eroare la încărcarea datelor.')
  } finally {
    isLoading.value = false
  }
}

const openCreate = () => {
  editingBus.value = null
  form.value = { licensePlate: '', internalName: '', currentRouteId: null }
  showModal.value = true
}

const openEdit = (bus: Bus) => {
  editingBus.value = bus
  form.value = { licensePlate: bus.licensePlate, internalName: bus.internalName, currentRouteId: bus.currentRouteId }
  showModal.value = true
}

const closeModal = () => {
  showModal.value = false
  editingBus.value = null
}

const handleSubmit = async () => {
  try {
    if (editingBus.value) {
      await adminBusesService.updateBus(editingBus.value.id, { id: editingBus.value.id, ...form.value })
    } else {
      await adminBusesService.createBus(form.value)
    }
    await loadData()
    closeModal()
  } catch {
    alert('Eroare la salvarea autobuzului.')
  }
}

const deleteBus = async (bus: Bus) => {
  if (!confirm(`Ștergi autobuzul "${bus.licensePlate}" (${bus.internalName})?`)) return
  try {
    await adminBusesService.deleteBus(bus.id)
    await loadData()
  } catch {
    alert('Eroare la ștergerea autobuzului.')
  }
}

onMounted(loadData)
</script>

<style scoped>
.buses-management {
  background: var(--bg-primary);
  border-radius: 12px;
  padding: 24px;
  box-shadow: var(--shadow-sm);
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
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

.btn-primary:hover { background: #2563eb; }

.summary-cards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.summary-card {
  background: var(--bg-secondary);
  border-radius: 10px;
  padding: 20px;
  text-align: center;
  border-left: 4px solid #667eea;
}

.summary-card.green { border-left-color: #10b981; }
.summary-card.amber { border-left-color: #f59e0b; }

.card-value {
  font-size: 32px;
  font-weight: 800;
  color: var(--text-primary);
  line-height: 1;
}

.card-label {
  font-size: 13px;
  color: var(--text-secondary);
  margin-top: 6px;
  font-weight: 500;
}

.loading {
  text-align: center;
  padding: 40px;
  color: var(--text-secondary);
}

.table-container { overflow-x: auto; }

.data-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table thead { background: var(--bg-secondary); }

.data-table th {
  padding: 12px 16px;
  text-align: left;
  font-weight: 600;
  color: var(--text-primary);
  border-bottom: 2px solid var(--border-color);
  font-size: 13px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.data-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color);
  color: var(--text-primary);
  font-size: 14px;
}

.id-col { color: var(--text-secondary); font-size: 12px; width: 50px; }

.route-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
}

.route-dot {
  width: 6px;
  height: 6px;
  background: #3b82f6;
  border-radius: 50%;
}

.no-route {
  color: var(--text-secondary);
  font-size: 13px;
  font-style: italic;
}

.status-badge {
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 700;
}

.status-badge.active {
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
}

.status-badge.garage {
  background: rgba(148, 163, 184, 0.15);
  color: #94a3b8;
}

.action-buttons { display: flex; gap: 8px; }

.btn-icon {
  padding: 6px 10px;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 15px;
  transition: all 0.2s;
}

.btn-edit { background: rgba(251, 191, 36, 0.15); }
.btn-edit:hover { background: #fbbf24; }
.btn-delete { background: rgba(239, 68, 68, 0.12); }
.btn-delete:hover { background: #ef4444; }

.empty-row { text-align: center; color: var(--text-secondary); padding: 40px; }

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--bg-primary);
  border-radius: 12px;
  padding: 28px;
  width: 100%;
  max-width: 460px;
}

.modal h3 {
  margin: 0 0 20px;
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
}

.form-group { margin-bottom: 16px; }

.form-group label {
  display: block;
  margin-bottom: 6px;
  font-weight: 600;
  color: var(--text-primary);
  font-size: 13px;
}

.form-group input,
.form-group select {
  width: 100%;
  padding: 10px 12px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.form-group input:focus,
.form-group select:focus {
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
}

.btn-secondary:hover { background: var(--bg-tertiary); }
</style>
