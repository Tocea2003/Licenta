<template>
  <Teleport to="body">
    <Transition name="fade">
      <div v-if="show" class="onboarding-overlay" @click="handleSkip">
        <div
          class="onboarding-card"
          role="dialog"
          aria-modal="true"
          :aria-label="t(currentStepData.titleKey)"
          @click.stop
        >
          <div class="progress" aria-hidden="true">
            <span v-for="(_, index) in steps" :key="index" class="progress-bar" :class="{ active: index <= currentStep }"></span>
          </div>
          <span class="step-count">{{ t('onboardStepOf', '', { n: currentStep + 1, total: steps.length }) }}</span>

          <Transition name="slide-fade" mode="out-in">
            <div :key="currentStep" class="step">
              <span class="step-icon" aria-hidden="true">
                <component :is="currentStepData.icon" :size="32" />
              </span>
              <h2 class="step-title">{{ t(currentStepData.titleKey) }}</h2>
              <p class="step-text">{{ t(currentStepData.textKey) }}</p>
            </div>
          </Transition>

          <div class="navigation">
            <button v-if="currentStep > 0" type="button" class="btn btn-secondary" @click="previousStep">{{ t('back') }}</button>
            <button v-else type="button" class="btn btn-ghost" @click="handleSkip">{{ t('onboardSkip') }}</button>
            <button type="button" class="btn btn-primary" @click="nextStep">
              {{ currentStep === steps.length - 1 ? t('onboardStart') : t('onboardNext') }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Bell, BusFront, Map, Route, Ticket } from 'lucide-vue-next'
import { useOnboarding } from '@/composables/useOnboarding'
import { useLanguage } from '@/composables/useLanguage'

defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  close: []
}>()

const { completeOnboarding } = useOnboarding()
const { t } = useLanguage()
const currentStep = ref(0)

const steps = [
  { icon: BusFront, titleKey: 'onboardWelcomeTitle', textKey: 'onboardWelcomeText' },
  { icon: Map, titleKey: 'onboardMapTitle', textKey: 'onboardMapText' },
  { icon: Route, titleKey: 'onboardPlanTitle', textKey: 'onboardPlanText' },
  { icon: Bell, titleKey: 'onboardAlertsTitle', textKey: 'onboardAlertsText' },
  { icon: Ticket, titleKey: 'onboardTicketsTitle', textKey: 'onboardTicketsText' }
]

const currentStepData = computed(() => steps[currentStep.value] ?? steps[0]!)

const nextStep = () => {
  if (currentStep.value < steps.length - 1) {
    currentStep.value++
  } else {
    finishOnboarding()
  }
}

const previousStep = () => {
  if (currentStep.value > 0) {
    currentStep.value--
  }
}

const handleSkip = () => {
  finishOnboarding()
}

const finishOnboarding = () => {
  completeOnboarding()
  emit('close')
}
</script>

<style scoped>
.onboarding-overlay {
  position: fixed;
  inset: 0;
  z-index: 3000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: var(--bg-overlay);
}

.onboarding-card {
  width: 100%;
  max-width: 400px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 24px;
  border-radius: var(--radius-xl);
  background: var(--bg-primary);
  color: var(--text-primary);
  box-shadow: var(--shadow-xl);
  font-family: var(--font-sans);
}

.progress {
  display: grid;
  grid-auto-flow: column;
  gap: 4px;
}

.progress-bar {
  height: 4px;
  border-radius: 2px;
  background: var(--bg-tertiary);
}

.progress-bar.active {
  background: var(--text-primary);
}

.step-count {
  font-size: 12px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.step {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 210px;
}

.step-icon {
  width: 64px;
  height: 64px;
  border-radius: 16px;
  background: var(--bg-inverse);
  color: var(--text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
}

.step-title {
  margin: 4px 0 0;
  font-size: 26px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.02em;
}

.step-text {
  margin: 0;
  font-size: 16px;
  line-height: 1.55;
  color: var(--text-secondary);
}

.navigation {
  display: flex;
  gap: 8px;
}

.navigation .btn {
  flex: 1;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.slide-fade-enter-active,
.slide-fade-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}

.slide-fade-enter-from {
  opacity: 0;
  transform: translateX(12px);
}

.slide-fade-leave-to {
  opacity: 0;
  transform: translateX(-12px);
}
</style>
