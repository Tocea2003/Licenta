<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import CardInputForm, { type CardData } from '@/components/CardInputForm.vue'
import ticketsService, { type Ticket, type TicketType } from '@/services/ticketsService'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()
const router = useRouter()

type Step = 1 | 2 | 3
const step = ref<Step>(1)
const submitting = ref(false)
const serverError = ref<string | null>(null)
const purchased = ref<Ticket | null>(null)

const selectedType = ref<TicketType>('single')
const cardData = ref<CardData>({
  cardholderName: '',
  cardNumber: '',
  expiryMonth: '',
  expiryYear: '',
  cvv: ''
})
const cardValid = ref(false)

// Tarife Tursib Sibiu în vigoare de la 1 aprilie 2025
const ticketOptions: { id: TicketType; icon: string; name: string; desc: string; price: number; badge?: string }[] = [
  {
    id: 'single',
    icon: '🎫',
    name: 'Bilet intern 60 min',
    desc: 'O călătorie, valabil 60 de minute',
    price: 3.50
  },
  {
    id: 'daily',
    icon: '☀️',
    name: 'Legitimație zilnică',
    desc: 'Călătorii nelimitate, 1 zi calendaristică',
    price: 7.00
  },
  {
    id: 'weekly',
    icon: '📅',
    name: 'Abonament 7 zile',
    desc: 'Călătorii nelimitate, 7 zile calendaristice',
    price: 24.00
  },
  {
    id: 'monthly_nominal',
    icon: '🪪',
    name: 'Abonament nominal 30 zile',
    desc: 'Călătorii nelimitate, 30 zile — pe numele tău',
    price: 90.00,
    badge: 'Nominal'
  },
  {
    id: 'monthly_nonnominal',
    icon: '📋',
    name: 'Abonament nenominal 30 zile',
    desc: 'Călătorii nelimitate, 30 zile — transferabil',
    price: 126.00,
    badge: 'Nenominal'
  },
]

const selectedOption = computed(() => ticketOptions.find(o => o.id === selectedType.value)!)

function goToStep2() {
  step.value = 2
}

async function submitPayment() {
  if (!cardValid.value) return
  submitting.value = true
  serverError.value = null
  try {
    const ticket = await ticketsService.purchase({
      ticketType: selectedType.value,
      cardNumber: cardData.value.cardNumber,
      expiryMonth: cardData.value.expiryMonth,
      expiryYear: cardData.value.expiryYear,
      cvv: cardData.value.cvv,
      cardholderName: cardData.value.cardholderName
    })
    purchased.value = ticket
    step.value = 3
  } catch (err: any) {
    const status = err?.response?.status
    const msg = err?.response?.data?.message
    if (status === 402) {
      serverError.value = msg || 'Plata a fost refuzata'
    } else if (status === 401) {
      serverError.value = 'Sesiune expirata. Te rugam sa te autentifici din nou.'
      setTimeout(() => router.push('/login'), 1500)
    } else {
      serverError.value = msg || 'A aparut o eroare la procesarea platii'
    }
  } finally {
    submitting.value = false
  }
}

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('ro-RO', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  })
}

function onCardUpdate(payload: { data: CardData; valid: boolean }) {
  cardData.value = payload.data
  cardValid.value = payload.valid
}
</script>

<template>
  <div class="page page-narrow">
    <header class="page-header">
      <div>
        <h1 class="page-title">{{ t('buyTicket') }}</h1>
        <p class="page-subtitle">{{ t('payWithCardSubtitle') }}</p>
      </div>
    </header>

    <nav class="stepper" aria-label="Pasi checkout">
      <div class="stepper-item" :class="{ active: step === 1, done: step > 1 }">
        <span class="stepper-num">{{ step > 1 ? '✓' : '1' }}</span>
        <span>{{ t('ticket') }}</span>
      </div>
      <div class="stepper-item" :class="{ active: step === 2, done: step > 2 }">
        <span class="stepper-num">{{ step > 2 ? '✓' : '2' }}</span>
        <span>{{ t('payment') }}</span>
      </div>
      <div class="stepper-item" :class="{ active: step === 3 }">
        <span class="stepper-num">3</span>
        <span>{{ t('confirmation') }}</span>
      </div>
    </nav>

    <!-- STEP 1 — select ticket -->
    <section v-if="step === 1" class="card checkout-section">
      <h2 class="section-title">{{ t('chooseTicketType') }}</h2>
      <div class="ticket-options">
        <button
          v-for="opt in ticketOptions"
          :key="opt.id"
          type="button"
          class="chip ticket-chip"
          :class="{ selected: selectedType === opt.id }"
          @click="selectedType = opt.id"
        >
          <span class="chip-icon" aria-hidden="true">{{ opt.icon }}</span>
          <div class="chip-main">
            <div class="chip-name-row">
              <strong>{{ opt.name }}</strong>
              <span v-if="opt.badge" class="chip-badge">{{ opt.badge }}</span>
            </div>
            <span class="chip-desc">{{ opt.desc }}</span>
          </div>
          <span class="chip-price">{{ opt.price.toFixed(2) }} RON</span>
        </button>
      </div>
      <div class="section-actions">
        <button type="button" class="btn btn-primary btn-lg" @click="goToStep2">
          Continua la plata · {{ selectedOption.price.toFixed(2) }} RON
        </button>
      </div>
    </section>

    <!-- STEP 2 — card details -->
    <section v-if="step === 2" class="card checkout-section">
      <h2 class="section-title">{{ t('cardDetails') }}</h2>
      <div class="order-summary">
        <span>{{ selectedOption.name }}</span>
        <strong>{{ selectedOption.price.toFixed(2) }} RON</strong>
      </div>

      <CardInputForm @update="onCardUpdate" />

      <div v-if="serverError" class="alert alert-danger" role="alert">
        {{ serverError }}
      </div>

      <div class="section-actions">
        <button type="button" class="btn btn-ghost" :disabled="submitting" @click="step = 1">
          ← Inapoi
        </button>
        <button
          type="button"
          class="btn btn-primary btn-lg"
          :disabled="!cardValid || submitting"
          @click="submitPayment"
        >
          {{ submitting ? t('processing') : `${t('payment')} ${selectedOption.price.toFixed(2)} RON` }}
        </button>
      </div>
    </section>

    <!-- STEP 3 — confirmation -->
    <section v-if="step === 3 && purchased" class="card checkout-section success-section">
      <div class="success-icon" aria-hidden="true">✓</div>
      <h2 class="section-title success-title">Plata aprobata!</h2>
      <p class="success-sub">Biletul tau a fost emis cu succes.</p>

      <div class="summary-grid">
        <div>
          <span class="field-label">{{ t('ticket') }}</span>
          <span class="field-value">{{ selectedOption.icon }} {{ selectedOption.name }}</span>
        </div>
        <div>
          <span class="field-label">{{ t('pricePaid') }}</span>
          <span class="field-value">{{ purchased.priceRon.toFixed(2) }} RON</span>
        </div>
        <div v-if="purchased.ridesTotal">
          <span class="field-label">{{ t('ridesIncluded') }}</span>
          <span class="field-value">{{ purchased.ridesTotal }} {{ t('rides') }}</span>
        </div>
        <div>
          <span class="field-label">{{ t('validUntil') }}</span>
          <span class="field-value">{{ formatDateTime(purchased.validUntil) }}</span>
        </div>
        <div v-if="purchased.payment">
          <span class="field-label">{{ t('card') }}</span>
          <span class="field-value">
            {{ purchased.payment.cardBrand === 'visa' ? 'Visa' : purchased.payment.cardBrand === 'mastercard' ? 'Mastercard' : 'Card' }}
            •••• {{ purchased.payment.cardLast4 }}
          </span>
        </div>
      </div>

      <div class="qr-box">
        <span class="qr-label">{{ t('validationCode') }}</span>
        <code class="qr-token">{{ purchased.qrToken }}</code>
      </div>

      <div class="section-actions">
        <button type="button" class="btn btn-outline" @click="router.push('/tickets')">
          Vezi biletele mele
        </button>
        <button type="button" class="btn btn-primary" @click="router.push('/')">
          Inapoi la harta
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.checkout-section {
  padding: var(--space-5);
}

.section-title {
  font-size: var(--text-lg);
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
  margin: 0 0 var(--space-4) 0;
}

.ticket-options {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-bottom: var(--space-5);
}

.ticket-chip {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
  border-radius: var(--radius-md);
  min-height: 64px;
  width: 100%;
  text-align: left;
}
.chip-icon {
  font-size: 24px;
  flex-shrink: 0;
}
.chip-main {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex: 1;
}
.chip-name-row {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}
.chip-badge {
  font-size: 10px;
  font-weight: var(--fw-bold);
  padding: 2px 7px;
  border-radius: 99px;
  background: var(--color-success-soft);
  color: var(--color-success);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.chip-desc {
  font-size: var(--text-xs);
  color: var(--text-tertiary);
  font-weight: var(--fw-normal);
}
.chip-price {
  font-size: var(--text-lg);
  font-weight: var(--fw-bold);
  flex-shrink: 0;
}

.section-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  margin-top: var(--space-5);
}
.section-actions .btn-primary {
  min-width: 220px;
}

.order-summary {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: var(--bg-secondary);
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  padding: var(--space-3) var(--space-4);
  margin-bottom: var(--space-4);
  font-size: var(--text-sm);
}
.order-summary strong {
  font-size: var(--text-md);
}

.success-section {
  text-align: center;
  padding: var(--space-8) var(--space-5);
}
.success-icon {
  width: 72px;
  height: 72px;
  border-radius: var(--radius-full);
  background: var(--gradient-success);
  color: #fff;
  font-size: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto var(--space-4) auto;
  box-shadow: var(--shadow-lg);
}
.success-title {
  margin-bottom: var(--space-2);
}
.success-sub {
  color: var(--text-secondary);
  margin: 0 0 var(--space-6) 0;
}

.summary-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-3) var(--space-4);
  background: var(--bg-secondary);
  border-radius: var(--radius-md);
  padding: var(--space-4);
  margin-bottom: var(--space-4);
  text-align: left;
}
.summary-grid > div {
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
  font-weight: var(--fw-medium);
  color: var(--text-primary);
}

.qr-box {
  border: 2px dashed var(--border-secondary);
  padding: var(--space-4);
  border-radius: var(--radius-md);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-4);
}
.qr-label {
  font-size: var(--text-xs);
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.qr-token {
  font-family: 'SF Mono', Menlo, monospace;
  font-size: var(--text-sm);
  color: var(--text-primary);
  word-break: break-all;
  text-align: center;
}

@media (max-width: 520px) {
  .summary-grid { grid-template-columns: 1fr; }
  .section-actions { flex-direction: column-reverse; }
  .section-actions .btn { width: 100%; }
}

/* Telefoane foarte mici: ascunde textul din stepper, afișează doar numere */
@media (max-width: 360px) {
  .stepper-item span:not(.stepper-num) {
    display: none;
  }
  .stepper-item {
    justify-content: center;
    padding: var(--space-2);
    flex: none;
    width: 40px;
  }
  .stepper {
    justify-content: center;
    gap: var(--space-2);
  }
}
</style>
