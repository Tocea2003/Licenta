<template>
  <section class="twin-panel" aria-labelledby="twin-title">
    <header class="panel-head">
      <h3 id="twin-title"><FlaskConical :size="18" aria-hidden="true" /> {{ t('twinTitle') }}</h3>
    </header>
    <p class="hint">{{ t('twinHint') }}</p>

    <form class="form" @submit.prevent="run">
      <label class="field">
        <span>{{ t('twinRoute') }}</span>
        <select v-model.number="form.routeId" required>
          <option :value="null" disabled>{{ t('twinPickRoute') }}</option>
          <option v-for="r in routes" :key="r.id" :value="r.id">{{ r.routeNumber }} · {{ r.name }}</option>
        </select>
      </label>

      <label class="field">
        <span>{{ t('twinBuses') }}</span>
        <input v-model.number="form.busCount" type="number" :min="MIN_BUSES" :max="MAX_BUSES" :disabled="form.useLive" />
      </label>

      <label class="field">
        <span>{{ t('twinDuration') }}</span>
        <select v-model.number="form.durationMinutes">
          <option :value="60">1 h</option>
          <option :value="120">2 h</option>
          <option :value="240">4 h</option>
          <option :value="360">6 h</option>
        </select>
      </label>

      <label class="check">
        <input v-model="form.useLive" type="checkbox" />
        <span>{{ t('twinUseLive') }}</span>
      </label>

      <label class="check">
        <input v-model="form.disturbance" type="checkbox" />
        <span>{{ t('twinDisturbance') }}</span>
      </label>
      <div v-if="form.disturbance" class="inline">
        <label class="field">
          <span>{{ t('twinAtMinute') }}</span>
          <input v-model.number="form.disturbanceAtMinute" type="number" min="0" :max="form.durationMinutes" />
        </label>
        <label class="field">
          <span>{{ t('twinHoldMinutes') }}</span>
          <input v-model.number="form.disturbanceHoldMinutes" type="number" min="1" max="60" />
        </label>
      </div>

      <button class="run" type="submit" :disabled="running || !scenario">
        <Play :size="16" aria-hidden="true" /> {{ running ? t('twinRunning') : t('twinRun') }}
      </button>
    </form>

    <p v-if="error" class="error" role="alert">{{ error }}</p>

    <div v-if="runs.length > 0" class="results">
      <div class="chart-wrap">
        <svg :viewBox="`0 0 ${W} ${H + 24}`" role="img" :aria-label="t('twinChartLabel')" class="chart">
          <g transform="translate(0,8)">
            <rect x="0" :y="thresholdY" :width="W" :height="H - thresholdY" class="zone" />
            <line x1="0" :x2="W" :y1="thresholdY" :y2="thresholdY" class="threshold" />
            <text x="4" :y="thresholdY - 4" class="axis-text">{{ t('twinThreshold') }} {{ BUNCHING_THRESHOLD }}</text>
            <polyline
              v-for="r in runs"
              :key="r.id"
              :points="linePoints(r.result.series, r.result.durationSeconds, W, H, yMax)"
              :stroke="r.color"
              fill="none"
              stroke-width="2"
              stroke-linejoin="round"
            />
            <line x1="0" :x2="W" :y1="H" :y2="H" class="axis" />
            <text v-for="h in hourTicks" :key="h" :x="(h * 3600 / maxDuration) * W" :y="H + 14" class="axis-text" text-anchor="middle">{{ h }} h</text>
          </g>
        </svg>
        <p class="chart-note">{{ t('twinChartNote') }}</p>
      </div>

      <table class="runs">
        <thead>
          <tr>
            <th scope="col">{{ t('twinScenario') }}</th>
            <th scope="col">{{ t('twinBunchedTime') }}</th>
            <th scope="col">{{ t('twinCv') }}</th>
            <th scope="col">{{ t('twinEpisodes') }}</th>
            <th scope="col"><span class="sr-only">{{ t('twinRemove') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in runs" :key="r.id">
            <td><span class="swatch" :style="{ background: r.color }"></span>{{ r.label }}</td>
            <td>{{ r.result.bunchedPercent }}%</td>
            <td>{{ r.result.meanCv }}</td>
            <td>{{ r.result.bunchingEpisodes }}</td>
            <td>
              <button type="button" class="icon-btn" :aria-label="t('twinRemove')" @click="remove(r.id)">
                <X :size="14" aria-hidden="true" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { FlaskConical, Play, X } from 'lucide-vue-next'
import { useLanguage } from '@/composables/useLanguage'
import { buildScenario, BUNCHING_THRESHOLD, chartYMax, linePoints, MAX_BUSES, MIN_BUSES, runLabel, type TwinForm } from '@/utils/twin'
import type { TwinRunEntry, TwinRunResult } from '@/types/twin'

const { t } = useLanguage()
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
const MAX_RUNS = 5
const COLORS = ['#2563eb', '#c2410c', '#0f766e', '#7c3aed', '#be123c']
const W = 600
const H = 160

interface RouteOption { id: number; routeNumber: string; name: string }

const routes = ref<RouteOption[]>([])
const runs = ref<TwinRunEntry[]>([])
const running = ref(false)
const error = ref('')
let nextId = 1

const form = reactive<TwinForm>({
  routeId: null,
  busCount: 3,
  durationMinutes: 240,
  useLive: false,
  disturbance: false,
  disturbanceAtMinute: 10,
  disturbanceHoldMinutes: 4,
  seed: 1
})

const scenario = computed(() => buildScenario(form))
const yMax = computed(() => chartYMax(runs.value.map(r => r.result)))
const thresholdY = computed(() => H - (BUNCHING_THRESHOLD / yMax.value) * H)
const maxDuration = computed(() => Math.max(...runs.value.map(r => r.result.durationSeconds), 3600))
const hourTicks = computed(() => Array.from({ length: Math.floor(maxDuration.value / 3600) + 1 }, (_, i) => i))

onMounted(async () => {
  try {
    const res = await fetch(`${API_BASE_URL}/Routes`)
    if (res.ok) routes.value = (await res.json()) as RouteOption[]
  } catch {
    error.value = t('twinLoadError')
  }
})

async function run() {
  const body = scenario.value
  if (!body) return
  running.value = true
  error.value = ''
  try {
    const res = await fetch(`${API_BASE_URL}/twin/run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    })
    if (!res.ok) {
      const problem = await res.json().catch(() => null)
      error.value = problem?.message ?? t('twinRunError')
      return
    }
    const result = (await res.json()) as TwinRunResult
    // seed diferit la fiecare rulare ca să nu obținem mereu aceeași traiectorie
    const id = nextId++
    runs.value = [...runs.value, { id, label: runLabel(form, result), color: COLORS[(id - 1) % COLORS.length]!, result }].slice(-MAX_RUNS)
    form.seed++
  } catch {
    error.value = t('twinRunError')
  } finally {
    running.value = false
  }
}

function remove(id: number) {
  runs.value = runs.value.filter(r => r.id !== id)
}
</script>

<style scoped>
.twin-panel {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 16px;
  padding: 20px;
}
.panel-head h3 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 18px; font-weight: 600; color: var(--text-primary); }
.hint { margin: 8px 0 16px; font-size: 13px; color: var(--text-secondary); }

.form { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; align-items: end; }
.field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: var(--text-secondary); }
.field select, .field input {
  min-height: 44px; padding: 0 10px; border-radius: 10px; font: inherit; font-size: 14px;
  border: 1px solid var(--border-primary); background: var(--bg-secondary); color: var(--text-primary);
}
.check { display: flex; align-items: center; gap: 8px; min-height: 44px; font-size: 14px; color: var(--text-primary); }
.check input { width: 18px; height: 18px; }
.inline { display: contents; }
.run {
  display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 44px; padding: 0 18px;
  border: 0; border-radius: 10px; cursor: pointer; font: inherit; font-weight: 600;
  background: var(--accent-primary); color: #fff;
}
.run:disabled { opacity: 0.5; cursor: not-allowed; }
.error { margin: 12px 0 0; font-size: 13px; color: var(--color-danger); }

.results { margin-top: 20px; display: grid; gap: 16px; }
.chart { width: 100%; height: auto; display: block; }
.zone { fill: var(--color-danger-soft); }
.threshold { stroke: var(--color-danger); stroke-width: 1; stroke-dasharray: 4 3; }
.axis { stroke: var(--border-primary); stroke-width: 1; }
.axis-text { font-size: 10px; fill: var(--text-secondary); }
.chart-note { margin: 4px 0 0; font-size: 12px; color: var(--text-secondary); }

.runs { width: 100%; border-collapse: collapse; font-size: 13px; font-variant-numeric: tabular-nums; }
.runs th { text-align: left; font-weight: 600; color: var(--text-secondary); padding: 6px 8px; border-bottom: 1px solid var(--border-primary); }
.runs td { padding: 8px; border-bottom: 1px solid var(--border-primary); color: var(--text-primary); }
.swatch { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 8px; }
.icon-btn {
  width: 32px; height: 32px; display: inline-flex; align-items: center; justify-content: center;
  border: 0; border-radius: 8px; cursor: pointer; background: transparent; color: var(--text-secondary);
}
.icon-btn:hover { background: var(--bg-secondary); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
