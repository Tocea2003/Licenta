<script setup lang="ts">
import { computed } from 'vue'
import type { Ticket, TicketType } from '@/services/ticketsService'

const props = defineProps<{ ticket: Ticket }>()

const TICKET_META: Record<TicketType, { icon: string; name: string; sub: string }> = {
  single:             { icon: '🎫', name: 'Bilet intern 60 min',         sub: 'O călătorie — valabil 60 min' },
  daily:              { icon: '☀️', name: 'Legitimație zilnică',          sub: 'Călătorii nelimitate — 1 zi' },
  weekly:             { icon: '📅', name: 'Abonament 7 zile',            sub: 'Călătorii nelimitate — 7 zile' },
  monthly_nominal:    { icon: '🪪', name: 'Abonament nominal 30 zile',   sub: 'Călătorii nelimitate — 30 zile (nominal)' },
  monthly_nonnominal: { icon: '📋', name: 'Abonament nenominal 30 zile', sub: 'Călătorii nelimitate — 30 zile (nenominal)' },
}

const meta = computed(() => TICKET_META[props.ticket.ticketType] ?? TICKET_META.single)

function formatDateTime(iso: string): string {
  const d = new Date(iso)
  return d.toLocaleString('ro-RO', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  })
}

const computedStatus = computed(() => {
  if (props.ticket.status !== 'active') return props.ticket.status
  const now = new Date()
  const validUntil = new Date(props.ticket.validUntil)
  if (validUntil < now) return 'expired'
  return 'active'
})

const statusLabel = computed(() => {
  switch (computedStatus.value) {
    case 'active':  return 'Activ'
    case 'used':    return 'Folosit'
    case 'expired': return 'Expirat'
    default:        return computedStatus.value
  }
})
</script>

<template>
  <article class="ticket-card card" :class="`status-${computedStatus}`">
    <div class="ticket-head">
      <div class="ticket-kind">
        <span class="ticket-icon" aria-hidden="true">{{ meta.icon }}</span>
        <div>
          <h3 class="ticket-title">{{ meta.name }}</h3>
          <p class="ticket-sub">{{ meta.sub }}</p>
        </div>
      </div>
      <span class="badge status-badge">{{ statusLabel }}</span>
    </div>

    <div class="ticket-body">
      <div class="ticket-field">
        <span class="field-label">Pret</span>
        <span class="field-value">{{ ticket.priceRon.toFixed(2) }} RON</span>
      </div>
      <div v-if="ticket.ridesTotal" class="ticket-field">
        <span class="field-label">Calatorii incluse</span>
        <span class="field-value rides-value">{{ ticket.ridesTotal }} ×</span>
      </div>
      <div class="ticket-field">
        <span class="field-label">Cumparat</span>
        <span class="field-value">{{ formatDateTime(ticket.purchasedAt) }}</span>
      </div>
      <div class="ticket-field">
        <span class="field-label">Valabil pana la</span>
        <span class="field-value">{{ formatDateTime(ticket.validUntil) }}</span>
      </div>
      <div v-if="ticket.payment" class="ticket-field">
        <span class="field-label">Card</span>
        <span class="field-value">
          {{ ticket.payment.cardBrand === 'visa' ? 'Visa' : ticket.payment.cardBrand === 'mastercard' ? 'Mastercard' : 'Card' }}
          •••• {{ ticket.payment.cardLast4 }}
        </span>
      </div>
    </div>

    <div class="ticket-foot">
      <span class="qr-label">Cod validare</span>
      <code class="qr-token">{{ ticket.qrToken.slice(0, 12) }}…{{ ticket.qrToken.slice(-4) }}</code>
    </div>
  </article>
</template>

<style scoped>
.ticket-card {
  padding: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.ticket-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
  background: var(--gradient-primary-soft);
  border-bottom: 1px dashed var(--border-primary);
}

.ticket-card.status-expired .ticket-head,
.ticket-card.status-used .ticket-head {
  background: var(--bg-tertiary);
  filter: saturate(0.4);
}

.ticket-kind {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.ticket-icon {
  font-size: 28px;
}

.ticket-title {
  margin: 0;
  font-size: var(--text-lg);
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
}
.ticket-sub {
  margin: 2px 0 0 0;
  font-size: var(--text-xs);
  color: var(--text-secondary);
}

.status-badge {
  background: var(--color-success-soft);
  color: var(--color-success);
  padding: 4px 10px;
}
.ticket-card.status-expired .status-badge {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
.ticket-card.status-used .status-badge {
  background: var(--bg-tertiary);
  color: var(--text-secondary);
}

.ticket-body {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-3) var(--space-4);
  padding: var(--space-4) var(--space-5);
}

.ticket-field {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.field-label {
  font-size: var(--text-xs);
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.field-value {
  font-size: var(--text-sm);
  color: var(--text-primary);
  font-weight: var(--fw-medium);
}

.ticket-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-5);
  background: var(--bg-secondary);
  border-top: 1px dashed var(--border-primary);
}
.qr-label {
  font-size: var(--text-xs);
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.qr-token {
  font-family: 'SF Mono', Menlo, monospace;
  font-size: var(--text-xs);
  color: var(--text-secondary);
  background: var(--bg-primary);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  border: 1px solid var(--border-primary);
}

@media (max-width: 420px) {
  .ticket-body { grid-template-columns: 1fr; }
}
</style>
