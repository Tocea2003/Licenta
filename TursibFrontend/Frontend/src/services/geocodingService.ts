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
    .replace(/\bstr\.?\s+/gi,  'Strada ')
    .replace(/\bbd\.?\s+/gi,   'Bulevardul ')
    .replace(/\bb-dul\s+/gi,   'Bulevardul ')
    .replace(/\bcal\.?\s+/gi,  'Calea ')
    .replace(/\bal\.?\s+/gi,   'Aleea ')
    .replace(/\bp-?ta\.?\s+/gi,'Piata ')
    .replace(/\bnr\.?\s*/gi,   '')   // strip "nr." prefix — the number alone is better
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

/**
 * Search for addresses using Nominatim.
 * Returns up to 8 results, ranked by relevance (house numbers first).
 * No bbox filtering — every precise address is accepted.
 */
export async function searchAddresses(query: string): Promise<GeocodingResult[]> {
  if (query.trim().length < 2) return []

  // Cancel any in-flight request
  if (_abort) _abort.abort()
  _abort = new AbortController()

  const normalized = normalizeAddressQuery(query)
  const fullQuery  = /romania/i.test(normalized)
    ? normalized
    : `${normalized}, Sibiu, Romania`

  const params = new URLSearchParams({
    format:          'json',
    q:               fullQuery,
    limit:           '8',
    addressdetails:  '1',
    countrycodes:    'ro',
    viewbox:         SIBIU_VIEWBOX,
    bounded:         '0',      // viewbox is a hint, not a hard limit
    'accept-language': 'ro'
  })

  try {
    const res = await fetch(
      `https://nominatim.openstreetmap.org/search?${params}`,
      { signal: _abort.signal, headers: { Accept: 'application/json' } }
    )
    const data: any[] = await res.json()

    const results: GeocodingResult[] = data.map(item => ({
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

    // Deduplicate by displayName (Nominatim sometimes returns the same place twice)
    const seen = new Set<string>()
    const unique = results.filter(r => {
      if (seen.has(r.displayName)) return false
      seen.add(r.displayName)
      return true
    })

    return rankResults(normalized, unique)
  } catch (err: any) {
    if (err?.name === 'AbortError') return []
    if (err instanceof TypeError && err.message.includes('fetch')) {
      console.warn('⚠️ Geocoding: network error, will retry on next input')
      return []
    }
    console.error('❌ Geocoding error:', err?.message)
    return []
  }
}
