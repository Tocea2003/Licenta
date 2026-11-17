<template>
  <figure class="twin-chart">
    <svg :viewBox="`0 0 ${W} ${H + 24}`" role="img" :aria-label="label" class="chart">
      <g transform="translate(0,8)">
        <rect x="0" :y="thresholdY" :width="W" :height="H - thresholdY" class="zone" />
        <line x1="0" :x2="W" :y1="thresholdY" :y2="thresholdY" class="threshold" />
        <text x="4" :y="thresholdY - 4" class="axis-text">{{ thresholdLabel }} {{ BUNCHING_THRESHOLD }}</text>
        <polyline
          v-for="line in lines"
          :key="line.id"
          :points="linePoints(line.series, line.durationSeconds, W, H, yMax)"
          :stroke="line.color"
          fill="none"
          stroke-width="2"
          stroke-linejoin="round"
        />
        <line x1="0" :x2="W" :y1="H" :y2="H" class="axis" />
        <text v-for="h in hourTicks" :key="h" :x="(h * 3600 / maxDuration) * W" :y="H + 14" class="axis-text" text-anchor="middle">{{ h }} h</text>
      </g>
    </svg>
    <figcaption class="note"><slot /></figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { TwinSample } from '@/types/twin'
import { BUNCHING_THRESHOLD, chartYMax, linePoints } from '@/utils/twin'

export interface ChartLine {
  id: string | number
  color: string
  series: TwinSample[]
  durationSeconds: number
}

const props = defineProps<{ lines: ChartLine[]; label: string; thresholdLabel: string }>()

const W = 600
const H = 160

const yMax = computed(() => chartYMax(props.lines.map(l => l.series)))
const thresholdY = computed(() => H - (BUNCHING_THRESHOLD / yMax.value) * H)
const maxDuration = computed(() => Math.max(...props.lines.map(l => l.durationSeconds), 3600))
const hourTicks = computed(() => Array.from({ length: Math.floor(maxDuration.value / 3600) + 1 }, (_, i) => i))
</script>

<style scoped>
.twin-chart { margin: 0; }
.chart { width: 100%; height: auto; display: block; }
.zone { fill: var(--color-danger-soft); }
.threshold { stroke: var(--color-danger); stroke-width: 1; stroke-dasharray: 4 3; }
.axis { stroke: var(--border-primary); stroke-width: 1; }
.axis-text { font-size: 10px; fill: var(--text-secondary); }
.note { margin: 4px 0 0; font-size: 12px; color: var(--text-secondary); }
</style>
