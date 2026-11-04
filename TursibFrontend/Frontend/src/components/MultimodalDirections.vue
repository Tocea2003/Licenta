<template>
  <section v-if="visible" class="trip-panel" :aria-label="t('completeRoute')">
    <header class="trip-panel__header">
      <h3 class="trip-panel__title">{{ t('completeRoute') }}</h3>
      <button type="button" @click="$emit('close')" class="trip-panel__close" :aria-label="t('close')">
        <X :size="20" aria-hidden="true" />
      </button>
    </header>

    <div class="trip-panel__body">
      <div class="trip-summary">
        <div v-if="formatTime(departureTime) && formatTime(arrivalTime)" class="trip-summary__times">
          <span>{{ formatTime(departureTime) }}</span>
          <ArrowRight :size="20" class="trip-summary__arrow" aria-hidden="true" />
          <span>{{ formatTime(arrivalTime) }}</span>
        </div>
        <div class="trip-summary__meta">
          <span>{{ t('totalDuration') }} <strong>{{ formatDuration(totalTime) }}</strong></span>
          <span v-if="busLine">{{ t('transfers') }} <strong>0</strong></span>
          <span v-if="totalWalkDistance > 0">{{ t('walkingDistance') }} <strong>{{ formatDistance(totalWalkDistance) }}</strong></span>
        </div>
      </div>

      <ol class="trip-steps">
        <!-- Mers pe jos către prima stație -->
        <li v-if="showFirstWalk" class="trip-step trip-step--walk">
          <span class="trip-step__rail"><span class="trip-step__icon"><Footprints :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="trip-step__title">{{ t('walkOnFoot') }}</span>
              <span class="trip-step__duration">{{ formatDuration(firstWalkTime) }}</span>
            </div>
            <div class="trip-step__line">{{ t('toLabel') }} <strong>{{ boardingStation || endLocation }}</strong></div>
            <div v-if="firstWalkDistance > 0" class="trip-step__sub">{{ formatDistance(firstWalkDistance) }} · {{ t('fromLabel') }} {{ startLocation }}</div>
          </div>
        </li>

        <!-- Linia de autobuz -->
        <li v-if="busLine" class="trip-step trip-step--bus" :style="{ '--step-color': busColor }">
          <span class="trip-step__rail"><span class="trip-step__icon"><BusFront :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="line-pill" :style="{ background: busColor }">{{ busLine }}</span>
              <span class="trip-step__sub">{{ displayStationsCount }} {{ t('stationsLabel') }}</span>
              <span class="trip-step__duration">{{ formatDuration(busTime) }}</span>
            </div>
            <div class="trip-step__line"><span class="label">{{ t('boarding') }}</span> <strong>{{ boardingStation }}</strong><span v-if="formatTime(busStartTime)" class="time">{{ formatTime(busStartTime) }}</span></div>
            <div class="trip-step__line"><span class="label">{{ t('alighting') }}</span> <strong>{{ alightingStation }}</strong><span v-if="formatTime(busEndTime)" class="time">{{ formatTime(busEndTime) }}</span></div>

            <template v-if="busStationsList.length > 2">
              <button type="button" class="trip-panel__toggle" @click="showAllStations = !showAllStations" :aria-expanded="showAllStations">
                {{ showAllStations ? t('hideStations') : t('showAllStations') }}
              </button>
              <ol v-if="showAllStations" class="trip-panel__stations">
                <li v-for="(station, idx) in busStationsList" :key="`bus-station-${idx}`">{{ station }}</li>
              </ol>
            </template>
          </div>
        </li>

        <!-- Mers pe jos către destinație -->
        <li v-if="showSecondWalk" class="trip-step trip-step--walk">
          <span class="trip-step__rail"><span class="trip-step__icon"><Flag :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="trip-step__title">{{ t('walkOnFoot') }}</span>
              <span class="trip-step__duration">{{ formatDuration(secondWalkTime) }}</span>
            </div>
            <div class="trip-step__line">{{ t('toLabel') }} <strong>{{ endLocation }}</strong></div>
            <div v-if="secondWalkDistance > 0" class="trip-step__sub">{{ formatDistance(secondWalkDistance) }} · {{ t('fromLabel') }} {{ alightingStation }}</div>
          </div>
        </li>
      </ol>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useLanguage } from '@/composables/useLanguage'
import { ArrowRight, BusFront, Flag, Footprints, X } from 'lucide-vue-next'
import '@/assets/trip-panel.css'

const { t } = useLanguage()

const props = defineProps<{
  visible: boolean
  startLocation: string
  endLocation: string
  boardingStation: string
  alightingStation: string
  busLine: string
  busColor: string
  busStationsList: string[]
  firstWalkDistance: number
  firstWalkTime: number
  secondWalkDistance: number
  secondWalkTime: number
  busTime: number
  busStationsCount?: number
  busStartTime?: string
  busEndTime?: string
  departureTime?: string
  arrivalTime?: string
}>()

defineEmits<{
  close: []
}>()

const showAllStations = ref(false)

const showFirstWalk = computed(() => props.firstWalkDistance > 0 || props.firstWalkTime > 0)
const showSecondWalk = computed(() => props.secondWalkDistance > 0 || props.secondWalkTime > 0)
const totalTime = computed(() => props.firstWalkTime + props.busTime + props.secondWalkTime)
const totalWalkDistance = computed(() => props.firstWalkDistance + props.secondWalkDistance)
const displayStationsCount = computed(() => props.busStationsCount || props.busStationsList.length)

const formatTime = (isoString?: string): string => {
  if (!isoString) return ''
  const date = new Date(isoString)
  if (isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })
}

const formatDistance = (meters: number): string => {
  if (meters < 1000) return `${Math.round(meters)} m`
  return `${(meters / 1000).toFixed(1)} km`
}

const formatDuration = (minutes: number): string => {
  if (minutes < 60) return `${Math.round(minutes)} min`
  const hours = Math.floor(minutes / 60)
  const mins = Math.round(minutes % 60)
  return `${hours}h ${mins}min`
}
</script>
