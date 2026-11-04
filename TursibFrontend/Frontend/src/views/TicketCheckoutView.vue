<template>
  <div class="checkout-page">
    <header>
      <h1 class="page-title">{{ t('buyTicket') }}</h1>
      <p class="page-subtitle">{{ t('payWithCardSubtitle') }}</p>
    </header>

    <ol class="steps" :aria-label="t('buyTicket')">
      <li :class="{ active: step === 1, done: step > 1 }" :aria-current="step === 1 ? 'step' : undefined">
        <span class="step-num"><Check v-if="step > 1" :size="14" aria-hidden="true" /><template v-else>1</template></span>{{ t('ticket') }}
      </li>
      <li :class="{ active: step === 2, done: step > 2 }" :aria-current="step === 2 ? 'step' : undefined">
        <span class="step-num"><Check v-if="step > 2" :size="14" aria-hidden="true" /><template v-else>2</template></span>{{ t('payment') }}
      </li>
      <li :class="{ active: step === 3 }" :aria-current="step === 3 ? 'step' : undefined">
        <span class="step-num">3</span>{{ t('confirmation') }}
      </li>
    </ol>

    <!-- 1. Tipul biletului -->
    <section v-if="step === 1" class="checkout-section">
      <h2 class="section-label">{{ t('chooseTicketType') }}</h2>
      <div class="options" role="radiogroup" :aria-label="t('chooseTicketType')">
        <button
          v-for="opt in TICKET_CATALOG"
          :key="opt.id"
          type="button"
          role="radio"
          :aria-checked="selectedType === opt.id"
          class="option"
          :class="{ selected: selectedType === opt.id }"
          @click="selectedType = opt.id"
        >
          <span class="option-main">
            <span class="option-name">{{ t(opt.nameKey) }}</span>
            <span class="option-desc">{{ t(opt.descKey) }}</span>
          </span>
          <span class="option-price">{{ formatPrice(opt.price) }}</span>
        </button>
      </div>
      <button type="button" class="btn btn-primary btn-lg" @click="goToStep2">
        {{ t('continueToPayment', '', { price: formatPrice(selectedOption.price) }) }}
      </button>
    </section>

    <!-- 2. Datele cardului -->
    <section v-if="step === 2" class="checkout-section">
      <div class="order-summary">
        <span class="order-name">{{ t(selectedOption.nameKey) }}</span>
        <span class="order-price">{{ formatPrice(selectedOption.price) }}</span>
        <button type="button" class="link-btn" :disabled="submitting" @click="step = 1">{{ t('back') }}</button>
      </div>

      <h2 class="section-label">{{ t('cardDetails') }}</h2>
      <CardInputForm @update="onCardUpdate" />

      <div v-if="serverError" class="alert alert-danger" role="alert">{{ serverError }}</div>

      <button
        type="button"
        class="btn btn-primary btn-lg"
        :disabled="!cardValid || submitting"
        @click="submitPayment"
      >
        <Lock :size="18" aria-hidden="true" />
        {{ submitting ? t('processing') : t('payAmount', '', { price: formatPrice(selectedOption.price) }) }}
      </button>
    </section>

    <!-- 3. Confirmare -->
    <section v-if="step === 3 && purchased" class="checkout-section success" role="status">
      <span class="success-icon" aria-hidden="true"><Check :size="28" /></span>
      <h2 class="success-title">{{ t('paymentApproved') }}</h2>
      <p class="success-sub">{{ t('ticketIssued') }}</p>

      <dl class="summary">
        <div><dt>{{ t('ticket') }}</dt><dd>{{ t(selectedOption.nameKey) }}</dd></div>
        <div><dt>{{ t('pricePaid') }}</dt><dd>{{ formatPrice(purchased.priceRon) }}</dd></div>
        <div v-if="purchased.ridesTotal"><dt>{{ t('ridesIncluded') }}</dt><dd>{{ purchased.ridesTotal }} {{ t('rides') }}</dd></div>
        <div><dt>{{ t('validUntil') }}</dt><dd>{{ formatDateTime(purchased.validUntil) }}</dd></div>
        <div v-if="purchased.payment">
          <dt>{{ t('card') }}</dt>
          <dd>{{ purchased.payment.cardBrand === 'visa' ? 'Visa' : purchased.payment.cardBrand === 'mastercard' ? 'Mastercard' : 'Card' }} •••• {{ purchased.payment.cardLast4 }}</dd>
        </div>
      </dl>

      <div class="success-actions">
        <button type="button" class="btn btn-primary btn-lg" @click="router.push('/tickets')">{{ t('viewMyTickets') }}</button>
        <button type="button" class="btn btn-secondary btn-lg" @click="router.push('/')">{{ t('backToMap') }}</button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import CardInputForm, { type CardData } from '@/components/CardInputForm.vue'
import ticketsService, { type Ticket, type TicketType } from '@/services/ticketsService'
import { useLanguage } from '@/composables/useLanguage'
import { Check, Lock } from 'lucide-vue-next'
import { TICKET_CATALOG, ticketOption, formatPrice } from '@/constants/ticketCatalog'

const { t } = useLanguage()
const router = useRouter()
const route = useRoute()

type Step = 1 | 2 | 3
const step = ref<Step>(TICKET_CATALOG.some(o => o.id === route.query.type) ? 2 : 1)
const submitting = ref(false)
const serverError = ref<string | null>(null)
const purchased = ref<Ticket | null>(null)

// Tipul ales pe pagina de bilete (?type=...) sare direct la plată
const typeFromQuery = TICKET_CATALOG.find(o => o.id === route.query.type)?.id
const selectedType = ref<TicketType>(typeFromQuery ?? 'single')
const cardData = ref<CardData>({
  cardholderName: '',
  cardNumber: '',
  expiryMonth: '',
  expiryYear: '',
  cvv: ''
})
const cardValid = ref(false)

const selectedOption = computed(() => ticketOption(selectedType.value))

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
      serverError.value = msg || t('paymentDeclined')
    } else if (status === 401) {
      serverError.value = t('sessionExpiredLogin')
      setTimeout(() => router.push('/login'), 1500)
    } else {
      serverError.value = msg || t('paymentGenericError')
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

<style scoped>
.checkout-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px 16px 32px;
  font-variant-numeric: tabular-nums;
}

.page-subtitle {
  margin-top: 4px;
}

.steps {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 6px;
}

.steps li {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-top: 10px;
  border-top: 3px solid var(--border-primary);
  font-size: 13px;
  font-weight: var(--fw-bold);
  color: var(--text-secondary);
}

.steps li.active {
  border-top-color: var(--text-primary);
  color: var(--text-primary);
}

.steps li.done {
  border-top-color: var(--color-success);
  color: var(--color-success);
}

.step-num {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-tertiary);
  font-size: 12px;
}

.steps li.active .step-num {
  background: var(--bg-inverse);
  color: var(--text-inverse);
}

.steps li.done .step-num {
  background: var(--color-success);
  color: #fff;
}

.checkout-section {
  display: flex;
  flex-direction: column;
  gap: 14px;
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

.options {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.option {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  min-height: 64px;
  padding: 12px 14px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.option:hover {
  border-color: var(--border-secondary);
}

.option.selected {
  border: 2px solid var(--text-primary);
  padding: 11px 13px;
}

.option:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

.option-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.option-name {
  font-size: 15px;
  font-weight: var(--fw-bold);
}

.option-desc {
  font-size: 13px;
  color: var(--text-secondary);
}

.option-price {
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: var(--fw-heavy);
  white-space: nowrap;
}

.order-summary {
  display: flex;
  align-items: baseline;
  gap: 10px;
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
}

.order-name {
  font-weight: var(--fw-bold);
}

.order-price {
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: var(--fw-heavy);
}

.link-btn {
  margin-left: auto;
  min-height: 36px;
  border: none;
  background: none;
  color: var(--accent-primary);
  font: inherit;
  font-size: 14px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.success {
  align-items: stretch;
  text-align: center;
}

.success-icon {
  align-self: center;
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background: var(--color-success-soft);
  color: var(--color-success);
  display: flex;
  align-items: center;
  justify-content: center;
}

.success-title {
  margin: 0;
  font-size: 24px;
  font-weight: var(--fw-heavy);
}

.success-sub {
  margin: 0;
  color: var(--text-secondary);
}

.summary {
  margin: 0;
  padding: 16px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px 16px;
  text-align: left;
}

.summary dt {
  font-size: 12px;
  color: var(--text-secondary);
}

.summary dd {
  margin: 2px 0 0;
  font-weight: var(--fw-semibold);
}

.success-actions {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>
