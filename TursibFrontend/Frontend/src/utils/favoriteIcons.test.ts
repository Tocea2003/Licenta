import { describe, it, expect } from 'vitest'
import { favoriteIconKey } from './favoriteIcons'

describe('favoriteIconKey', () => {
  it('keeps new-style keys', () => {
    expect(favoriteIconKey('cafe')).toBe('cafe')
  })

  it('maps legacy emoji to keys', () => {
    expect(favoriteIconKey('🏠')).toBe('home')
    expect(favoriteIconKey('☕')).toBe('cafe')
    expect(favoriteIconKey('🚏')).toBe('pin')
  })

  it('falls back to the favorite type, then to a pin', () => {
    expect(favoriteIconKey(undefined, 'work')).toBe('work')
    expect(favoriteIconKey('🦄')).toBe('pin')
  })
})
