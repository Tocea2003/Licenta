<template>
  <section v-if="visible" class="trip-panel" :aria-label="t('routeWithTransfer')">
    <header class="trip-panel__header">
      <h3 class="trip-panel__title">{{ t('routeWithTransfer') }}</h3>
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
          <span>{{ t('transfers') }} <strong>{{ transferCount }}</strong></span>
          <span v-if="totalWalkDistance > 0">{{ t('walkingDistance') }} <strong>{{ formatDistance(totalWalkDistance) }}</strong></span>
        </div>
      </div>

      <ol class="trip-steps">
        <!-- Mers pe jos către prima stație -->
        <li class="trip-step trip-step--walk">
          <span class="trip-step__rail"><span class="trip-step__icon"><Footprints :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="trip-step__title">{{ t('walkOnFoot') }}</span>
              <span class="trip-step__duration">{{ formatDuration(firstWalkTime) }}</span>
            </div>
            <div class="trip-step__line">{{ t('toLabel') }} <strong>{{ boardingStation?.name }}</strong></div>
            <div v-if="firstWalkDistance > 0" class="trip-step__sub">{{ formatDistance(firstWalkDistance) }} · {{ t('fromLabel') }} {{ startName }}</div>
          </div>
        </li>

        <!-- Prima linie -->
        <li class="trip-step trip-step--bus" :style="{ '--step-color': route1Color }">
          <span class="trip-step__rail"><span class="trip-step__icon"><BusFront :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="line-pill" :style="{ background: route1Color }">{{ route1Number }}</span>
              <span class="trip-step__sub">{{ route1StationsCount }} {{ t('stationsLabel') }}</span>
              <span class="trip-step__duration">{{ formatDuration(busTime1) }}</span>
            </div>
            <div class="trip-step__line"><span class="label">{{ t('boarding') }}</span> <strong>{{ boardingStation?.name }}</strong><span v-if="formatTime(bus1StartTime)" class="time">{{ formatTime(bus1StartTime) }}</span></div>
            <div class="trip-step__line"><span class="label">{{ t('alighting') }}</span> <strong>{{ transferStation?.name }}</strong><span v-if="formatTime(bus1EndTime)" class="time">{{ formatTime(bus1EndTime) }}</span></div>
          </div>
        </li>

        <li class="trip-transfer">
          <span class="trip-transfer__dot" aria-hidden="true"></span>
          <span class="trip-transfer__chip"><ArrowLeftRight :size="14" aria-hidden="true" />{{ t('changeBus') }}</span>
        </li>

        <!-- A doua linie -->
        <li class="trip-step trip-step--bus" :style="{ '--step-color': route2Color }">
          <span class="trip-step__rail"><span class="trip-step__icon"><BusFront :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="line-pill" :style="{ background: route2Color }">{{ route2Number }}</span>
              <span class="trip-step__sub">{{ route2StationsCount }} {{ t('stationsLabel') }}</span>
              <span class="trip-step__duration">{{ formatDuration(busTime2) }}</span>
            </div>
            <div class="trip-step__line"><span class="label">{{ t('boarding') }}</span> <strong>{{ transferStation?.name }}</strong><span v-if="formatTime(bus2StartTime)" class="time">{{ formatTime(bus2StartTime) }}</span></div>
            <div class="trip-step__line"><span class="label">{{ t('alighting') }}</span> <strong>{{ secondBusDropoffStation?.name }}</strong><span v-if="formatTime(bus2EndTime)" class="time">{{ formatTime(bus2EndTime) }}</span></div>
          </div>
        </li>

        <template v-if="hasThirdBus">
          <li class="trip-transfer">
            <span class="trip-transfer__dot" aria-hidden="true"></span>
            <span class="trip-transfer__chip"><ArrowLeftRight :size="14" aria-hidden="true" />{{ t('changeBus') }}</span>
          </li>

          <!-- A treia linie -->
          <li class="trip-step trip-step--bus" :style="{ '--step-color': route3Color }">
            <span class="trip-step__rail"><span class="trip-step__icon"><BusFront :size="16" aria-hidden="true" /></span></span>
            <div class="trip-step__content">
              <div class="trip-step__head">
                <span class="line-pill" :style="{ background: route3Color }">{{ route3Number }}</span>
                <span class="trip-step__sub">{{ route3StationsCount }} {{ t('stationsLabel') }}</span>
                <span class="trip-step__duration">{{ formatDuration(busTime3 || 0) }}</span>
              </div>
              <div class="trip-step__line"><span class="label">{{ t('boarding') }}</span> <strong>{{ transferStation2?.name }}</strong><span v-if="formatTime(bus3StartTime)" class="time">{{ formatTime(bus3StartTime) }}</span></div>
              <div class="trip-step__line"><span class="label">{{ t('alighting') }}</span> <strong>{{ alightingStation?.name }}</strong><span v-if="formatTime(bus3EndTime)" class="time">{{ formatTime(bus3EndTime) }}</span></div>
            </div>
          </li>
        </template>

        <!-- Mers pe jos către destinație -->
        <li class="trip-step trip-step--walk">
          <span class="trip-step__rail"><span class="trip-step__icon"><Flag :size="16" aria-hidden="true" /></span></span>
          <div class="trip-step__content">
            <div class="trip-step__head">
              <span class="trip-step__title">{{ t('walkOnFoot') }}</span>
              <span class="trip-step__duration">{{ formatDuration(secondWalkTime) }}</span>
            </div>
            <div class="trip-step__line">{{ t('toLabel') }} <strong>{{ endName }}</strong></div>
            <div v-if="secondWalkDistance > 0" class="trip-step__sub">{{ formatDistance(secondWalkDistance) }} · {{ t('fromLabel') }} {{ alightingStation?.name }}</div>
          </div>
        </li>
      </ol>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useLanguage } from '@/composables/useLanguage'
import { ArrowLeftRight, ArrowRight, BusFront, Flag, Footprints, X } from 'lucide-vue-next'
import '@/assets/trip-panel.css'

const { t } = useLanguage()

interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

const props = defineProps<{
  visible: boolean
  startName: string
  endName: string
  boardingStation: Station | null
  transferStation: Station | null
  transferStation2?: Station | null
  alightingStation: Station | null
  route1Number: string
  route1Color: string
  route1StationsCount: number
  route2Number: string
  route2Color: string
  route2StationsCount: number
  route3Number?: string
  route3Color?: string
  route3StationsCount?: number
  firstWalkDistance: number
  firstWalkTime: number
  busTime1: number
  busTime2: number
  busTime3?: number
  secondWalkDistance: number
  secondWalkTime: number
  bus1StartTime?: string
  bus1EndTime?: string
  bus2StartTime?: string
  bus2EndTime?: string
  bus3StartTime?: string
  bus3EndTime?: string
  departureTime?: string
  arrivalTime?: string
}>()

defineEmits<{
  close: []
}>()

const totalTime = computed(() => 
  props.firstWalkTime + props.busTime1 + props.busTime2 + (props.busTime3 || 0) + props.secondWalkTime
)

const hasThirdBus = computed(() => Boolean(props.route3Number && props.transferStation2))

const secondBusDropoffStation = computed(() => {
  if (hasThirdBus.value && props.transferStation2) {
    return props.transferStation2
  }
  return props.alightingStation
})

const transferCount = computed(() => (hasThirdBus.value ? 2 : 1))

const totalWalkDistance = computed(() => 
  props.firstWalkDistance + props.secondWalkDistance
)

const formatTime = (isoString?: string): string => {
  if (!isoString) return ''
  const date = new Date(isoString)
  if (isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('ro-RO', { hour: '2-digit', minute: '2-digit' })
}

const formatDistance = (meters: number): string => {
  if (meters < 1000) {
    return `${Math.round(meters)} m`
  }
  return `${(meters / 1000).toFixed(1)} km`
}

const formatDuration = (minutes: number): string => {
  if (minutes < 60) {
    return `${Math.round(minutes)} min`
  }
  const hours = Math.floor(minutes / 60)
  const mins = Math.round(minutes % 60)
  return `${hours}h ${mins}min`
}
</script>
