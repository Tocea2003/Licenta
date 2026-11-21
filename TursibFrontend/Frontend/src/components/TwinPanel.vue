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
        <span>{{ t('twinFleetMode') }}</span>
        <select v-model="form.fleetMode" :disabled="form.useLive">
          <option value="buses">{{ t('twinFleetByBuses') }}</option>
          <option value="headway">{{ t('twinFleetByHeadway') }}</option>
        </select>
      </label>

      <label v-if="form.fleetMode !== 'headway'" class="field">
        <span>{{ t('twinBuses') }}</span>
        <input v-model.number="form.busCount" type="number" :min="MIN_BUSES" :max="MAX_BUSES" :disabled="form.useLive" />
      </label>
      <label v-else class="field">
        <span>{{ t('twinHeadwayMinutes') }}</span>
        <input v-model.number="form.headwayMinutes" type="number" min="2" max="120" :disabled="form.useLive" />
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

      <label class="check">
        <input v-model="form.extraBuses" type="checkbox" />
        <span>{{ t('twinExtraBuses') }}</span>
      </label>
      <div v-if="form.extraBuses" class="inline">
        <label class="field">
          <span>{{ t('twinExtraCount') }}</span>
          <input v-model.number="form.extraBusesCount" type="number" min="1" max="10" />
        </label>
        <label class="field">
          <span>{{ t('twinAtMinute') }}</span>
          <input v-model.number="form.extraBusesAtMinute" type="number" min="0" :max="form.durationMinutes" />
        </label>
      </div>

      <label class="check">
        <input v-model="form.closure" type="checkbox" />
        <span>{{ t('twinClosure') }}</span>
      </label>
      <div v-if="form.closure" class="inline">
        <label class="field">
          <span>{{ t('twinClosureFromKm') }}</span>
          <input v-model.number="form.closureFromKm" type="number" min="0" step="0.1" />
        </label>
        <label class="field">
          <span>{{ t('twinClosureToKm') }}</span>
          <input v-model.number="form.closureToKm" type="number" min="0" step="0.1" />
        </label>
        <label class="field">
          <span>{{ t('twinClosureDetourKm') }}</span>
          <input v-model.number="form.closureDetourKm" type="number" min="0" max="20" step="0.1" />
        </label>
        <label class="field">
          <span>{{ t('twinClosureWindow') }}</span>
          <span class="pair">
            <input v-model.number="form.closureFromMinute" type="number" min="0" :aria-label="t('twinClosureFromMinute')" />
            <input v-model.number="form.closureToMinute" type="number" min="0" :aria-label="t('twinClosureToMinute')" :placeholder="t('twinUntilEnd')" />
          </span>
        </label>
      </div>

      <button class="run" type="submit" :disabled="busy || !scenario">
        <Play :size="16" aria-hidden="true" /> {{ running ? t('twinRunning') : t('twinRun') }}
      </button>
      <button class="run run--secondary" type="button" :disabled="busy || !scenario" @click="compare">
        <Scale :size="16" aria-hidden="true" /> {{ comparing ? t('twinRunning') : t('twinCompare') }}
      </button>
    </form>

    <p v-if="error" class="error" role="alert">{{ error }}</p>

    <div v-if="runs.length > 0" class="results">
      <TwinChart :lines="runLines" :label="t('twinChartLabel')" :threshold-label="t('twinThreshold')">
        {{ t('twinChartNote') }}
      </TwinChart>

      <table class="runs">
        <thead>
          <tr>
            <th scope="col">{{ t('twinScenario') }}</th>
            <th scope="col">{{ t('twinBunchedTime') }}</th>
            <th scope="col">{{ t('twinCv') }}</th>
            <th scope="col">{{ t('twinMeanWait') }}</th>
            <th scope="col">{{ t('twinEpisodes') }}</th>
            <th scope="col"><span class="sr-only">{{ t('twinRemove') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in runs" :key="r.id">
            <td><span class="swatch" :style="{ background: r.color }"></span>{{ r.label }}</td>
            <td>{{ r.result.bunchedPercent }}%</td>
            <td>{{ r.result.meanCv }}</td>
            <td>{{ r.result.meanWaitSeconds != null ? formatDuration(r.result.meanWaitSeconds) : '–' }}</td>
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

    <div v-if="comparison" class="results comparison">
      <h4>{{ t('twinComparisonTitle') }}</h4>
      <p v-if="verdict" class="verdict">{{ verdict }}</p>
      <TwinChart :lines="comparisonLines" :label="t('twinChartLabel')" :threshold-label="t('twinThreshold')">
        {{ t('twinChartNote') }}
      </TwinChart>
      <table class="runs">
        <thead>
          <tr>
            <th scope="col">{{ t('twinStrategy') }}</th>
            <th scope="col">{{ t('twinTotalDelay') }}</th>
            <th scope="col">{{ t('twinMeanWait') }}</th>
            <th scope="col">{{ t('twinHeadwayCv') }}</th>
            <th scope="col">{{ t('twinBunchedTime') }}</th>
            <th scope="col">{{ t('twinHoldCost') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in comparison.results" :key="r.strategy" :class="{ best: r.strategy === best?.strategy }">
            <td><span class="swatch" :style="{ background: strategyColor(r.strategy) }"></span>{{ t(`twinStrategy_${r.strategy}`) }}</td>
            <td><strong>{{ formatDuration(r.totalDelayPerPassengerSeconds) }}</strong> <span class="pm">± {{ Math.round(r.totalDelayStdSeconds) }} s</span></td>
            <td>{{ formatDuration(r.meanWaitSeconds) }}</td>
            <td>{{ r.headwayCv }}</td>
            <td>{{ r.bunchedPercent }}%</td>
            <td>{{ r.holdSecondsPerBusHour }} s</td>
          </tr>
        </tbody>
      </table>
      <p class="chart-note">{{ t('twinComparisonNote', undefined, { n: comparison.replications }) }}</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { FlaskConical, Play, Scale, X } from 'lucide-vue-next'
import TwinChart, { type ChartLine } from '@/components/TwinChart.vue'
import { useLanguage } from '@/composables/useLanguage'
import { formatDuration } from '@/utils/bunching'
import { bestStrategy, buildCompareRequest, buildScenario, MAX_BUSES, MIN_BUSES, reductionPercent, runLabel, type TwinForm } from '@/utils/twin'
import type { TwinComparison, TwinRunEntry, TwinRunResult, TwinStrategy } from '@/types/twin'

const { t } = useLanguage()
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
const MAX_RUNS = 5
const COLORS = ['#2563eb', '#c2410c', '#0f766e', '#7c3aed', '#be123c']
const STRATEGY_COLORS: Record<TwinStrategy, string> = { none: '#be123c', headway: '#2563eb', equalize: '#0f766e', rl: '#7c3aed' }
const strategyColor = (s: TwinStrategy) => STRATEGY_COLORS[s]

interface RouteOption { id: number; routeNumber: string; name: string }

const routes = ref<RouteOption[]>([])
const runs = ref<TwinRunEntry[]>([])
const running = ref(false)
const comparing = ref(false)
const comparison = ref<TwinComparison | null>(null)
const busy = computed(() => running.value || comparing.value)
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
  seed: 1,
  fleetMode: 'buses',
  headwayMinutes: 10,
  extraBuses: false,
  extraBusesCount: 1,
  extraBusesAtMinute: 60,
  closure: false,
  closureFromKm: 1,
  closureToKm: 1.8,
  closureDetourKm: 1.2,
  closureFromMinute: 0,
  closureToMinute: undefined
})

const scenario = computed(() => buildScenario(form))
const runLines = computed<ChartLine[]>(() =>
  runs.value.map(r => ({ id: r.id, color: r.color, series: r.result.series, durationSeconds: r.result.durationSeconds })))
const comparisonLines = computed<ChartLine[]>(() =>
  (comparison.value?.results ?? []).map(r => ({
    id: r.strategy, color: strategyColor(r.strategy), series: r.series, durationSeconds: comparison.value!.durationSeconds
  })))
const best = computed(() => bestStrategy(comparison.value?.results ?? []))
/** Concluzia în cuvinte: cât scade întârzierea pe pasager cu cea mai bună strategie față de fără control, și cu ce cost. */
const verdict = computed(() => {
  const none = comparison.value?.results.find(r => r.strategy === 'none')
  const top = best.value
  if (!none || !top || top.strategy === 'none') return ''
  return t('twinVerdict', undefined, {
    strategy: t(`twinStrategy_${top.strategy}`),
    drop: reductionPercent(none.totalDelayPerPassengerSeconds, top.totalDelayPerPassengerSeconds),
    cvBefore: none.headwayCv,
    cvAfter: top.headwayCv,
    hold: Math.round(top.holdSecondsPerBusHour)
  })
})

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

async function compare() {
  const body = buildCompareRequest(form)
  if (!body) return
  comparing.value = true
  error.value = ''
  try {
    const res = await fetch(`${API_BASE_URL}/twin/compare`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    })
    if (!res.ok) {
      const problem = await res.json().catch(() => null)
      error.value = problem?.message ?? t('twinRunError')
      return
    }
    comparison.value = (await res.json()) as TwinComparison
  } catch {
    error.value = t('twinRunError')
  } finally {
    comparing.value = false
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
.pair { display: flex; gap: 6px; }
.pair input { width: 50%; min-width: 0; }
.check { display: flex; align-items: center; gap: 8px; min-height: 44px; font-size: 14px; color: var(--text-primary); }
.check input { width: 18px; height: 18px; }
.inline { display: contents; }
.run {
  display: inline-flex; align-items: center; justify-content: center; gap: 8px; min-height: 44px; padding: 0 18px;
  border: 0; border-radius: 10px; cursor: pointer; font: inherit; font-weight: 600;
  background: var(--accent-primary); color: #fff;
}
.run:disabled { opacity: 0.5; cursor: not-allowed; }
.run--secondary { background: var(--bg-secondary); color: var(--text-primary); border: 1px solid var(--border-primary); }
.comparison h4 { margin: 0; font-size: 15px; color: var(--text-primary); }
.pm { font-size: 11px; color: var(--text-secondary); }
.verdict { margin: 0; font-size: 14px; font-weight: 600; color: var(--text-primary); }
.runs tr.best td { background: var(--live-soft); }
.error { margin: 12px 0 0; font-size: 13px; color: var(--color-danger); }

.results { margin-top: 20px; display: grid; gap: 16px; }
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
