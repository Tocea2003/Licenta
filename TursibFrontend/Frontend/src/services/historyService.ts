import { parseHistory, type ReplayPoint } from '@/utils/replay'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

/** De ce nu avem istoric: backend-ul nu îl înregistrează, cererea e invalidă sau serverul nu răspunde. */
export class HistoryError extends Error {
  constructor(public readonly kind: 'disabled' | 'invalid' | 'network' | 'unauthorized', message: string) {
    super(message)
    this.name = 'HistoryError'
  }
}

/** Istoricul e doar pentru administratori: trimitem tokenul sesiunii de admin. */
function authHeaders(): HeadersInit {
  const token = localStorage.getItem('admin_token') || localStorage.getItem('token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function getJson(url: string): Promise<{ status: number; body: unknown }> {
  let response: Response
  try {
    response = await fetch(url, { headers: authHeaders() })
  } catch {
    throw new HistoryError('network', 'Serverul nu răspunde')
  }
  if (response.status === 401 || response.status === 403) {
    throw new HistoryError('unauthorized', 'Sesiunea de administrator a expirat')
  }
  if (response.status === 503) throw new HistoryError('disabled', 'Istoricul nu este activat pe server')
  if (response.status === 400) {
    const problem = await response.json().catch(() => null) as { message?: string } | null
    throw new HistoryError('invalid', problem?.message ?? 'Cerere invalidă')
  }
  if (response.status === 204) return { status: 204, body: null }
  if (!response.ok) throw new HistoryError('network', `Eroare ${response.status}`)
  return { status: response.status, body: await response.json() }
}

/** Pozițiile unei linii dintr-un interval (secunde Unix), în ordine cronologică. */
export async function fetchHistory(routeId: number, fromSeconds: number, toSeconds: number): Promise<ReplayPoint[]> {
  const params = new URLSearchParams({
    routeId: String(routeId),
    from: new Date(fromSeconds * 1000).toISOString(),
    to: new Date(toSeconds * 1000).toISOString(),
    limit: '20000'
  })
  const { body } = await getJson(`${API_BASE_URL}/live/history?${params}`)
  return parseHistory(body)
}

/** Primul și ultimul moment pentru care există istoric (null dacă nu există nimic). */
export async function fetchHistoryBounds(routeId: number): Promise<{ first: number; last: number } | null> {
  const { status, body } = await getJson(`${API_BASE_URL}/live/history/bounds?routeId=${routeId}`)
  if (status === 204) return null
  const b = body as { first?: unknown; last?: unknown }
  return typeof b.first === 'number' && typeof b.last === 'number' ? { first: b.first, last: b.last } : null
}
