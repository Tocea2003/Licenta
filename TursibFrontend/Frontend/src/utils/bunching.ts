import type { BunchingAlert } from '@/types/bus'

// Logica pură pentru alertele de bunching: parsare, sortare, formatare.

/** Alertele primite de la backend (SignalR / REST): `null` = lipsă, intrările invalide sunt ignorate. */
export function parseBunchingList(list: unknown): BunchingAlert[] {
  if (!Array.isArray(list)) return []
  const alerts: BunchingAlert[] = []
  for (const item of list) {
    if (!item || typeof item !== 'object') continue
    const a = item as Record<string, unknown>
    if (typeof a.key !== 'string' || typeof a.routeId !== 'number' || typeof a.ratio !== 'number') continue
    alerts.push({
      key: a.key,
      routeId: a.routeId,
      routeNumber: (a.routeNumber as string | null) ?? undefined,
      leaderId: String(a.leaderId ?? ''),
      followerId: String(a.followerId ?? ''),
      gapMeters: Number(a.gapMeters ?? 0),
      idealGapMeters: Number(a.idealGapMeters ?? 0),
      ratio: a.ratio,
      severity: a.severity === 'critical' ? 'critical' : 'warning',
      startedAt: Number(a.startedAt ?? 0),
      endedAt: typeof a.endedAt === 'number' ? a.endedAt : undefined,
      headwaySeconds: typeof a.headwaySeconds === 'number' ? a.headwaySeconds : undefined
    })
  }
  return alerts
}

/** Cele mai grave primele (critice, apoi raportul cel mai mic). */
export function sortAlerts(alerts: BunchingAlert[]): BunchingAlert[] {
  return [...alerts].sort((a, b) => {
    if (a.severity !== b.severity) return a.severity === 'critical' ? -1 : 1
    return a.ratio - b.ratio
  })
}

/** 85 -> "85 m", 1240 -> "1,2 km" */
export function formatGap(meters: number): string {
  if (meters < 1000) return `${Math.round(meters)} m`
  return `${(meters / 1000).toFixed(1).replace('.', ',')} km`
}

/** 45 -> "45 s", 150 -> "2 min 30 s" */
export function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.round(seconds))
  if (s < 60) return `${s} s`
  const minutes = Math.floor(s / 60)
  const rest = s % 60
  return rest === 0 ? `${minutes} min` : `${minutes} min ${rest} s`
}

/** Cât a durat (sau durează) o alertă, în secunde. */
export function alertDurationSeconds(alert: BunchingAlert, nowSeconds: number): number {
  return Math.max(0, (alert.endedAt ?? nowSeconds) - alert.startedAt)
}
