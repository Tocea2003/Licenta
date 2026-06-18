<template>
  <div class="tickets-management">
    <div class="page-header">
      <h2>{{ t('ticketsAndRevenue') }}</h2>
    </div>

    <!-- Revenue KPI cards -->
    <div class="kpi-cards" v-if="!isLoading">
      <div class="kpi-card">
        <div class="kpi-icon">🎫</div>
        <div class="kpi-body">
          <div class="kpi-value">{{ tickets.length }}</div>
          <div class="kpi-label">{{ t('totalTicketsSold') }}</div>
        </div>
      </div>
      <div class="kpi-card green">
        <div class="kpi-icon">💰</div>
        <div class="kpi-body">
          <div class="kpi-value">{{ totalRevenue.toFixed(2) }} RON</div>
          <div class="kpi-label">{{ t('totalRevenueLabel') }}</div>
        </div>
      </div>
      <div class="kpi-card blue">
        <div class="kpi-icon">✅</div>
        <div class="kpi-body">
          <div class="kpi-value">{{ activeCount }}</div>
          <div class="kpi-label">{{ t('activeTicketsLabel') }}</div>
        </div>
      </div>
      <div class="kpi-card amber">
        <div class="kpi-icon">📅</div>
        <div class="kpi-body">
          <div class="kpi-value">{{ todayCount }}</div>
          <div class="kpi-label">{{ t('soldToday') }}</div>
        </div>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-row">
      <input v-model="searchQuery" class="search-input" :placeholder="t('searchByUser')" />
      <select v-model="statusFilter" class="filter-select">
        <option value="">{{ t('allStatuses') }}</option>
        <option value="active">{{ t('statusActive') }}</option>
        <option value="expired">{{ t('statusExpired') }}</option>
        <option value="used">{{ t('statusUsed') }}</option>
      </select>
    </div>

    <div v-if="isLoading" class="loading">{{ t('loading') }}</div>

    <div v-else class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>{{ t('userCol') }}</th>
            <th>{{ t('typeCol') }}</th>
            <th>{{ t('price') }}</th>
            <th>{{ t('statusCol') }}</th>
            <th>{{ t('purchasedAt') }}</th>
            <th>{{ t('validUntil') }}</th>
            <th>{{ t('card') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ticket in paginatedTickets" :key="ticket.id">
            <td class="id-col">#{{ ticket.id }}</td>
            <td>
              <span class="username-chip">{{ ticket.username || `User #${ticket.userId}` }}</span>
            </td>
            <td>
              <span class="type-badge">{{ ticket.ticketType }}</span>
            </td>
            <td class="price-col">{{ ticket.priceRon.toFixed(2) }} RON</td>
            <td>
              <span :class="['status-badge', ticket.status]">
                {{ statusLabel(ticket.status) }}
              </span>
            </td>
            <td class="date-col">{{ formatDateTime(ticket.purchasedAt) }}</td>
            <td class="date-col">{{ formatDateTime(ticket.validUntil) }}</td>
            <td class="card-col">
              <span v-if="ticket.payment" class="card-info">
                {{ ticket.payment.cardBrand }} ····{{ ticket.payment.cardLast4 }}
              </span>
              <span v-else class="no-data">—</span>
            </td>
          </tr>
          <tr v-if="filteredTickets.length === 0">
            <td colspan="8" class="empty-row">{{ t('noTicketsFound') }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="totalPages > 1" class="pagination">
        <button @click="prevPage" :disabled="currentPage === 1" class="page-btn">&laquo;</button>
        <button v-for="p in visiblePages" :key="p" @click="goToPage(p)" :class="['page-btn', { active: p === currentPage }]">{{ p }}</button>
        <button @click="nextPage" :disabled="currentPage === totalPages" class="page-btn">&raquo;</button>
        <span class="page-info">{{ filteredTickets.length }} / {{ tickets.length }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { adminTicketsService, type AdminTicket } from '@/services/adminService'
import { usePagination } from '@/composables/usePagination'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const tickets = ref<AdminTicket[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const statusFilter = ref('')

const totalRevenue = computed(() =>
  tickets.value
    .filter(t => t.payment?.status === 'succeeded')
    .reduce((sum, t) => sum + t.priceRon, 0)
)

const activeCount = computed(() => tickets.value.filter(t => t.status === 'active').length)

const todayCount = computed(() => {
  const today = new Date().toDateString()
  return tickets.value.filter(t => new Date(t.purchasedAt).toDateString() === today).length
})

const filteredTickets = computed(() =>
  tickets.value.filter(t => {
    const matchesSearch = !searchQuery.value ||
      (t.username ?? '').toLowerCase().includes(searchQuery.value.toLowerCase())
    const matchesStatus = !statusFilter.value || t.status === statusFilter.value
    return matchesSearch && matchesStatus
  })
)

const { paginatedItems: paginatedTickets, currentPage, totalPages, visiblePages, goToPage, prevPage, nextPage, resetPage } = usePagination(filteredTickets, 10)
watch([searchQuery, statusFilter], () => resetPage())

const statusLabel = (status: string) => {
  const map: Record<string, string> = { active: 'statusActive', expired: 'statusExpired', used: 'statusUsed' }
  return t(map[status] ?? status)
}

const formatDateTime = (iso: string) => {
  if (!iso) return '—'
  return new Date(iso).toLocaleString('ro-RO', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  })
}

const loadTickets = async () => {
  isLoading.value = true
  try {
    tickets.value = await adminTicketsService.getTickets()
  } catch {
    alert(t('ticketsLoadError'))
  } finally {
    isLoading.value = false
  }
}

onMounted(loadTickets)
</script>

<style scoped>
.tickets-management {
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

.kpi-cards {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.kpi-card {
  background: var(--bg-secondary);
  border-radius: 10px;
  padding: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  border-left: 4px solid #667eea;
}

.kpi-card.green { border-left-color: #10b981; }
.kpi-card.blue  { border-left-color: #3b82f6; }
.kpi-card.amber { border-left-color: #f59e0b; }

.kpi-icon { font-size: 28px; }

.kpi-value {
  font-size: 22px;
  font-weight: 800;
  color: var(--text-primary);
  line-height: 1.1;
}

.kpi-label {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 4px;
  font-weight: 500;
}

.filters-row {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

.search-input,
.filter-select {
  padding: 10px 14px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.search-input { flex: 1; }
.search-input:focus,
.filter-select:focus { outline: none; border-color: #667eea; }

.loading { text-align: center; padding: 40px; color: var(--text-secondary); }

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
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  white-space: nowrap;
}

.data-table td {
  padding: 11px 16px;
  border-bottom: 1px solid var(--border-color);
  color: var(--text-primary);
  font-size: 13px;
}

.id-col { color: var(--text-secondary); font-size: 12px; }
.price-col { font-weight: 700; color: #10b981; }
.date-col { color: var(--text-secondary); white-space: nowrap; }
.card-col { color: var(--text-secondary); }

.username-chip {
  padding: 3px 8px;
  background: rgba(102, 126, 234, 0.1);
  color: #667eea;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
}

.type-badge {
  padding: 3px 8px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  text-transform: capitalize;
}

.status-badge {
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 700;
}

.status-badge.active  { background: rgba(16, 185, 129, 0.12); color: #10b981; }
.status-badge.expired { background: rgba(148, 163, 184, 0.15); color: #94a3b8; }
.status-badge.used    { background: rgba(251, 191, 36, 0.12);  color: #d97706; }

.card-info { font-size: 12px; }
.no-data { color: var(--text-secondary); }

.empty-row {
  text-align: center;
  color: var(--text-secondary);
  padding: 40px;
}

@media (max-width: 1100px) {
  .kpi-cards { grid-template-columns: repeat(2, 1fr); }
}

.pagination { display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 16px; padding: 12px 0; }
.page-btn { min-width: 36px; height: 36px; border: 1px solid var(--border-color); background: var(--bg-primary); color: var(--text-primary); border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 500; transition: all 0.15s; }
.page-btn:hover:not(:disabled) { background: var(--bg-secondary); }
.page-btn.active { background: #3b82f6; color: white; border-color: #3b82f6; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.page-info { margin-left: 12px; font-size: 13px; color: var(--text-secondary); }
</style>
