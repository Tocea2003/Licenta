// Configurarea sursei de poziții live (SignalR din backend sau Firebase).

export type LiveSource = 'signalr' | 'firebase'

/** URL-ul hub-ului SignalR, derivat din VITE_API_URL (".../api" → ".../hubs/vehicles"). */
export function liveHubUrl(apiUrl: string): string {
  return apiUrl.replace(/\/+$/, '').replace(/\/api$/, '') + '/hubs/vehicles'
}

/**
 * VITE_LIVE_SOURCE:
 * - "auto" (implicit): pornește pe Firebase și trece pe SignalR când hub-ul trimite date;
 * - "firebase": doar Firebase (fără încercare SignalR).
 */
export function signalrEnabled(setting: string | undefined): boolean {
  return (setting ?? 'auto').trim().toLowerCase() !== 'firebase'
}
