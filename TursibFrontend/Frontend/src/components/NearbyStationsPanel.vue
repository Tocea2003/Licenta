<template>
  <section v-if="visible" class="nearby-panel" :aria-label="t('nearYou')">
    <header class="panel-header">
      <h2 class="panel-title">{{ t('nearYou') }}</h2>
      <span class="panel-count">{{ stations.length }} {{ t('nearbyStationsCount') }}</span>
      <button type="button" @click="$emit('close')" class="close-btn" :aria-label="t('close')">
        <X :size="20" aria-hidden="true" />
      </button>
    </header>

    <ul class="stations-list">
      <li v-for="station in stations" :key="station.id" class="station-item">
        <div class="station-head">
          <span class="station-name">{{ station.name }}</span>
          <span class="station-distance">{{ formatDistance(station.distance) }}</span>
        </div>

        <div v-if="getStationETAs(station.id).length > 0" class="station-etas">
          <span v-for="eta in getStationETAs(station.id)" :key="eta.busId" class="eta-chip">
            <span class="line-pill" :style="{ background: eta.color }">{{ eta.routeNumber }}</span>
            <span class="eta-time">{{ eta.eta }}</span>
            <span class="eta-live" aria-hidden="true"></span>
          </span>
        </div>

        <p v-if="isNotificationActive(station.id)" class="notification-summary">
          <BellRing :size="14" aria-hidden="true" />
          <span v-if="isRouteNotificationActive(station.id)">{{ t('alertsActiveForLine', '', { line: getActiveRouteForStation(station.id) ?? '' }) }}</span>
          <span v-else>{{ t('alertsActiveAllBuses') }}</span>
        </p>

        <div class="station-actions">
          <button
            type="button"
            @click="$emit('toggleNotification', { stationId: station.id })"
            class="notification-btn"
            :class="{ active: isNotificationActive(station.id) && !isRouteNotificationActive(station.id) }"
          >
            <BellOff v-if="isNotificationActive(station.id) && !isRouteNotificationActive(station.id)" :size="16" aria-hidden="true" />
            <Bell v-else :size="16" aria-hidden="true" />
            {{ isNotificationActive(station.id) && !isRouteNotificationActive(station.id) ? t('disableAlerts') : t('stationAlerts') }}
          </button>

          <template v-if="getStationETAs(station.id).length > 0">
            <button
              v-for="eta in getUniqueRouteETAs(station.id)"
              :key="`${station.id}-${eta.routeId}`"
              type="button"
              class="route-chip"
              :class="{ active: isSpecificRouteActive(station.id, eta.routeId) }"
              :title="t('alertsForLine')"
              :aria-label="`${t('alertsForLine')}: ${eta.routeNumber}`"
              :aria-pressed="isSpecificRouteActive(station.id, eta.routeId)"
              @click="$emit('toggleNotification', { stationId: station.id, routeId: eta.routeId })"
            >
              <Bell :size="14" aria-hidden="true" />
              <span class="line-pill" :style="{ background: eta.color }">{{ eta.routeNumber }}</span>
            </button>
          </template>
        </div>
      </li>

      <li v-if="stations.length === 0" class="no-stations">{{ t('noStationsNearby') }}</li>
    </ul>

    <footer class="panel-footer"><span class="eta-live" aria-hidden="true"></span>{{ t('liveHint') }}</footer>
  </section>
</template>

<script setup lang="ts">
import type { Station } from '@/services/apiService'
import { Bell, BellOff, BellRing, X } from 'lucide-vue-next'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

interface StationWithDistance extends Station {
  distance: number
}

interface Props {
  visible: boolean
  stations: StationWithDistance[]
  activeNotificationStationId: number | null
  activeNotificationRouteId: number | null
  getStationETAs: (stationId: number) => Array<{
    busId: string
    routeId: number
    routeNumber: string
    eta: string
    color: string
  }>
}

const props = defineProps<Props>()

defineEmits<{
  close: []
  toggleNotification: [payload: { stationId: number; routeId?: number }]
}>()

const formatDistance = (distance: number): string => {
  if (distance < 1) {
    return `${Math.round(distance * 1000)} m`
  }
  return `${distance.toFixed(1)} km`
}

const isNotificationActive = (stationId: number): boolean => {
  return props.activeNotificationStationId === stationId
}

const isRouteNotificationActive = (stationId: number): boolean => {
  return isNotificationActive(stationId) && props.activeNotificationRouteId !== null
}

const isSpecificRouteActive = (stationId: number, routeId: number): boolean => {
  return isNotificationActive(stationId) && props.activeNotificationRouteId === routeId
}

const getUniqueRouteETAs = (stationId: number) => {
  const etas = props.getStationETAs(stationId)
  const seen = new Set<number>()
  const unique: typeof etas = []

  for (const eta of etas) {
    if (!seen.has(eta.routeId)) {
      seen.add(eta.routeId)
      unique.push(eta)
    }
  }

  return unique.slice(0, 4)
}

const getActiveRouteForStation = (stationId: number): string | null => {
  if (!isRouteNotificationActive(stationId) || props.activeNotificationRouteId === null) {
    return null
  }

  const matchingETA = props.getStationETAs(stationId).find(
    eta => eta.routeId === props.activeNotificationRouteId
  )

  return matchingETA?.routeNumber ?? String(props.activeNotificationRouteId)
}
</script>

<style scoped>
.nearby-panel {
  position: fixed;
  right: 76px; /* lângă coloana de butoane plutitoare */
  top: 16px;
  width: 360px;
  max-height: calc(100dvh - 32px);
  display: flex;
  flex-direction: column;
  background: var(--bg-primary);
  color: var(--text-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-xl);
  z-index: 1200;
  font-family: var(--font-sans);
  font-variant-numeric: tabular-nums;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 8px 10px 20px;
  border-bottom: 1px solid var(--border-subtle);
}

.panel-title {
  margin: 0;
  font-size: 20px;
  font-weight: var(--fw-bold);
}

.panel-count {
  font-size: 13px;
  color: var(--text-secondary);
}

.close-btn {
  margin-left: auto;
  width: 44px;
  height: 44px;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.close-btn:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.stations-list {
  list-style: none;
  margin: 0;
  padding: 0 20px;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.station-item {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px 0;
  border-bottom: 1px solid var(--border-subtle);
}

.station-item:last-child {
  border-bottom: none;
}

.station-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
}

.station-name {
  font-size: 16px;
  font-weight: var(--fw-bold);
}

.station-distance {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
}

.station-etas {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.eta-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px 4px 4px;
  border-radius: 8px;
  background: var(--bg-secondary);
  font-size: 14px;
  font-weight: var(--fw-bold);
}

.eta-live {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--live);
  display: inline-block;
}

.notification-summary {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 0;
  font-size: 13px;
  font-weight: var(--fw-semibold);
  color: var(--accent-primary);
}

.station-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.notification-btn,
.route-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-full);
  background: var(--bg-primary);
  color: var(--text-primary);
  font: inherit;
  font-size: 13px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.route-chip {
  padding: 0 4px 0 10px;
}

.route-chip .line-pill {
  height: 26px;
  border-radius: 13px;
}

.notification-btn:hover,
.route-chip:hover {
  background: var(--bg-tertiary);
}

.notification-btn.active,
.route-chip.active {
  background: var(--bg-inverse);
  border-color: var(--bg-inverse);
  color: var(--text-inverse);
}

.no-stations {
  padding: 24px 0;
  text-align: center;
  font-size: 14px;
  color: var(--text-secondary);
}

.panel-footer {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px 14px;
  font-size: 12px;
  color: var(--text-secondary);
  border-top: 1px solid var(--border-subtle);
}

@media (max-width: 767px) {
  .nearby-panel {
    right: 0;
    left: 0;
    top: auto;
    bottom: var(--nav-height);
    width: auto;
    max-height: 55dvh;
    border-radius: var(--radius-xl) var(--radius-xl) 0 0;
  }
}
</style>
