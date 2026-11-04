import { describe, it, expect } from 'vitest'
import { normalizeLineColor, contrastWithWhite, normalizeLineColorsDeep } from './lineColors'

// Culori reale din feed-ul GTFS Tursib
const GTFS_COLORS = ['#ff2e2e', '#00ff40', '#00b700', '#0000ff', '#ba71cc', '#ff97ff', '#9c4e4e', '#188363', '#008040', '#808080', '#ffff00', '#ff00ff', '#c0c0c0']

describe('normalizeLineColor', () => {
  it.each(GTFS_COLORS)('makes %s readable with white text (contrast ≥ 4.5)', hex => {
    expect(contrastWithWhite(normalizeLineColor(hex))).toBeGreaterThanOrEqual(4.5)
  })

  it('keeps the hue family (red stays red, blue stays blue)', () => {
    const red = normalizeLineColor('#ff2e2e')
    const blue = normalizeLineColor('#0000ff')
    expect(parseInt(red.slice(1, 3), 16)).toBeGreaterThan(parseInt(red.slice(5, 7), 16))
    expect(parseInt(blue.slice(5, 7), 16)).toBeGreaterThan(parseInt(blue.slice(1, 3), 16))
  })

  it('leaves an already dark, readable color almost unchanged', () => {
    expect(normalizeLineColor('#188363')).toBe('#188363')
  })

  it('falls back to neutral grey for missing or invalid input', () => {
    expect(normalizeLineColor(undefined)).toBe('#5f6672')
    expect(normalizeLineColor('not-a-color')).toBe('#5f6672')
  })

  it('accepts 3-digit hex', () => {
    expect(contrastWithWhite(normalizeLineColor('#0f0'))).toBeGreaterThanOrEqual(4.5)
  })
})

describe('normalizeLineColorsDeep', () => {
  it('normalizes color and routeColor fields at any depth', () => {
    const data = [{ id: 1, color: '#00ff40', segments: [{ type: 'bus', color: '#0000ff' }] }, { routeColor: '#ff97ff', name: '#ffffff not a color field' }]
    normalizeLineColorsDeep(data)
    expect(data[0]!.color).not.toBe('#00ff40')
    expect(data[0]!.segments![0]!.color).not.toBe('#0000ff')
    expect(data[1]!.routeColor).not.toBe('#ff97ff')
    expect(data[1]!.name).toBe('#ffffff not a color field')
  })

  it('ignores non-hex values', () => {
    const data = { color: 'red' }
    expect(normalizeLineColorsDeep(data).color).toBe('red')
  })
})
