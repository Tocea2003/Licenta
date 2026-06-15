<template>
  <div class="buses-management">
    <div class="page-header">
      <h2>Flotă Autobuze</h2>
      <div class="header-actions">
        <div class="tab-switcher">
          <button :class="['tab-btn', { active: activeTab === 'live' }]" @click="activeTab = 'live'">
            <span class="tab-dot live-dot"></span>
            Live ({{ liveBuses.length }})
          </button>
          <button :class="['tab-btn', { active: activeTab === 'registered' }]" @click="activeTab = 'registered'">
            Înregistrate ({{ buses.length }})
          </button>
        </div>
        <button v-if="activeTab === 'registered'" @click="openCreate" class="btn-primary">+ Adaugă autobuz</button>
      </div>
    </div>

    <!-- Summary cards -->
    <div class="summary-cards">
      <div class="summary-card">
        <div class="card-value">{{ liveBuses.length }}</div>
        <div class="card-label">Autobuze active</div>
      </div>
      <div class="summary-card green">
        <div class="card-value">{{ liveRouteCount }}</div>
        <div class="card-label">Trasee deservite</div>
      </div>
      <div class="summary-card amber">
        <div class="card-value">{{ liveAvgSpeed }} km/h</div>
        <div class="card-label">Viteză medie</div>
      </div>
    </div>

    <div v-if="isLoading" class="loading">Se încarcă...</div>

    <!-- Live buses tab -->
    <div v-else-if="activeTab === 'live'" class="table-container">
      <div class="live-header">
        <input v-model="liveSearch" class="search-input" placeholder="Caută după traseu sau ID..." />
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>ID Bus</th>
            <th>Traseu</th>
            <th>Direcție</th>
            <th>Viteză</th>
            <th>Ocupare</th>
            <th>Stația următoare</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="bus in paginatedLiveBuses" :key="bus.id">
            <td class="id-col">{{ bus.shortId }}</td>
            <td><span class="route-chip"><span class="route-dot"></span> Linia {{ bus.routeNumber }}</span></td>
            <td>{{ bus.routeName || '—' }}</td>
            <td>{{ bus.speed.toFixed(0) }} km/h</td>
            <td>
              <div class="occupancy-bar-cell">
                <div class="occupancy-bar">
                  <div class="occupancy-fill" :style="{ width: bus.occupancy + '%', background: occupancyColor(bus.occupancy) }"></div>
                </div>
                <span class="occupancy-text">{{ bus.occupancy }}%</span>
              </div>
            </td>
            <td>{{ bus.nextStationName || '—' }}</td>
            <td><span class="status-badge active">Activ</span></td>
          </tr>
          <tr v-if="filteredLiveBuses.length === 0">
            <td colspan="7" class="empty-row">
              {{ liveSearch ? 'Niciun rezultat.' : 'Niciun autobuz activ momentan.' }}
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="liveTotalPages > 1" class="pagination">
        <button @click="liveCurrentPage > 1 && liveCurrentPage--" :disabled="liveCurrentPage === 1" class="page-btn">&laquo;</button>
        <button v-for="p in liveTotalPages" :key="p" @click="liveCurrentPage = p" :class="['page-btn', { active: p === liveCurrentPage }]">{{ p }}</button>
        <button @click="liveCurrentPage < liveTotalPages && liveCurrentPage++" :disabled="liveCurrentPage === liveTotalPages" class="page-btn">&raquo;</button>
        <span class="page-info">{{ filteredLiveBuses.length }} autobuze</span>
      </div>
    </div>

    <!-- Registered buses tab -->
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
          <tr v-for="bus in paginatedBuses" :key="bus.id">
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

      <div v-if="totalPages > 1" class="pagination">
        <button @click="prevPage" :disabled="currentPage === 1" class="page-btn">&laquo;</button>
        <button v-for="p in visiblePages" :key="p" @click="goToPage(p)" :class="['page-btn', { active: p === currentPage }]">{{ p }}</button>
        <button @click="nextPage" :disabled="currentPage === totalPages" class="page-btn">&raquo;</button>
        <span class="page-info">{{ buses.length }} total</span>
      </div>
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
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { adminBusesService, adminRoutesService, type Bus, type Route } from '@/services/adminService'
import { usePagination } from '@/composables/usePagination'
import { database } from '@/main'
import { ref as dbRef, onValue, off } from 'firebase/database'

interface LiveBus {
  id: string
  shortId: string
  routeNumber: string
  routeName: string
  latitude: number
  longitude: number
  speed: number
  heading: number
  occupancy: number
  nextStationName: string
  status: string
}

const activeTab = ref<'live' | 'registered'>('live')
const buses = ref<Bus[]>([])
const { paginatedItems: paginatedBuses, currentPage, totalPages, visiblePages, goToPage, prevPage, nextPage } = usePagination(buses, 10)
const routes = ref<Route[]>([])
const isLoading = ref(true)
const showModal = ref(false)
const editingBus = ref<Bus | null>(null)

const liveBuses = ref<LiveBus[]>([])
const liveSearch = ref('')
const liveCurrentPage = ref(1)
const LIVE_PER_PAGE = 15

const filteredLiveBuses = computed(() => {
  if (!liveSearch.value) return liveBuses.value
  const q = liveSearch.value.toLowerCase()
  return liveBuses.value.filter(b =>
    b.routeNumber.toLowerCase().includes(q) ||
    b.shortId.toLowerCase().includes(q) ||
    (b.routeName && b.routeName.toLowerCase().includes(q))
  )
})

const liveTotalPages = computed(() => Math.max(1, Math.ceil(filteredLiveBuses.value.length / LIVE_PER_PAGE)))

const paginatedLiveBuses = computed(() => {
  const start = (liveCurrentPage.value - 1) * LIVE_PER_PAGE
  return filteredLiveBuses.value.slice(start, start + LIVE_PER_PAGE)
})

const liveRouteCount = computed(() => {
  const uniqueRoutes = new Set(liveBuses.value.map(b => b.routeNumber))
  return uniqueRoutes.size
})

const liveAvgSpeed = computed(() => {
  if (liveBuses.value.length === 0) return 0
  const total = liveBuses.value.reduce((sum, b) => sum + b.speed, 0)
  return Math.round(total / liveBuses.value.length)
})

const occupancyColor = (value: number) => {
  if (value < 40) return '#10b981'
  if (value < 70) return '#f59e0b'
  return '#ef4444'
}

const form = ref({
  licensePlate: '',
  internalName: '',
  currentRouteId: null as number | null
})

const assignedCount = computed(() => buses.value.filter(b => b.currentRouteId !== null).length)

let busesRef: ReturnType<typeof dbRef> | null = null

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

const setupFirebase = () => {
  busesRef = dbRef(database, 'bus_locations')
  onValue(busesRef, (snapshot) => {
    const data = snapshot.val()
    const parsed: LiveBus[] = []
    if (data) {
      for (const [id, busData] of Object.entries(data)) {
        if (!id.startsWith('route_')) continue
        const bd = busData as any
        if (!bd?.latitude || !bd?.longitude) continue
        const parts = id.replace('route_', '').split('_bus_')
        parsed.push({
          id,
          shortId: parts.length === 2 ? `Bus #${parts[1]}` : id,
          routeNumber: bd.routeNumber || String(bd.routeId) || 'N/A',
          routeName: bd.routeName || '',
          latitude: bd.latitude,
          longitude: bd.longitude,
          speed: bd.speed || 0,
          heading: bd.heading || 0,
          occupancy: bd.occupancy || 0,
          nextStationName: bd.nextStationName || '',
          status: bd.status || 'active'
        })
      }
    }
    parsed.sort((a, b) => {
      const rA = parseInt(a.routeNumber) || 0
      const rB = parseInt(b.routeNumber) || 0
      if (rA !== rB) return rA - rB
      return a.shortId.localeCompare(b.shortId)
    })
    liveBuses.value = parsed
  })
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

onMounted(() => {
  loadData()
  setupFirebase()
})

onUnmounted(() => {
  if (busesRef) off(busesRef)
})
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
  flex-wrap: wrap;
  gap: 12px;
}

.page-header h2 {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.tab-switcher {
  display: flex;
  background: var(--bg-secondary);
  border-radius: 10px;
  padding: 3px;
}

.tab-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--text-secondary);
  font-weight: 600;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}

.tab-btn.active {
  background: var(--bg-primary);
  color: var(--text-primary);
  box-shadow: 0 1px 3px rgba(0,0,0,0.12);
}

.tab-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.live-dot {
  background: #10b981;
  animation: pulse-dot 2s infinite;
}

@keyframes pulse-dot {
  0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.5); }
  50% { opacity: 0.8; box-shadow: 0 0 0 4px rgba(16, 185, 129, 0); }
}

.live-header {
  margin-bottom: 16px;
}

.search-input {
  width: 100%;
  max-width: 320px;
  padding: 10px 14px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  transition: border-color 0.2s;
}

.search-input:focus {
  outline: none;
  border-color: #3b82f6;
}

.occupancy-bar-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.occupancy-bar {
  width: 60px;
  height: 8px;
  background: var(--bg-secondary);
  border-radius: 4px;
  overflow: hidden;
}

.occupancy-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.3s;
}

.occupancy-text {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  min-width: 32px;
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

.pagination { display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 16px; padding: 12px 0; }
.page-btn { min-width: 36px; height: 36px; border: 1px solid var(--border-color); background: var(--bg-primary); color: var(--text-primary); border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 500; transition: all 0.15s; }
.page-btn:hover:not(:disabled) { background: var(--bg-secondary); }
.page-btn.active { background: #3b82f6; color: white; border-color: #3b82f6; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.page-info { margin-left: 12px; font-size: 13px; color: var(--text-secondary); }
</style>
