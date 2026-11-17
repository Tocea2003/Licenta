// Logica pură a redării istoricului: parsare, piste per autobuz, interpolare între eșantioane, timp.

/** O poziție salvată în istoric (GET /api/live/history). */
export interface ReplayPoint {
  id: string
  routeId: number
  latitude: number
  longitude: number
  /** grade, 0 = nord */
  heading: number
  /** km/h */
  speed: number
  /** secunde Unix */
  timestamp: number
  occupancy?: number
  status?: string
}

/** Pozițiile unui autobuz, în ordine cronologică. */
export interface ReplayTrack {
  id: string
  routeId: number
  points: ReplayPoint[]
}

/** Starea unui autobuz la un moment dat (interpolată între două eșantioane). */
export interface ReplayFrame {
  id: string
  routeId: number
  latitude: number
  longitude: number
  heading: number
  speed: number
  occupancy?: number
  status?: string
}

export const REPLAY_SPEEDS = [1, 5, 15, 60] as const
/** Peste acest interval fără eșantioane nu mai interpolăm: autobuzul nu a raportat, deci nu-l afișăm. */
export const MAX_GAP_SECONDS = 90

/** Poziții primite de la backend; intrările invalide se ignoră, `null` = lipsă. */
export function parseHistory(raw: unknown): ReplayPoint[] {
  if (!Array.isArray(raw)) return []
  const points: ReplayPoint[] = []
  for (const item of raw) {
    if (!item || typeof item !== 'object') continue
    const p = item as Record<string, unknown>
    if (typeof p.id !== 'string' || typeof p.latitude !== 'number' || typeof p.longitude !== 'number'
      || typeof p.timestamp !== 'number' || typeof p.routeId !== 'number') continue
    points.push({
      id: p.id,
      routeId: p.routeId,
      latitude: p.latitude,
      longitude: p.longitude,
      heading: typeof p.heading === 'number' ? p.heading : 0,
      speed: typeof p.speed === 'number' ? p.speed : 0,
      timestamp: p.timestamp,
      occupancy: typeof p.occupancy === 'number' ? p.occupancy : undefined,
      status: typeof p.status === 'string' ? p.status : undefined
    })
  }
  return points
}

/** Grupează pozițiile pe autobuz și le ordonează cronologic. */
export function buildTracks(points: ReplayPoint[]): ReplayTrack[] {
  const byId = new Map<string, ReplayTrack>()
  for (const point of points) {
    let track = byId.get(point.id)
    if (!track) byId.set(point.id, track = { id: point.id, routeId: point.routeId, points: [] })
    track.points.push(point)
  }
  const tracks = [...byId.values()]
  for (const track of tracks) track.points.sort((a, b) => a.timestamp - b.timestamp)
  return tracks.sort((a, b) => a.id.localeCompare(b.id))
}

/** Primul și ultimul moment din istoric (secunde Unix), sau null dacă e gol. */
export function timeBounds(tracks: ReplayTrack[]): { start: number; end: number } | null {
  let start = Infinity
  let end = -Infinity
  for (const track of tracks) {
    const first = track.points[0]
    const last = track.points[track.points.length - 1]
    if (!first || !last) continue
    start = Math.min(start, first.timestamp)
    end = Math.max(end, last.timestamp)
  }
  return start === Infinity ? null : { start, end }
}

/** Interpolare pe cel mai scurt arc (350° -> 10° trece prin 0°, nu prin 180°). */
export function interpolateHeading(from: number, to: number, fraction: number): number {
  const delta = ((to - from + 540) % 360) - 180
  return (from + delta * fraction + 360) % 360
}

/**
 * Poziția unui autobuz la momentul `t`: interpolată liniar între cele două eșantioane care îl încadrează.
 * `null` dacă `t` e în afara pistei sau într-un gol mai mare de `maxGap` secunde (autobuzul nu raporta).
 */
export function frameForTrack(track: ReplayTrack, t: number, maxGap = MAX_GAP_SECONDS): ReplayFrame | null {
  const pts = track.points
  if (pts.length === 0 || t < pts[0]!.timestamp || t > pts[pts.length - 1]!.timestamp) return null

  // căutare binară: ultimul eșantion cu timestamp <= t
  let lo = 0
  let hi = pts.length - 1
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1
    if (pts[mid]!.timestamp <= t) lo = mid
    else hi = mid - 1
  }
  const a = pts[lo]!
  const b = pts[lo + 1]
  if (!b || b.timestamp === a.timestamp) return toFrame(a)
  if (b.timestamp - a.timestamp > maxGap) return t - a.timestamp <= maxGap / 2 ? toFrame(a) : t >= b.timestamp - maxGap / 2 ? toFrame(b) : null

  const f = (t - a.timestamp) / (b.timestamp - a.timestamp)
  return {
    id: a.id,
    routeId: a.routeId,
    latitude: a.latitude + (b.latitude - a.latitude) * f,
    longitude: a.longitude + (b.longitude - a.longitude) * f,
    heading: interpolateHeading(a.heading, b.heading, f),
    speed: a.speed + (b.speed - a.speed) * f,
    occupancy: a.occupancy,
    status: a.status
  }
}

function toFrame(p: ReplayPoint): ReplayFrame {
  return { id: p.id, routeId: p.routeId, latitude: p.latitude, longitude: p.longitude, heading: p.heading, speed: p.speed, occupancy: p.occupancy, status: p.status }
}

/** Toți autobuzii vizibili la momentul `t`. */
export function framesAt(tracks: ReplayTrack[], t: number, maxGap = MAX_GAP_SECONDS): ReplayFrame[] {
  const frames: ReplayFrame[] = []
  for (const track of tracks) {
    const frame = frameForTrack(track, t, maxGap)
    if (frame) frames.push(frame)
  }
  return frames
}

/** Coada traseului unui autobuz: pozițiile din ultimele `seconds` secunde până la `t`, plus poziția curentă. */
export function trailFor(track: ReplayTrack, t: number, seconds: number): Array<[number, number]> {
  const current = frameForTrack(track, t)
  if (!current) return []
  const trail: Array<[number, number]> = track.points
    .filter(p => p.timestamp >= t - seconds && p.timestamp <= t)
    .map(p => [p.latitude, p.longitude])
  trail.push([current.latitude, current.longitude])
  return trail
}

/** Avansează capul de redare cu `dtMs` milisecunde reale, la viteza dată; se oprește la sfârșit. */
export function advancePlayhead(playhead: number, dtMs: number, speed: number, end: number): { t: number; ended: boolean } {
  const next = playhead + (dtMs / 1000) * speed
  return next >= end ? { t: end, ended: true } : { t: next, ended: false }
}

/** Ora locală HH:MM:SS dintr-un timestamp Unix (secunde). */
export function formatClock(timestamp: number): string {
  const d = new Date(timestamp * 1000)
  return [d.getHours(), d.getMinutes(), d.getSeconds()].map(n => String(n).padStart(2, '0')).join(':')
}

/** 3725 -> "1:02:05"; 125 -> "2:05" */
export function formatElapsed(seconds: number): string {
  const s = Math.max(0, Math.round(seconds))
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = String(s % 60).padStart(2, '0')
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${sec}` : `${m}:${sec}`
}

export type ReplayWindow = '15' | '60' | '180' | 'latest'

/** Intervalul de încărcat: ultimele N minute, sau ultima oră cu date din `bounds` pentru „latest”. */
export function windowRange(
  window: ReplayWindow, nowSeconds: number, bounds?: { first: number; last: number } | null
): { from: number; to: number } | null {
  if (window === 'latest') {
    if (!bounds) return null
    return { from: Math.max(bounds.first, bounds.last - 3600), to: bounds.last + 1 }
  }
  return { from: nowSeconds - Number(window) * 60, to: nowSeconds }
}
