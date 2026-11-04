// Culorile liniilor din GTFS sunt adesea primare saturate (#00FF40, #0000FF) pe care
// numărul alb al liniei nu se citește. Păstrăm nuanța, dar aducem saturația și
// luminozitatea într-un interval comun, astfel încât textul alb să aibă contrast ≥ 4.5:1.

const FALLBACK = '#5f6672'
const MIN_CONTRAST_WITH_WHITE = 4.5
const MAX_SATURATION = 0.7
const MAX_LIGHTNESS = 0.46
const MIN_LIGHTNESS = 0.22

function parseHex(hex: string): [number, number, number] | null {
  const m = /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return null
  let h = m[1]!
  if (h.length === 3) h = h.split('').map(c => c + c).join('')
  return [0, 2, 4].map(i => parseInt(h.slice(i, i + 2), 16) / 255) as [number, number, number]
}

function toHex([r, g, b]: [number, number, number]): string {
  return '#' + [r, g, b].map(v => Math.round(v * 255).toString(16).padStart(2, '0')).join('')
}

function rgbToHsl([r, g, b]: [number, number, number]): [number, number, number] {
  const max = Math.max(r, g, b)
  const min = Math.min(r, g, b)
  const l = (max + min) / 2
  if (max === min) return [0, 0, l]
  const d = max - min
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
  let h: number
  if (max === r) h = (g - b) / d + (g < b ? 6 : 0)
  else if (max === g) h = (b - r) / d + 2
  else h = (r - g) / d + 4
  return [h / 6, s, l]
}

function hslToRgb([h, s, l]: [number, number, number]): [number, number, number] {
  if (s === 0) return [l, l, l]
  const hue = (p: number, q: number, t: number) => {
    if (t < 0) t += 1
    if (t > 1) t -= 1
    if (t < 1 / 6) return p + (q - p) * 6 * t
    if (t < 1 / 2) return q
    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6
    return p
  }
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s
  const p = 2 * l - q
  return [hue(p, q, h + 1 / 3), hue(p, q, h), hue(p, q, h - 1 / 3)]
}

function relativeLuminance([r, g, b]: [number, number, number]): number {
  const lin = (c: number) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4)
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)
}

/** Raportul de contrast WCAG dintre o culoare și alb. */
export function contrastWithWhite(hex: string): number {
  const rgb = parseHex(hex)
  if (!rgb) return 1
  return 1.05 / (relativeLuminance(rgb) + 0.05)
}

const cache = new Map<string, string>()

/** Varianta „Semnal” a unei culori de linie: aceeași nuanță, lizibilă cu text alb. */
export function normalizeLineColor(hex: string | null | undefined): string {
  if (!hex) return FALLBACK
  const key = hex.toLowerCase()
  const cached = cache.get(key)
  if (cached) return cached

  const rgb = parseHex(hex)
  if (!rgb) return FALLBACK

  const [h, s, l] = rgbToHsl(rgb)
  const sat = Math.min(s, MAX_SATURATION)
  let light = Math.min(l, MAX_LIGHTNESS)
  let result = toHex(hslToRgb([h, sat, light]))
  while (contrastWithWhite(result) < MIN_CONTRAST_WITH_WHITE && light > MIN_LIGHTNESS) {
    light -= 0.02
    result = toHex(hslToRgb([h, sat, light]))
  }

  cache.set(key, result)
  return result
}

/**
 * Normalizează, recursiv, câmpurile `color` / `routeColor` care conțin culori hex
 * dintr-un răspuns API (rute, segmente de călătorie, orare).
 */
export function normalizeLineColorsDeep<T>(data: T): T {
  if (Array.isArray(data)) {
    data.forEach(item => normalizeLineColorsDeep(item))
  } else if (data && typeof data === 'object') {
    const record = data as Record<string, unknown>
    for (const [key, value] of Object.entries(record)) {
      if ((key === 'color' || key === 'routeColor') && typeof value === 'string' && parseHex(value)) {
        record[key] = normalizeLineColor(value)
      } else if (value && typeof value === 'object') {
        normalizeLineColorsDeep(value)
      }
    }
  }
  return data
}
