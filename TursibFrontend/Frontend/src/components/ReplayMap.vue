<template>
  <div ref="container" class="replay-map" role="application" :aria-label="label"></div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import L from 'leaflet'
import type { ReplayFrame } from '@/utils/replay'

export interface ReplayTrail {
  id: string
  points: Array<[number, number]>
}

const props = defineProps<{
  frames: ReplayFrame[]
  trails: ReplayTrail[]
  /** geometria liniei (desenată sub autobuze) */
  shape: Array<[number, number]>
  color: string
  routeLabel: string
  label: string
}>()

const container = ref<HTMLElement>()
let map: L.Map | null = null
let shapeLayer: L.Polyline | null = null
let markerLayer: L.LayerGroup | null = null
let trailLayer: L.LayerGroup | null = null
const markers = new Map<string, L.Marker>()
let fitted = false

function icon(frame: ReplayFrame) {
  const stopped = frame.status === 'at_station'
  return L.divIcon({
    className: 'replay-bus',
    iconSize: [34, 34],
    iconAnchor: [17, 17],
    html: `<span class="replay-bus__pill${stopped ? ' replay-bus__pill--stopped' : ''}" style="background:${props.color}">${escapeHtml(props.routeLabel)}</span>`
  })
}

function escapeHtml(value: string) {
  return value.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!)
}

onMounted(() => {
  map = L.map(container.value!, { zoomControl: true, attributionControl: true }).setView([45.7983, 24.1256], 13)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    className: 'replay-tiles',
    maxZoom: 19
  }).addTo(map)
  trailLayer = L.layerGroup().addTo(map)
  markerLayer = L.layerGroup().addTo(map)
  drawShape()
})

onBeforeUnmount(() => {
  map?.remove()
  map = null
  markers.clear()
})

function drawShape() {
  if (!map) return
  shapeLayer?.remove()
  shapeLayer = null
  fitted = false
  if (props.shape.length < 2) return
  shapeLayer = L.polyline(props.shape, { color: props.color, weight: 5, opacity: 0.55 }).addTo(map)
  map.fitBounds(shapeLayer.getBounds(), { padding: [30, 30] })
  fitted = true
}

watch(() => [props.shape, props.color], drawShape)

// Markerii sunt imperativi (ca pe harta principală): se mută, nu se recreează, la fiecare cadru.
watch(() => props.frames, frames => {
  if (!map || !markerLayer) return
  const seen = new Set<string>()
  for (const frame of frames) {
    seen.add(frame.id)
    const existing = markers.get(frame.id)
    if (existing) {
      existing.setLatLng([frame.latitude, frame.longitude])
    } else {
      const marker = L.marker([frame.latitude, frame.longitude], { icon: icon(frame), keyboard: false, title: frame.id })
      marker.addTo(markerLayer)
      markers.set(frame.id, marker)
    }
    // starea „în stație” schimbă doar clasa pastilei
    markers.get(frame.id)!.getElement()?.querySelector('.replay-bus__pill')
      ?.classList.toggle('replay-bus__pill--stopped', frame.status === 'at_station')
  }
  for (const [id, marker] of markers) {
    if (!seen.has(id)) { marker.remove(); markers.delete(id) }
  }
  if (!fitted && frames.length > 0) {
    map.setView([frames[0]!.latitude, frames[0]!.longitude], 14)
    fitted = true
  }
})

watch(() => props.trails, trails => {
  if (!trailLayer) return
  trailLayer.clearLayers()
  for (const trail of trails) {
    if (trail.points.length > 1) L.polyline(trail.points, { color: props.color, weight: 3, opacity: 0.9, dashArray: '2 6' }).addTo(trailLayer)
  }
})
</script>

<style>
.replay-map { width: 100%; height: 100%; min-height: 320px; border-radius: 12px; overflow: hidden; }
.replay-tiles { filter: saturate(0.35) sepia(0.12) brightness(1.03) contrast(0.9); }
.dark .replay-tiles { filter: invert(1) hue-rotate(180deg) saturate(0.25) brightness(0.82) contrast(0.95); }
.replay-bus { background: transparent !important; border: 0 !important; }
.replay-bus__pill {
  display: inline-flex; align-items: center; justify-content: center; min-width: 30px; height: 26px; padding: 0 7px;
  border-radius: 8px; color: #fff; font: 800 13px/1 var(--font-display, sans-serif);
  border: 2px solid #fff; box-shadow: 0 1px 4px rgba(0, 0, 0, 0.45);
  transition: opacity 0.2s;
}
.replay-bus__pill--stopped { opacity: 0.65; }
</style>
