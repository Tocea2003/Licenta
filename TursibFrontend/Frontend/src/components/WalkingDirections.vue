<template>
  <section v-if="visible" class="trip-panel walking-panel" :aria-label="t('walkingDirections')">
    <header class="trip-panel__header">
      <Footprints :size="18" aria-hidden="true" />
      <h3 class="trip-panel__title">{{ t('walkingDirections') }}</h3>
      <button type="button" class="trip-panel__close" :aria-label="t('close')" @click="emit('close')">
        <X :size="20" aria-hidden="true" />
      </button>
    </header>

    <div class="trip-panel__body" aria-live="polite">
      <div v-if="loading" class="walking-state">
        <LoaderCircle :size="28" class="walking-spinner" aria-hidden="true" />
        <p>{{ t('calculatingRoute') }}</p>
      </div>

      <div v-else-if="error" class="walking-state walking-state--error">
        <TriangleAlert :size="28" aria-hidden="true" />
        <p>{{ error }}</p>
        <button type="button" class="walking-retry" @click="calculate">{{ t('retry') }}</button>
      </div>

      <template v-else-if="route">
        <div class="trip-summary">
          <div class="trip-summary__times">
            {{ formatMinutes(route.durationS) }}
          </div>
          <div class="trip-summary__meta">
            <span>{{ t('distanceLabel') }}: <strong>{{ formatDistance(route.distanceM) }}</strong></span>
            <span>{{ startName || t('yourLocation') }} → {{ endName || t('destination') }}</span>
          </div>
        </div>

        <p v-if="route.approximate" class="walking-note">
          <Info :size="16" aria-hidden="true" />
          {{ t('approximateWalkingRoute') }}
        </p>

        <ol class="trip-steps">
          <li
            v-for="(step, index) in route.steps"
            :key="index"
            class="trip-step trip-step--walk"
            :class="{ 'walking-step--arrive': step.kind === 'arrive' }"
          >
            <div class="trip-step__rail">
              <span class="trip-step__icon">
                <component :is="iconFor(step.kind)" :size="16" aria-hidden="true" />
              </span>
            </div>
            <div class="trip-step__content">
              <div class="trip-step__head">
                <span class="walking-step__text">{{ instructionFor(step) }}</span>
              </div>
              <div v-if="step.distanceM >= 1" class="trip-step__sub">
                {{ formatDistance(step.distanceM) }} · {{ formatMinutes(step.durationS) }}
              </div>
            </div>
          </li>
        </ol>
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onBeforeUnmount, ref, watch, type Component } from 'vue'
import {
  ArrowUp,
  ArrowUpLeft,
  ArrowUpRight,
  CornerUpLeft,
  CornerUpRight,
  Flag,
  Footprints,
  Info,
  LoaderCircle,
  RotateCw,
  TriangleAlert,
  Undo2,
  X,
} from 'lucide-vue-next'
import { useLanguage } from '@/composables/useLanguage'
import { fetchWalkingRoute, type ManeuverKind, type WalkingRoute, type WalkingStep } from '@/services/valhallaService'
import '@/assets/trip-panel.css'

const props = withDefaults(defineProps<{
  visible: boolean
  startLat?: number
  startLon?: number
  endLat?: number
  endLon?: number
  startName?: string
  endName?: string
}>(), {
  startName: '',
  endName: '',
})

const emit = defineEmits<{
  close: []
  routeCalculated: [geometry: [number, number][]]
  snappedCoordinates: [start: { lat: number; lon: number }, end: { lat: number; lon: number }]
}>()

const { t, currentLanguage } = useLanguage()

const loading = ref(false)
const error = ref<string | null>(null)
const route = ref<WalkingRoute | null>(null)
let controller: AbortController | null = null

const ICONS: Record<ManeuverKind, Component> = {
  depart: Footprints,
  arrive: Flag,
  straight: ArrowUp,
  'slight-left': ArrowUpLeft,
  left: CornerUpLeft,
  'sharp-left': CornerUpLeft,
  'slight-right': ArrowUpRight,
  right: CornerUpRight,
  'sharp-right': CornerUpRight,
  uturn: Undo2,
  roundabout: RotateCw,
}

const iconFor = (kind: ManeuverKind) => ICONS[kind]

const instructionFor = (step: WalkingStep) => {
  if (step.instruction) return step.instruction
  const name = props.endName || t('destination')
  return step.kind === 'arrive' ? t('arrivedAt', name, { name }) : t('walkStraightTo', name, { name })
}

const formatDistance = (meters: number) =>
  meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${Math.round(meters)} m`

const formatMinutes = (seconds: number) => `${Math.max(1, Math.round(seconds / 60))} min`

async function calculate() {
  const { startLat, startLon, endLat, endLon } = props
  if (startLat == null || startLon == null || endLat == null || endLon == null) {
    error.value = t('missingCoordinates')
    return
  }

  controller?.abort()
  const current = new AbortController()
  controller = current
  loading.value = true
  error.value = null

  const start = { lat: startLat, lon: startLon }
  const end = { lat: endLat, lon: endLon }
  try {
    const result = await fetchWalkingRoute(start, end, currentLanguage.value, current.signal)
    route.value = result
    emit('routeCalculated', result.geometry)
    emit('snappedCoordinates', start, end)
  } catch {
    if (!current.signal.aborted) error.value = t('missingCoordinates')
  } finally {
    if (controller === current) loading.value = false
  }
}

watch(
  () => [props.visible, props.startLat, props.startLon, props.endLat, props.endLon, currentLanguage.value] as const,
  ([visible]) => {
    if (visible) calculate()
    else controller?.abort()
  },
  { immediate: true },
)

onBeforeUnmount(() => controller?.abort())
</script>

<style scoped>
/* Layout comes from assets/trip-panel.css; only the walking-specific bits live here. */
.walking-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 32px 16px;
  text-align: center;
  color: var(--text-secondary);
}

.walking-state p {
  margin: 0;
}

.walking-state--error {
  color: var(--color-danger);
}

.walking-spinner {
  animation: walking-spin 0.9s linear infinite;
}

@keyframes walking-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .walking-spinner {
    animation: none;
  }
}

.walking-retry {
  min-height: 44px;
  padding: 0 20px;
  border: none;
  border-radius: var(--radius-md);
  background: var(--accent-primary);
  color: var(--text-on-accent);
  font: inherit;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.walking-retry:hover {
  background: var(--accent-primary-hover);
}

.walking-note {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  margin: 0;
  padding: 10px 12px;
  border-radius: var(--radius-md);
  background: var(--color-warning-soft);
  color: var(--text-primary);
  font-size: 13px;
}

.walking-step__text {
  font-size: 14px;
  line-height: 1.4;
}

.walking-step--arrive .trip-step__icon {
  background: var(--text-primary);
  color: var(--bg-primary);
}
</style>
