<template>
  <section class="bunching-panel" aria-labelledby="bunching-title">
    <header class="panel-head">
      <h3 id="bunching-title">
        <TriangleAlert :size="18" aria-hidden="true" />
        {{ t('bunchingTitle') }}
      </h3>
      <span class="count" :class="{ 'count--active': alerts.length > 0 }">
        {{ alerts.length }} {{ t('bunchingActive') }}
      </span>
    </header>
    <p class="hint">{{ t('bunchingHint') }}</p>

    <p v-if="alerts.length === 0" class="empty">
      <CircleCheck :size="18" aria-hidden="true" />
      {{ t('bunchingNone') }}
    </p>

    <ul v-else class="alert-list">
      <li v-for="a in alerts" :key="a.key" class="alert" :class="`alert--${a.severity}`">
        <span class="line-pill" :style="{ background: routeColor(a.routeId) }">{{ a.routeNumber || a.routeId }}</span>
        <div class="alert-body">
          <strong>{{ a.followerId }} → {{ a.leaderId }}</strong>
          <span class="detail">
            {{ t('bunchingGap') }} {{ formatGap(a.gapMeters) }} / {{ formatGap(a.idealGapMeters) }}
            <template v-if="a.headwaySeconds != null"> · ≈ {{ formatDuration(a.headwaySeconds) }}</template>
          </span>
        </div>
        <span class="badge">{{ t(a.severity === 'critical' ? 'bunchingCritical' : 'bunchingWarning') }}</span>
        <span class="since">{{ formatDuration(alertDurationSeconds(a, nowSeconds)) }}</span>
      </li>
    </ul>

    <details v-if="recent.length > 0" class="recent">
      <summary>{{ t('bunchingRecent') }} ({{ recent.length }})</summary>
      <ul class="recent-list">
        <li v-for="a in recent.slice(0, 10)" :key="`${a.key}-${a.startedAt}`">
          <span class="line-pill" :style="{ background: routeColor(a.routeId) }">{{ a.routeNumber || a.routeId }}</span>
          <span>{{ a.followerId }} → {{ a.leaderId }}</span>
          <span class="since">{{ formatDuration(alertDurationSeconds(a, nowSeconds)) }}</span>
        </li>
      </ul>
    </details>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { CircleCheck, TriangleAlert } from 'lucide-vue-next'
import { useLiveBusesStore } from '@/stores/liveBuses'
import { useLanguage } from '@/composables/useLanguage'
import { alertDurationSeconds, formatDuration, formatGap, parseBunchingList, sortAlerts } from '@/utils/bunching'
import type { BunchingAlert } from '@/types/bus'

const { t } = useLanguage()
const store = useLiveBusesStore()
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

const recent = ref<BunchingAlert[]>([])
const nowSeconds = ref(Math.floor(Date.now() / 1000))
const alerts = computed(() => sortAlerts(store.bunching))

/** culori stabile per linie, ca să recunoști linia dintr-o privire */
const palette = ['#2563eb', '#0f766e', '#b45309', '#7c3aed', '#be123c', '#0369a1', '#4d7c0f']
const routeColor = (routeId: number) => palette[routeId % palette.length]

// Alertele active vin prin SignalR (store); istoricul îl citim din REST la montare și când o alertă se încheie.
async function loadRecent() {
  try {
    const res = await fetch(`${API_BASE_URL}/live/bunching`)
    if (res.ok) recent.value = parseBunchingList((await res.json()).recent)
  } catch {
    /* istoricul e opțional */
  }
}

watch(() => alerts.value.length, (now, before) => { if (now < before) void loadRecent() })

let tick: ReturnType<typeof setInterval> | null = null
onMounted(() => {
  store.subscribe()
  void loadRecent()
  tick = setInterval(() => { nowSeconds.value = Math.floor(Date.now() / 1000) }, 1000)
})
onUnmounted(() => {
  store.unsubscribe()
  if (tick) clearInterval(tick)
})
</script>

<style scoped>
.bunching-panel {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 16px;
  padding: 20px;
}
.panel-head { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.panel-head h3 {
  display: flex; align-items: center; gap: 8px;
  margin: 0; font-size: 18px; font-weight: 600; color: var(--text-primary);
}
.count {
  font-size: 12px; font-weight: 600; padding: 4px 10px; border-radius: 8px;
  color: var(--text-secondary); background: var(--bg-secondary);
}
.count--active { color: var(--color-danger); background: var(--color-danger-soft); }
.hint { margin: 8px 0 16px; font-size: 13px; color: var(--text-secondary); }
.empty { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 14px; color: var(--text-secondary); }

.alert-list, .recent-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.alert {
  display: grid; grid-template-columns: auto 1fr auto auto; align-items: center; gap: 12px;
  padding: 12px 14px; border-radius: 12px; border: 1px solid var(--border-primary);
}
.alert--critical { background: var(--color-danger-soft); border-color: var(--color-danger); }
.alert--warning { background: var(--live-soft); }
.alert-body { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.alert-body strong { font-size: 14px; color: var(--text-primary); overflow-wrap: anywhere; }
.detail { font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.badge {
  font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.04em; padding: 3px 8px; border-radius: 6px;
}
.alert--critical .badge { color: var(--color-danger); border: 1px solid var(--color-danger); }
.alert--warning .badge { color: var(--live-ink); border: 1px solid var(--live-ink); }
.since { font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }

.recent { margin-top: 16px; font-size: 13px; color: var(--text-secondary); }
.recent summary { cursor: pointer; min-height: 32px; display: flex; align-items: center; }
.recent-list { margin-top: 8px; }
.recent-list li { display: flex; align-items: center; gap: 10px; }
.recent-list .since { margin-left: auto; }

@media (max-width: 560px) {
  .alert { grid-template-columns: auto 1fr auto; }
  .alert .since { grid-column: 2 / 4; }
}
</style>
