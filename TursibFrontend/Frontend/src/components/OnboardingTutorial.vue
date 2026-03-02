<template>
  <Teleport to="body">
    <Transition name="fade">
      <div v-if="show" class="onboarding-overlay" @click="handleSkip">
        <div class="onboarding-content" @click.stop>
          <!-- Progress Indicator -->
          <div class="progress-dots">
            <span
              v-for="(step, index) in steps"
              :key="index"
              class="dot"
              :class="{ active: index === currentStep }"
            ></span>
          </div>

          <!-- Current Step -->
          <Transition name="slide-fade" mode="out-in">
            <div v-if="currentStepData" :key="currentStep" class="step">
              <div class="step-icon">{{ currentStepData.icon }}</div>
              <h2>{{ currentStepData.title }}</h2>
              <p>{{ currentStepData.description }}</p>
              
              <!-- Illustration -->
              <div class="illustration">
                <component :is="currentStepData.illustration" />
              </div>
            </div>
          </Transition>

          <!-- Navigation -->
          <div class="navigation">
            <button
              v-if="currentStep > 0"
              @click="previousStep"
              class="btn-secondary"
            >
              Înapoi
            </button>
            <button
              v-else
              @click="handleSkip"
              class="btn-text"
            >
              Omite
            </button>

            <button
              @click="nextStep"
              class="btn-primary"
            >
              {{ currentStep === steps.length - 1 ? 'Începe' : 'Următorul' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, defineComponent, h, computed } from 'vue'
import { useOnboarding } from '@/composables/useOnboarding'

const props = defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  close: []
}>()

const { completeOnboarding } = useOnboarding()
const currentStep = ref(0)

// Onboarding steps
const steps = [
  {
    icon: '🚌',
    title: 'Bine ai venit la Tursib!',
    description: 'Aplicația ta de transport public din Sibiu. Hai să explorăm funcțiile principale.',
    illustration: defineComponent({
      render: () => h('div', { class: 'illust-bus' }, [
        h('div', { class: 'bus-body' }),
        h('div', { class: 'bus-window' }),
        h('div', { class: 'bus-wheel' }),
        h('div', { class: 'bus-wheel wheel-2' }),
      ])
    })
  },
  {
    icon: '🗺️',
    title: 'Harta Interactivă',
    description: 'Vezi autobuze în timp real, găsește stații și planifică rute ușor pe hartă.',
    illustration: defineComponent({
      render: () => h('div', { class: 'illust-map' }, [
        h('div', { class: 'map-pin' }),
        h('div', { class: 'map-route' }),
      ])
    })
  },
  {
    icon: '🔍',
    title: 'Căutare Inteligentă',
    description: 'Caută stații, adrese sau rute. Istoricul tău este salvat pentru acces rapid.',
    illustration: defineComponent({
      render: () => h('div', { class: 'illust-search' }, [
        h('div', { class: 'search-bar' }),
        h('div', { class: 'search-results' }),
      ])
    })
  },
  {
    icon: '⭐',
    title: 'Favorite și Notificări',
    description: 'Salvează locații favorite și primește notificări despre autobuzele tale.',
    illustration: defineComponent({
      render: () => h('div', { class: 'illust-favorites' }, [
        h('div', { class: 'fav-star' }),
        h('div', { class: 'fav-star star-2' }),
        h('div', { class: 'fav-star star-3' }),
      ])
    })
  },
  {
    icon: '🎨',
    title: 'Personalizare',
    description: 'Activează modul întunecat din setări pentru o experiență confortabilă.',
    illustration: defineComponent({
      render: () => h('div', { class: 'illust-theme' }, [
        h('div', { class: 'theme-sun' }),
        h('div', { class: 'theme-moon' }),
      ])
    })
  }
]

const currentStepData = computed(() => steps[currentStep.value])

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
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.85);
  backdrop-filter: blur(8px);
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}

.onboarding-content {
  background: var(--bg-primary);
  border-radius: 24px;
  padding: 40px 32px;
  max-width: 480px;
  width: 100%;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  position: relative;
}

/* Progress Dots */
.progress-dots {
  display: flex;
  justify-content: center;
  gap: 8px;
  margin-bottom: 32px;
}

.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--border-secondary);
  transition: all 0.3s;
}

.dot.active {
  background: #3b82f6;
  width: 24px;
  border-radius: 4px;
}

/* Step Content */
.step {
  text-align: center;
  min-height: 320px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.step-icon {
  font-size: 4rem;
  margin-bottom: 16px;
  animation: bounce 2s infinite;
}

@keyframes bounce {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}

.step h2 {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.step p {
  font-size: 1rem;
  color: var(--text-secondary);
  line-height: 1.6;
  margin: 0 0 32px 0;
}

/* Illustrations */
.illustration {
  width: 100%;
  height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 24px;
}

/* Bus Illustration */
.illust-bus {
  position: relative;
  width: 100px;
  height: 60px;
}

.bus-body {
  position: absolute;
  width: 100%;
  height: 80%;
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  border-radius: 12px;
  top: 0;
}

.bus-window {
  position: absolute;
  width: 30%;
  height: 40%;
  background: rgba(255, 255, 255, 0.9);
  border-radius: 4px;
  top: 8px;
  left: 35%;
}

.bus-wheel {
  position: absolute;
  width: 20px;
  height: 20px;
  background: #1e293b;
  border-radius: 50%;
  bottom: -8px;
  left: 15px;
  border: 3px solid #64748b;
}

.wheel-2 {
  left: 65px;
}

/* Map Illustration */
.illust-map {
  position: relative;
  width: 120px;
  height: 120px;
  background: linear-gradient(135deg, #f0f9ff, #e0f2fe);
  border-radius: 12px;
}

.map-pin {
  position: absolute;
  width: 30px;
  height: 40px;
  background: #ef4444;
  border-radius: 50% 50% 50% 0;
  transform: rotate(-45deg);
  top: 30px;
  left: 45px;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
}

.map-route {
  position: absolute;
  width: 80%;
  height: 3px;
  background: #3b82f6;
  top: 50%;
  left: 10%;
  border-radius: 2px;
}

/* Search Illustration */
.illust-search {
  display: flex;
  flex-direction: column;
  gap: 12px;
  width: 140px;
}

.search-bar {
  height: 40px;
  background: var(--bg-secondary);
  border-radius: 20px;
  border: 2px solid #3b82f6;
  position: relative;
}

.search-bar::after {
  content: '🔍';
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
}

.search-results {
  height: 60px;
  background: var(--bg-secondary);
  border-radius: 12px;
  border: 1px solid var(--border-primary);
}

/* Favorites Illustration */
.illust-favorites {
  position: relative;
  width: 120px;
  height: 120px;
}

.fav-star {
  position: absolute;
  font-size: 3rem;
  animation: pulse 2s infinite;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

.fav-star::before {
  content: '⭐';
}

.star-2 {
  font-size: 2rem;
  top: 20%;
  left: 20%;
  animation-delay: 0.3s;
}

.star-3 {
  font-size: 2rem;
  top: 70%;
  left: 70%;
  animation-delay: 0.6s;
}

@keyframes pulse {
  0%, 100% { opacity: 1; transform: translate(-50%, -50%) scale(1); }
  50% { opacity: 0.6; transform: translate(-50%, -50%) scale(1.1); }
}

/* Theme Illustration */
.illust-theme {
  display: flex;
  gap: 24px;
  align-items: center;
}

.theme-sun {
  width: 50px;
  height: 50px;
  background: linear-gradient(135deg, #fbbf24, #f59e0b);
  border-radius: 50%;
  box-shadow: 0 0 20px rgba(251, 191, 36, 0.5);
  animation: rotate 10s linear infinite;
}

.theme-moon {
  width: 50px;
  height: 50px;
  background: linear-gradient(135deg, #94a3b8, #64748b);
  border-radius: 50%;
  position: relative;
}

.theme-moon::after {
  content: '';
  position: absolute;
  width: 35px;
  height: 35px;
  background: var(--bg-primary);
  border-radius: 50%;
  top: 5px;
  left: 5px;
}

@keyframes rotate {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* Navigation */
.navigation {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-top: auto;
}

.btn-primary,
.btn-secondary,
.btn-text {
  padding: 12px 24px;
  border-radius: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
  font-size: 1rem;
}

.btn-primary {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  color: white;
  flex: 1;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 16px rgba(59, 130, 246, 0.3);
}

.btn-secondary {
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border-primary);
}

.btn-secondary:hover {
  background: var(--bg-tertiary);
}

.btn-text {
  background: transparent;
  color: var(--text-secondary);
}

.btn-text:hover {
  color: var(--text-primary);
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.slide-fade-enter-active {
  transition: all 0.3s ease-out;
}

.slide-fade-leave-active {
  transition: all 0.2s ease-in;
}

.slide-fade-enter-from {
  transform: translateX(20px);
  opacity: 0;
}

.slide-fade-leave-to {
  transform: translateX(-20px);
  opacity: 0;
}

/* Responsive */
@media (max-width: 640px) {
  .onboarding-content {
    padding: 32px 24px;
  }

  .step h2 {
    font-size: 1.5rem;
  }

  .step p {
    font-size: 0.95rem;
  }

  .step-icon {
    font-size: 3rem;
  }
}
</style>
