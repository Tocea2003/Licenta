<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import FormField from './FormField.vue'

export interface CardData {
  cardholderName: string
  cardNumber: string
  expiryMonth: string
  expiryYear: string
  cvv: string
}

const emit = defineEmits<{
  (e: 'update', payload: { data: CardData; valid: boolean }): void
}>()

const form = reactive<CardData>({
  cardholderName: '',
  cardNumber: '',
  expiryMonth: '',
  expiryYear: '',
  cvv: ''
})

const touched = reactive({
  cardholderName: false,
  cardNumber: false,
  expiry: false,
  cvv: false
})

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '')
}

function formatCardNumber(value: string): string {
  const d = digitsOnly(value).slice(0, 19)
  return d.replace(/(\d{4})(?=\d)/g, '$1 ').trim()
}

function luhn(cardNumber: string): boolean {
  const d = digitsOnly(cardNumber)
  if (d.length < 13 || d.length > 19) return false
  let sum = 0
  let alt = false
  for (let i = d.length - 1; i >= 0; i--) {
    let n = parseInt(d.charAt(i), 10)
    if (alt) { n *= 2; if (n > 9) n -= 9 }
    sum += n
    alt = !alt
  }
  return sum % 10 === 0
}

function detectBrand(cardNumber: string): 'visa' | 'mastercard' | 'unknown' {
  const d = digitsOnly(cardNumber)
  if (!d) return 'unknown'
  if (d[0] === '4') return 'visa'
  if (d.length >= 2) {
    const p2 = parseInt(d.substring(0, 2), 10)
    if (p2 >= 51 && p2 <= 55) return 'mastercard'
  }
  if (d.length >= 4) {
    const p4 = parseInt(d.substring(0, 4), 10)
    if (p4 >= 2221 && p4 <= 2720) return 'mastercard'
  }
  return 'unknown'
}

const brand = computed(() => detectBrand(form.cardNumber))

const errors = computed(() => {
  const e: Record<string, string | undefined> = {}
  if (touched.cardholderName && form.cardholderName.trim().length < 2) {
    e.cardholderName = 'Introdu numele detinatorului'
  }
  const digits = digitsOnly(form.cardNumber)
  if (touched.cardNumber) {
    if (digits.length < 13) e.cardNumber = 'Numar card incomplet'
    else if (!luhn(form.cardNumber)) e.cardNumber = 'Numar card invalid'
  }
  if (touched.expiry) {
    const m = parseInt(form.expiryMonth, 10)
    const y = parseInt(form.expiryYear, 10)
    if (!m || m < 1 || m > 12) e.expiry = 'Luna invalida'
    else if (!y || y < 2000 || y > 2099) e.expiry = 'An invalid'
    else {
      const last = new Date(y, m, 0, 23, 59, 59)
      if (last < new Date()) e.expiry = 'Card expirat'
    }
  }
  if (touched.cvv && !/^\d{3,4}$/.test(form.cvv)) e.cvv = 'CVV invalid (3-4 cifre)'
  return e
})

const isValid = computed(() => {
  return (
    form.cardholderName.trim().length >= 2 &&
    luhn(form.cardNumber) &&
    /^\d{1,2}$/.test(form.expiryMonth) &&
    /^\d{4}$/.test(form.expiryYear) &&
    /^\d{3,4}$/.test(form.cvv) &&
    !errors.value.expiry
  )
})

watch(
  () => ({ ...form }),
  () => {
    emit('update', {
      data: {
        cardholderName: form.cardholderName.trim(),
        cardNumber: digitsOnly(form.cardNumber),
        expiryMonth: form.expiryMonth.padStart(2, '0'),
        expiryYear: form.expiryYear,
        cvv: form.cvv
      },
      valid: isValid.value
    })
  },
  { deep: true }
)

function onCardNumberInput(event: Event) {
  const target = event.target as HTMLInputElement
  form.cardNumber = formatCardNumber(target.value)
}

function fillTestCard() {
  form.cardholderName = 'Test User'
  form.cardNumber = formatCardNumber('4242424242424242')
  form.expiryMonth = '12'
  form.expiryYear = '2030'
  form.cvv = '123'
  touched.cardholderName = true
  touched.cardNumber = true
  touched.expiry = true
  touched.cvv = true
}
</script>

<template>
  <div class="card-form">
    <FormField label="Nume detinator card" :error="errors.cardholderName" required>
      <input
        v-model="form.cardholderName"
        class="input"
        :class="{ 'has-error': errors.cardholderName }"
        type="text"
        autocomplete="cc-name"
        placeholder="ex. Ion Popescu"
        @blur="touched.cardholderName = true"
      />
    </FormField>

    <FormField label="Numar card" :error="errors.cardNumber" required>
      <div class="card-number-wrap">
        <input
          :value="form.cardNumber"
          class="input card-number-input"
          :class="{ 'has-error': errors.cardNumber }"
          type="text"
          inputmode="numeric"
          autocomplete="cc-number"
          placeholder="1234 5678 9012 3456"
          maxlength="23"
          @input="onCardNumberInput"
          @blur="touched.cardNumber = true"
        />
        <span class="card-brand" :class="`brand-${brand}`" aria-hidden="true">
          <span v-if="brand === 'visa'">VISA</span>
          <span v-else-if="brand === 'mastercard'">MC</span>
          <span v-else>💳</span>
        </span>
      </div>
    </FormField>

    <div class="card-row">
      <FormField label="Luna" :error="errors.expiry && touched.expiry ? '' : undefined" required>
        <input
          v-model="form.expiryMonth"
          class="input"
          :class="{ 'has-error': !!errors.expiry }"
          type="text"
          inputmode="numeric"
          autocomplete="cc-exp-month"
          placeholder="LL"
          maxlength="2"
          @blur="touched.expiry = true"
        />
      </FormField>
      <FormField label="An" :error="errors.expiry" required>
        <input
          v-model="form.expiryYear"
          class="input"
          :class="{ 'has-error': !!errors.expiry }"
          type="text"
          inputmode="numeric"
          autocomplete="cc-exp-year"
          placeholder="AAAA"
          maxlength="4"
          @blur="touched.expiry = true"
        />
      </FormField>
      <FormField label="CVV" :error="errors.cvv" required>
        <input
          v-model="form.cvv"
          class="input"
          :class="{ 'has-error': errors.cvv }"
          type="text"
          inputmode="numeric"
          autocomplete="cc-csc"
          placeholder="123"
          maxlength="4"
          @blur="touched.cvv = true"
        />
      </FormField>
    </div>

    <div class="test-hint">
      <div class="alert alert-info" style="font-size: var(--text-xs)">
        <span>💡 Poti folosi cardul de test <strong>4242 4242 4242 4242</strong> cu orice CVV si o data de expirare in viitor.</span>
      </div>
      <button type="button" class="btn btn-ghost btn-test" @click="fillTestCard">
        Completeaza automat
      </button>
    </div>
  </div>
</template>

<style scoped>
.card-form {
  display: flex;
  flex-direction: column;
}

.card-row {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: var(--space-3);
}

.card-number-wrap {
  position: relative;
}
.card-number-input {
  padding-right: 64px;
  font-variant-numeric: tabular-nums;
  letter-spacing: 0.02em;
}
.card-brand {
  position: absolute;
  right: var(--space-3);
  top: 50%;
  transform: translateY(-50%);
  font-weight: var(--fw-bold);
  font-size: var(--text-xs);
  letter-spacing: 0.06em;
  color: var(--text-tertiary);
}
.card-brand.brand-visa { color: #1a1f71; }
.card-brand.brand-mastercard { color: #eb001b; }

.test-hint {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin-top: var(--space-2);
}
.btn-test {
  align-self: flex-start;
  font-size: var(--text-xs);
  min-height: 36px;
  padding: var(--space-1) var(--space-3);
}

@media (max-width: 520px) {
  .card-row { grid-template-columns: 1fr 1fr; }
  .card-row > :nth-child(3) { grid-column: span 2; }
}
</style>
