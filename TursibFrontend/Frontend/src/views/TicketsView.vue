<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import ticketsService, { type Ticket } from '@/services/ticketsService'
import TicketCard from '@/components/TicketCard.vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const tickets = ref<Ticket[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

async function loadTickets() {
  loading.value = true
  error.value = null
  try {
    tickets.value = await ticketsService.getMyTickets()
  } catch (err: any) {
    error.value = err?.response?.data?.message || 'Nu s-au putut incarca biletele'
  } finally {
    loading.value = false
  }
}

onMounted(loadTickets)
</script>

<template>
  <div class="page page-narrow">
    <header class="page-header">
      <div>
        <h1 class="page-title">{{ t('myTickets') }}</h1>
        <p class="page-subtitle">Istoric cumparari + bilete active</p>
      </div>
      <RouterLink to="/tickets/checkout" class="btn btn-primary">
        + Cumpara bilet
      </RouterLink>
    </header>

    <div v-if="loading" class="loading-state">
      <div class="skeleton" style="height: 120px; margin-bottom: var(--space-3);"></div>
      <div class="skeleton" style="height: 120px;"></div>
    </div>

    <div v-else-if="error" class="alert alert-danger">
      {{ error }}
    </div>

    <div v-else-if="tickets.length === 0" class="empty-state card">
      <div class="empty-icon" aria-hidden="true">🎫</div>
      <h2 class="empty-title">{{ t('noTicketsYet') }}</h2>
      <p class="empty-sub">Cumpara un bilet simplu in cateva secunde, platesti cu cardul.</p>
      <RouterLink to="/tickets/checkout" class="btn btn-primary btn-lg">
        Cumpara primul bilet
      </RouterLink>
    </div>

    <div v-else class="tickets-list">
      <TicketCard v-for="t in tickets" :key="t.id" :ticket="t" />
    </div>
  </div>
</template>

<style scoped>
.tickets-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.empty-state {
  text-align: center;
  padding: var(--space-10) var(--space-6);
}
.empty-icon {
  font-size: 48px;
  margin-bottom: var(--space-3);
}
.empty-title {
  margin: 0 0 var(--space-2) 0;
  font-size: var(--text-xl);
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
}
.empty-sub {
  margin: 0 0 var(--space-6) 0;
  color: var(--text-secondary);
}

.loading-state {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}
</style>
