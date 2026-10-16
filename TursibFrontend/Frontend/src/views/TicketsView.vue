<template>
  <div class="tickets-page">
    <header class="tickets-header">
      <h1 class="page-title">{{ t('tickets') }}</h1>
      <p class="page-subtitle">{{ t('ticketsLead') }}</p>
    </header>

    <div v-if="loading" class="loading-state" aria-busy="true">
      <div class="skeleton" style="height: 220px"></div>
      <div class="skeleton" style="height: 160px"></div>
    </div>

    <div v-else-if="error" class="alert alert-danger" role="alert">{{ error }}</div>

    <template v-else>
      <!-- Biletul activ: QR + timp rămas -->
      <article v-if="activeTicket" class="active-ticket" :aria-label="t('activeTicket')">
        <div class="active-top">
          <span class="active-badge">{{ t('activeTicket') }}</span>
          <span class="active-type">{{ t(ticketOption(activeTicket.ticketType).nameKey) }}</span>
        </div>

        <div class="active-main">
          <img v-if="qrDataUrl" :src="qrDataUrl" :alt="t('ticketQrLabel')" class="active-qr" width="128" height="128" />
          <div v-else class="active-qr active-qr--placeholder" aria-hidden="true"></div>

          <div class="active-time">
            <span class="active-label">{{ t('validFor') }}</span>
            <span class="active-countdown" role="timer" aria-live="off">{{ countdown }}</span>
            <span class="active-label">{{ t('validUntilTime', '', { time: formatTime(activeTicket.validUntil) }) }}</span>
          </div>
        </div>

        <div class="active-progress" aria-hidden="true">
          <div class="active-progress-fill" :style="{ width: `${remainingPercent}%` }"></div>
        </div>

        <code class="active-code">{{ shortCode(activeTicket.qrToken) }}</code>
      </article>

      <!-- Cumpără -->
      <section class="tickets-section" :aria-label="t('buySection')">
        <h2 class="section-label">{{ t('buySection') }}</h2>
        <div class="buy-grid">
          <RouterLink
            v-for="option in TICKET_CATALOG"
            :key="option.id"
            :to="{ path: '/tickets/checkout', query: { type: option.id } }"
            class="buy-option"
          >
            <span class="buy-name">{{ t(option.nameKey) }}</span>
            <span class="buy-price">{{ formatPrice(option.price) }}</span>
            <span class="buy-desc">{{ t(option.descKey) }}</span>
          </RouterLink>
        </div>
        <p v-if="tickets.length === 0" class="first-hint">{{ t('firstTicketHint') }}</p>
      </section>

      <!-- Istoric -->
      <section v-if="pastTickets.length > 0" class="tickets-section" :aria-label="t('historySection')">
        <h2 class="section-label">{{ t('historySection') }}</h2>
        <ul class="history">
          <li v-for="ticket in pastTickets" :key="ticket.id">
            <details class="history-item">
              <summary>
                <span class="history-name">{{ t(ticketOption(ticket.ticketType).nameKey) }}</span>
                <span class="history-date">{{ formatDate(ticket.purchasedAt) }}</span>
                <span class="history-status" :class="`is-${effectiveTicketStatus(ticket)}`">{{ statusLabel(ticket) }}</span>
                <ChevronDown :size="16" class="history-chevron" aria-hidden="true" />
              </summary>
              <dl class="history-details">
                <div><dt>{{ t('price') }}</dt><dd>{{ formatPrice(ticket.priceRon) }}</dd></div>
                <div><dt>{{ t('purchased') }}</dt><dd>{{ formatDateTime(ticket.purchasedAt) }}</dd></div>
                <div><dt>{{ t('validUntil') }}</dt><dd>{{ formatDateTime(ticket.validUntil) }}</dd></div>
                <div v-if="ticket.payment"><dt>{{ t('card') }}</dt><dd>{{ cardLabel(ticket) }}</dd></div>
                <div><dt>{{ t('validationCode') }}</dt><dd><code>{{ shortCode(ticket.qrToken) }}</code></dd></div>
              </dl>
            </details>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import QRCode from 'qrcode'
import { ChevronDown } from 'lucide-vue-next'
import ticketsService, { type Ticket } from '@/services/ticketsService'
import { useLanguage } from '@/composables/useLanguage'
import { TICKET_CATALOG, ticketOption, formatPrice, effectiveTicketStatus } from '@/constants/ticketCatalog'

const { t } = useLanguage()

const tickets = ref<Ticket[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const now = ref(Date.now())
const qrDataUrl = ref<string | null>(null)

let clock: number | null = null

async function loadTickets() {
  loading.value = true
  error.value = null
  try {
    tickets.value = await ticketsService.getMyTickets()
  } catch (err: any) {
    error.value = err?.response?.data?.message || t('ticketsLoadError')
  } finally {
    loading.value = false
  }
}

// Biletul valabil care expiră ultimul (dacă sunt mai multe)
const activeTicket = computed(() =>
  tickets.value
    .filter(ticket => effectiveTicketStatus(ticket, new Date(now.value)) === 'active')
    .sort((a, b) => new Date(b.validUntil).getTime() - new Date(a.validUntil).getTime())[0] ?? null
)

const pastTickets = computed(() => tickets.value.filter(ticket => ticket.id !== activeTicket.value?.id))

const remainingMs = computed(() =>
  activeTicket.value ? Math.max(0, new Date(activeTicket.value.validUntil).getTime() - now.value) : 0
)

const remainingPercent = computed(() => {
  if (!activeTicket.value) return 0
  const total = new Date(activeTicket.value.validUntil).getTime() - new Date(activeTicket.value.validFrom).getTime()
  return total > 0 ? Math.round((remainingMs.value / total) * 100) : 0
})

// mm:ss sub o oră, altfel „Xh MMmin” sau „Xz Yh” pentru abonamente
const countdown = computed(() => {
  const totalSeconds = Math.floor(remainingMs.value / 1000)
  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  const pad = (n: number) => String(n).padStart(2, '0')
  if (days > 0) return `${days}z ${hours}h`
  if (hours > 0) return `${hours}h ${pad(minutes)}min`
  return `${pad(minutes)}:${pad(seconds)}`
})

watch(() => activeTicket.value?.qrToken, async token => {
  qrDataUrl.value = token
    ? await QRCode.toDataURL(token, { margin: 1, width: 256, color: { dark: '#16181d', light: '#ffffff' } })
    : null
}, { immediate: true })

function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('ro-RO', { day: 'numeric', month: 'short' })
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString('ro-RO', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

function shortCode(token: string) {
  return `${token.slice(0, 8)}…${token.slice(-4)}`
}

function statusLabel(ticket: Ticket) {
  const status = effectiveTicketStatus(ticket, new Date(now.value))
  return status === 'active' ? t('statusActive') : status === 'used' ? t('statusUsed') : t('statusExpired')
}

function cardLabel(ticket: Ticket) {
  const brand = ticket.payment?.cardBrand === 'visa' ? 'Visa' : ticket.payment?.cardBrand === 'mastercard' ? 'Mastercard' : 'Card'
  return `${brand} •••• ${ticket.payment?.cardLast4 ?? ''}`
}

onMounted(() => {
  loadTickets()
  clock = window.setInterval(() => { now.value = Date.now() }, 1000)
})

onUnmounted(() => {
  if (clock) clearInterval(clock)
})
</script>

<style scoped>
.tickets-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
  padding: 20px 16px 32px;
  font-variant-numeric: tabular-nums;
}

.tickets-header .page-subtitle {
  margin-top: 4px;
}

.loading-state {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* Biletul activ */
.active-ticket {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  border-radius: var(--radius-xl);
  background: #1e3fae;
  color: #fff;
}

.active-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.active-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: var(--radius-full);
  background: #fff;
  color: #1e3fae;
  font-size: 12px;
  font-weight: var(--fw-heavy);
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.active-badge::before {
  content: '';
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #1f7a4d;
}

.active-type {
  font-size: 14px;
  color: #d6ddf5;
}

.active-main {
  display: flex;
  align-items: center;
  gap: 16px;
}

.active-qr {
  width: 128px;
  height: 128px;
  flex-shrink: 0;
  border-radius: var(--radius-md);
  background: #fff;
  padding: 6px;
}

.active-time {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.active-label {
  font-size: 13px;
  color: #d6ddf5;
}

.active-countdown {
  font-family: var(--font-display);
  font-size: 40px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.02em;
  line-height: 1;
}

.active-progress {
  height: 6px;
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.25);
  overflow: hidden;
}

.active-progress-fill {
  height: 100%;
  border-radius: 3px;
  background: #fff;
  transition: width 1s linear;
}

.active-code {
  align-self: flex-start;
  font-size: 12px;
  color: #d6ddf5;
}

/* Secțiuni */
.tickets-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section-label {
  margin: 0;
  font-family: var(--font-sans);
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.buy-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.buy-option {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 14px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  color: var(--text-primary);
  text-decoration: none;
  transition: border-color 0.15s ease;
}

.buy-option:first-child {
  border: 2px solid var(--text-primary);
  padding: 13px;
}

.buy-option:hover {
  border-color: var(--border-secondary);
  color: var(--text-primary);
  text-decoration: none;
}

.buy-option:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

.buy-name {
  font-size: 15px;
  font-weight: var(--fw-bold);
}

.buy-price {
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: var(--fw-heavy);
}

.buy-desc {
  font-size: 12px;
  line-height: 1.4;
  color: var(--text-secondary);
}

.first-hint {
  margin: 0;
  font-size: 14px;
  color: var(--text-secondary);
}

/* Istoric */
.history {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.history-item {
  border-radius: var(--radius-md);
  background: var(--bg-primary);
}

.history-item summary {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 48px;
  padding: 0 14px;
  list-style: none;
  cursor: pointer;
  font-size: 14px;
}

.history-item summary::-webkit-details-marker {
  display: none;
}

.history-item summary:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
  border-radius: var(--radius-md);
}

.history-name {
  font-weight: var(--fw-semibold);
}

.history-date {
  color: var(--text-secondary);
}

.history-status {
  margin-left: auto;
  font-size: 13px;
  color: var(--text-secondary);
}

.history-status.is-active {
  color: var(--color-success);
  font-weight: var(--fw-bold);
}

.history-chevron {
  color: var(--text-tertiary);
  transition: transform 0.15s ease;
}

.history-item[open] .history-chevron {
  transform: rotate(180deg);
}

.history-details {
  margin: 0;
  padding: 4px 14px 14px;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 16px;
}

.history-details dt {
  font-size: 12px;
  color: var(--text-secondary);
}

.history-details dd {
  margin: 2px 0 0;
  font-size: 14px;
  font-weight: var(--fw-semibold);
  word-break: break-all;
}
</style>
