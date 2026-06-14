/**
 * Shared geocoding service using Nominatim (OpenStreetMap).
 * Supports exact address search including house numbers.
 */

export interface GeocodingResult {
  /** Short readable name: "Strada Victoriei 1, Sibiu" */
  displayName: string
  /** Full Nominatim display_name (for tooltip / fallback) */
  fullAddress: string
  lat: number
  lon: number
  type: 'house' | 'building' | 'road' | 'place' | 'other'
  addressDetails: {
    road?: string
    houseNumber?: string
    suburb?: string
    city?: string
  }
}

// Sibiu bounding box — used as a hint, not a hard boundary
const SIBIU_VIEWBOX = '23.9,45.65,24.45,46.0'

// ─── Icon helpers ─────────────────────────────────────────────────────────────

export function getTypeIcon(type: GeocodingResult['type']): string {
  switch (type) {
    case 'house':    return '🏠'
    case 'building': return '🏢'
    case 'road':     return '🛣️'
    case 'place':    return '📌'
    default:         return '📍'
  }
}

// ─── Input normalization ──────────────────────────────────────────────────────

/** Expand common Romanian abbreviations so Nominatim gets the full word */
export function normalizeAddressQuery(query: string): string {
  return query
    .trim()
    .replace(/\bstr\.?\s+/gi,     'Strada ')
    .replace(/\bbd\.?\s+/gi,      'Bulevardul ')
    .replace(/\bb-dul\s+/gi,      'Bulevardul ')
    .replace(/\bcal\.?\s+/gi,     'Calea ')
    .replace(/\bal\.?\s+/gi,      'Aleea ')
    .replace(/\bp-?ta\.?\s+/gi,   'Piata ')
    .replace(/\bnr\.?\s*/gi,      '')   // strip "nr." abbreviation
    .replace(/\bnumarul\s*/gi,    '')   // strip full word "numarul"
    .replace(/\s{2,}/g,           ' ')  // collapse double spaces left by removals
    .trim()
}

// ─── Internal formatting ──────────────────────────────────────────────────────

function formatDisplayName(item: any): string {
  const a = item.address ?? {}
  const road = a.road ?? a.pedestrian ?? a.footway ?? a.cycleway ?? a.path
  const houseNum = a.house_number
  const quarter  = a.quarter ?? a.suburb ?? a.neighbourhood
  const city     = a.city ?? a.town ?? a.village ?? 'Sibiu'

  if (road && houseNum) {
    return quarter
      ? `${road} ${houseNum}, ${quarter}, ${city}`
      : `${road} ${houseNum}, ${city}`
  }
  if (road) {
    return quarter ? `${road}, ${quarter}, ${city}` : `${road}, ${city}`
  }
  if (item.name && item.name !== item.display_name) {
    return `${item.name}, ${city}`
  }
  // Fallback: first 3 comma-segments of display_name
  return item.display_name
    .split(',')
    .slice(0, 3)
    .map((p: string) => p.trim())
    .join(', ')
}

function classifyType(item: any): GeocodingResult['type'] {
  const a = item.address ?? {}
  if (a.house_number || item.type === 'house') return 'house'
  if (item.class === 'building')                return 'building'
  if (item.class === 'highway')                 return 'road'
  if (item.class === 'place' || item.class === 'boundary' || item.class === 'amenity') return 'place'
  return 'other'
}

// ─── Result ranking ───────────────────────────────────────────────────────────

function rankResults(query: string, results: GeocodingResult[]): GeocodingResult[] {
  const hasDigit = /\d/.test(query)
  const numMatch = query.match(/\d+/)
  const queryNum = numMatch?.[0]

  return [...results].sort((a, b) => {
    let sa = 0, sb = 0

    // Exact house-number match floats to top
    if (hasDigit) {
      if (a.type === 'house') sa += 30
      if (b.type === 'house') sb += 30
      if (queryNum && a.addressDetails.houseNumber === queryNum) sa += 25
      if (queryNum && b.addressDetails.houseNumber === queryNum) sb += 25
    }

    // Prefer results actually located in Sibiu
    if (a.fullAddress.toLowerCase().includes('sibiu')) sa += 8
    if (b.fullAddress.toLowerCase().includes('sibiu')) sb += 8

    return sb - sa
  })
}

// ─── Main API ─────────────────────────────────────────────────────────────────

let _abort: AbortController | null = null

/** Parse a normalized query into structured parts if it contains a house number */
function parseStructured(normalized: string): { street: string; city?: string } | null {
  // Match patterns like: "Strada Unirii 19 Cisnadie" or "Calea Dumbravii 5A Sibiu"
  const m = normalized.match(
    /^(.+?)\s+(\d+[A-Za-z]?)\s+([A-ZĂÂÎȘȚ][a-zăâîșț]+(?:\s+[A-ZĂÂÎȘȚ][a-zăâîșț]+)*)$/
  )
  if (!m) return null
  return { street: `${m[2]} ${m[1]}`, city: m[3] }
}

async function nominatimFetch(params: URLSearchParams, signal: AbortSignal): Promise<any[]> {
  try {
    const res = await fetch(
      `https://nominatim.openstreetmap.org/search?${params}`,
      { signal, headers: { Accept: 'application/json' } }
    )
    return await res.json()
  } catch {
    return []
  }
}

function toResults(data: any[]): GeocodingResult[] {
  return data.map(item => ({
    displayName:    formatDisplayName(item),
    fullAddress:    item.display_name,
    lat:            parseFloat(item.lat),
    lon:            parseFloat(item.lon),
    type:           classifyType(item),
    addressDetails: {
      road:        item.address?.road ?? item.address?.pedestrian ?? item.address?.footway,
      houseNumber: item.address?.house_number,
      suburb:      item.address?.suburb ?? item.address?.neighbourhood,
      city:        item.address?.city   ?? item.address?.town ?? item.address?.village
    }
  }))
}

/**
 * Search for addresses using Nominatim.
 * Runs a free-form query + (when a house number is detected) a parallel
 * structured query, then merges, deduplicates and ranks.
 */
export async function reverseGeocode(lat: number, lon: number): Promise<string> {
  try {
    const res = await fetch(
      `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lon}&addressdetails=1&accept-language=ro`,
      { headers: { Accept: 'application/json' } }
    )
    const data = await res.json()
    if (data?.address) {
      const a = data.address
      const road = a.road ?? a.pedestrian ?? a.footway ?? a.cycleway ?? a.path
      const houseNum = a.house_number
      const city = a.city ?? a.town ?? a.village ?? ''
      if (road && houseNum) return `${road} ${houseNum}, ${city}`
      if (road) return `${road}, ${city}`
      if (data.display_name) return data.display_name.split(',').slice(0, 3).map((s: string) => s.trim()).join(', ')
    }
    return `${lat.toFixed(5)}, ${lon.toFixed(5)}`
  } catch {
    return `${lat.toFixed(5)}, ${lon.toFixed(5)}`
  }
}

export async function searchAddresses(query: string): Promise<GeocodingResult[]> {
  if (query.trim().length < 2) return []

  if (_abort) _abort.abort()
  _abort = new AbortController()
  const signal = _abort.signal

  const normalized  = normalizeAddressQuery(query)
  const wordCount   = normalized.split(/\s+/).filter(Boolean).length
  // 4+ word queries already carry a city name — just add Romania to avoid
  // "Cisnadie, Sibiu, Romania" confusing Nominatim.
  const fullQuery = /romania/i.test(normalized)
    ? normalized
    : wordCount >= 4
      ? `${normalized}, Romania`
      : `${normalized}, Sibiu, Romania`

  // ── Free-form query ────────────────────────────────────────────────────────
  const freeParams = new URLSearchParams({
    format: 'json', q: fullQuery, limit: '6',
    addressdetails: '1', countrycodes: 'ro',
    viewbox: SIBIU_VIEWBOX, bounded: '0',
    'accept-language': 'ro'
  })

  // ── Structured query (parallel, only when house number detected) ───────────
  const structured = parseStructured(normalized)
  const structParams = structured
    ? new URLSearchParams({
        format: 'json', limit: '4', addressdetails: '1',
        countrycodes: 'ro', 'accept-language': 'ro',
        street:  structured.street,
        city:    structured.city ?? 'Sibiu',
        country: 'Romania'
      })
    : null

  try {
    const [freeData, structData] = await Promise.all([
      nominatimFetch(freeParams, signal),
      structParams ? nominatimFetch(structParams, signal) : Promise.resolve([])
    ])

    const all = toResults([...structData, ...freeData])   // structured first (more precise)

    // Deduplicate by displayName
    const seen = new Set<string>()
    const unique = all.filter(r => {
      if (seen.has(r.displayName)) return false
      seen.add(r.displayName)
      return true
    })

    return rankResults(normalized, unique)
  } catch (err: any) {
    if (err?.name === 'AbortError') return []
    console.warn('⚠️ Geocoding error:', err?.message)
    return []
  }
}
