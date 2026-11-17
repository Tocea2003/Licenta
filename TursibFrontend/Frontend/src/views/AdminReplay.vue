<template>
  <div class="replay-page">
    <div class="page-header">
      <div>
        <h2>{{ t('replayTitle') }}</h2>
        <p class="subtitle">{{ t('replayHint') }}</p>
      </div>
    </div>

    <form class="controls" @submit.prevent="load">
      <label class="field">
        <span>{{ t('twinRoute') }}</span>
        <select v-model.number="routeId" required>
          <option :value="null" disabled>{{ t('twinPickRoute') }}</option>
          <option v-for="r in routes" :key="r.id" :value="r.id">{{ r.routeNumber }} · {{ r.name }}</option>
        </select>
      </label>

      <label class="field">
        <span>{{ t('replayWindow') }}</span>
        <select v-model="windowKey">
          <option value="latest">{{ t('replayLatest') }}</option>
          <option value="15">{{ t('replayLast15') }}</option>
          <option value="60">{{ t('replayLast60') }}</option>
          <option value="180">{{ t('replayLast180') }}</option>
        </select>
      </label>

      <button class="btn-load" type="submit" :disabled="routeId == null || status === 'loading'">
        <Download :size="16" aria-hidden="true" />
        {{ status === 'loading' ? t('loading') : t('replayLoad') }}
      </button>
    </form>

    <p v-if="status === 'error'" class="notice notice--error" role="alert">
      <TriangleAlert :size="16" aria-hidden="true" /> {{ errorText }}
    </p>
    <p v-else-if="status === 'empty'" class="notice" role="status">
      <Info :size="16" aria-hidden="true" /> {{ t('replayEmpty') }}
    </p>

    <div class="stage">
      <ReplayMap
        class="stage__map"
        :frames="frames"
        :trails="trails"
        :shape="shape"
        :color="routeColor"
        :route-label="routeLabel"
        :label="t('replayMapLabel')"
      />

      <aside class="stage__side" :aria-label="t('replayVehicles')">
        <h3>{{ t('replayVehicles') }} <span v-if="status === 'ready'" class="count">{{ frames.length }}/{{ tracks.length }}</span></h3>
        <ul v-if="frames.length > 0" class="vehicles">
          <li v-for="f in frames" :key="f.id">
            <span class="line-pill" :style="{ background: routeColor }">{{ routeLabel }}</span>
            <span class="vehicles__id">{{ f.id }}</span>
            <span class="vehicles__meta">{{ Math.round(f.speed) }} km/h</span>
          </li>
        </ul>
        <p v-else class="muted">{{ status === 'ready' ? t('replayNoneAtMoment') : t('replayLoadFirst') }}</p>
      </aside>
    </div>

    <section class="player" :class="{ 'player--disabled': status !== 'ready' }" :aria-label="t('replayTimeline')">
      <button
        type="button"
        class="btn-play"
        :disabled="status !== 'ready'"
        :aria-label="playing ? t('replayPause') : t('replayPlay')"
        @click="togglePlay"
      >
        <Pause v-if="playing" :size="20" aria-hidden="true" />
        <Play v-else :size="20" aria-hidden="true" />
      </button>

      <div class="timeline">
        <input
          class="timeline__slider"
          type="range"
          :min="range.start"
          :max="range.end"
          step="1"
          :value="playhead"
          :disabled="status !== 'ready'"
          :aria-label="t('replayTimeline')"
          :aria-valuetext="clock"
          @input="seek(($event.target as HTMLInputElement).valueAsNumber)"
        />
        <div class="timeline__labels">
          <span>{{ status === 'ready' ? formatClock(range.start) : '--:--:--' }}</span>
          <strong class="timeline__now">{{ clock }}</strong>
          <span>{{ status === 'ready' ? formatClock(range.end) : '--:--:--' }}</span>
        </div>
      </div>

      <div class="speeds" role="group" :aria-label="t('replaySpeed')">
        <button
          v-for="s in REPLAY_SPEEDS"
          :key="s"
          type="button"
          class="speed"
          :class="{ 'speed--active': speed === s }"
          :aria-pressed="speed === s"
          :disabled="status !== 'ready'"
          @click="speed = s"
        >{{ s }}×</button>
      </div>
    </section>

    <p v-if="status === 'ready'" class="summary">
      {{ t('replaySummary', undefined, { points: pointCount, buses: tracks.length, span: formatElapsed(range.end - range.start) }) }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { Download, Info, Pause, Play, TriangleAlert } from 'lucide-vue-next'
import ReplayMap, { type ReplayTrail } from '@/components/ReplayMap.vue'
import { useLanguage } from '@/composables/useLanguage'
import { fetchHistory, fetchHistoryBounds, HistoryError } from '@/services/historyService'
import {
  advancePlayhead, buildTracks, formatClock, formatElapsed, framesAt, REPLAY_SPEEDS, timeBounds, trailFor, windowRange,
  type ReplayTrack, type ReplayWindow
} from '@/utils/replay'

const { t } = useLanguage()
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
const TRAIL_SECONDS = 120

interface RouteOption { id: number; routeNumber: string; name: string; color?: string }

const routes = ref<RouteOption[]>([])
const routeId = ref<number | null>(null)
const windowKey = ref<ReplayWindow>('latest')
const status = ref<'idle' | 'loading' | 'ready' | 'empty' | 'error'>('idle')
const errorKind = ref<'disabled' | 'invalid' | 'network'>('network')
const errorMessage = ref('')

const tracks = ref<ReplayTrack[]>([])
const shape = ref<Array<[number, number]>>([])
const range = ref({ start: 0, end: 0 })
const playhead = ref(0)
const playing = ref(false)
const speed = ref<number>(15)

const route = computed(() => routes.value.find(r => r.id === routeId.value))
const routeColor = computed(() => route.value?.color || '#2563eb')
const routeLabel = computed(() => route.value?.routeNumber ?? '')
const pointCount = computed(() => tracks.value.reduce((n, tr) => n + tr.points.length, 0))
const clock = computed(() => (status.value === 'ready' ? formatClock(playhead.value) : '--:--:--'))

const frames = computed(() => (status.value === 'ready' ? framesAt(tracks.value, playhead.value) : []))
const trails = computed<ReplayTrail[]>(() =>
  status.value !== 'ready'
    ? []
    : tracks.value.map(tr => ({ id: tr.id, points: trailFor(tr, playhead.value, TRAIL_SECONDS) })).filter(tr => tr.points.length > 1))

const errorText = computed(() =>
  errorKind.value === 'disabled' ? t('replayDisabled') : errorKind.value === 'invalid' ? errorMessage.value : t('replayLoadError'))

onMounted(async () => {
  try {
    const res = await fetch(`${API_BASE_URL}/Routes`)
    if (res.ok) routes.value = (await res.json()) as RouteOption[]
  } catch {
    errorKind.value = 'network'
    status.value = 'error'
  }
})

async function loadShape(id: number) {
  shape.value = []
  try {
    const res = await fetch(`${API_BASE_URL}/shapes/route/${id}`)
    if (!res.ok) return
    const body = await res.json() as { points?: Array<{ latitude: number; longitude: number }> }
    shape.value = (body.points ?? []).map(p => [p.latitude, p.longitude] as [number, number])
  } catch {
    /* traseul desenat e opțional */
  }
}

async function load() {
  if (routeId.value == null) return
  stop()
  status.value = 'loading'
  const id = routeId.value
  void loadShape(id)
  try {
    const nowSeconds = Math.floor(Date.now() / 1000)
    const bounds = windowKey.value === 'latest' ? await fetchHistoryBounds(id) : null
    const interval = windowRange(windowKey.value, nowSeconds, bounds)
    const points = interval ? await fetchHistory(id, interval.from, interval.to) : []
    const built = buildTracks(points)
    const span = timeBounds(built)
    if (!span) {
      tracks.value = []
      status.value = 'empty'
      return
    }
    tracks.value = built
    range.value = { start: span.start, end: span.end }
    playhead.value = span.start
    status.value = 'ready'
  } catch (err) {
    errorKind.value = err instanceof HistoryError ? err.kind : 'network'
    errorMessage.value = err instanceof Error ? err.message : ''
    status.value = 'error'
  }
}

// --- redare ---

let raf = 0
let last = 0

function frame(now: number) {
  const { t: next, ended } = advancePlayhead(playhead.value, now - last, speed.value, range.value.end)
  last = now
  playhead.value = next
  if (ended) { playing.value = false; raf = 0; return }
  raf = requestAnimationFrame(frame)
}

function togglePlay() {
  if (status.value !== 'ready') return
  if (playing.value) { stop(); return }
  if (playhead.value >= range.value.end) playhead.value = range.value.start
  playing.value = true
  last = performance.now()
  raf = requestAnimationFrame(frame)
}

function stop() {
  playing.value = false
  if (raf) cancelAnimationFrame(raf)
  raf = 0
}

function seek(value: number) {
  playhead.value = Math.min(range.value.end, Math.max(range.value.start, value))
}

watch(routeId, () => { if (status.value === 'ready' || status.value === 'empty') { stop(); status.value = 'idle' } })
onBeforeUnmount(stop)
</script>

<style scoped>
.replay-page { display: grid; gap: 16px; }
.page-header h2 { margin: 0; color: var(--text-primary); }
.subtitle { margin: 4px 0 0; font-size: 14px; color: var(--text-secondary); }

.controls { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; align-items: end; }
.field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: var(--text-secondary); }
.field select {
  min-height: 44px; padding: 0 10px; border-radius: 10px; font: inherit; font-size: 14px;
  border: 1px solid var(--border-primary); background: var(--bg-secondary); color: var(--text-primary);
}
.btn-load {
  display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 44px; padding: 0 18px;
  border: 0; border-radius: 10px; cursor: pointer; font: inherit; font-weight: 600; background: var(--accent-primary); color: #fff;
}
.btn-load:disabled { opacity: 0.5; cursor: not-allowed; }

.notice {
  display: flex; align-items: center; gap: 8px; margin: 0; padding: 10px 14px; border-radius: 10px;
  font-size: 14px; background: var(--bg-secondary); color: var(--text-primary);
}
.notice--error { background: var(--color-danger-soft); color: var(--color-danger); }

.stage { display: grid; grid-template-columns: minmax(0, 1fr) 260px; gap: 16px; min-height: 420px; }
.stage__map { height: 100%; min-height: 420px; }
.stage__side {
  background: var(--bg-primary); border: 1px solid var(--border-primary); border-radius: 12px; padding: 14px; overflow: auto; max-height: 520px;
}
.stage__side h3 { margin: 0 0 10px; font-size: 14px; color: var(--text-primary); display: flex; justify-content: space-between; }
.count { font-size: 12px; font-weight: 600; color: var(--live-ink); background: var(--live-soft); padding: 2px 8px; border-radius: 8px; }
.vehicles { list-style: none; margin: 0; padding: 0; display: grid; gap: 8px; }
.vehicles li { display: flex; align-items: center; gap: 8px; font-size: 13px; }
.vehicles__id { color: var(--text-primary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1; min-width: 0; }
.vehicles__meta { color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.muted { margin: 0; font-size: 13px; color: var(--text-secondary); }

.player {
  display: grid; grid-template-columns: auto 1fr auto; gap: 16px; align-items: center;
  background: var(--bg-primary); border: 1px solid var(--border-primary); border-radius: 12px; padding: 12px 16px;
}
.player--disabled { opacity: 0.7; }
.btn-play {
  width: 44px; height: 44px; border-radius: 50%; border: 0; cursor: pointer;
  display: inline-flex; align-items: center; justify-content: center; background: var(--accent-primary); color: #fff;
}
.btn-play:disabled { opacity: 0.4; cursor: not-allowed; }
.timeline { display: grid; gap: 4px; min-width: 0; }
.timeline__slider { width: 100%; min-height: 24px; accent-color: var(--accent-primary); }
.timeline__labels { display: flex; justify-content: space-between; font-size: 12px; color: var(--text-secondary); font-variant-numeric: tabular-nums; }
.timeline__now { color: var(--text-primary); font-family: var(--font-display); font-size: 14px; }
.speeds { display: flex; gap: 4px; }
.speed {
  min-width: 44px; min-height: 44px; border-radius: 10px; cursor: pointer; font: inherit; font-weight: 600;
  border: 1px solid var(--border-primary); background: var(--bg-secondary); color: var(--text-primary);
}
.speed--active { background: var(--accent-primary); border-color: var(--accent-primary); color: #fff; }
.speed:disabled { opacity: 0.4; cursor: not-allowed; }
.summary { margin: 0; font-size: 13px; color: var(--text-secondary); }

@media (max-width: 860px) {
  .stage { grid-template-columns: 1fr; }
  .stage__side { max-height: 200px; }
  .player { grid-template-columns: auto 1fr; }
  .speeds { grid-column: 1 / -1; justify-content: center; }
}
</style>
