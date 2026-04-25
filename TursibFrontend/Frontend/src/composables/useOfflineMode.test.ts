import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'

// The composable registers window event listeners at module-load time,
// so we test each area in a way that is resilient to accumulated listeners.
// We reset modules in beforeEach so every test starts with a fresh isOnline ref.

type OfflineModule = typeof import('@/composables/useOfflineMode')

describe('useOfflineMode', () => {
  let mod: OfflineModule

  beforeEach(async () => {
    vi.resetModules()
    // Start each test online
    Object.defineProperty(navigator, 'onLine', {
      value: true,
      writable: true,
      configurable: true
    })
    mod = await import('@/composables/useOfflineMode')
  })

  afterEach(() => {
    vi.clearAllMocks()
    vi.unstubAllGlobals()
  })

  // ─── reactive state ───────────────────────────────────────────────────────

  describe('initial state', () => {
    it('isOnline reflects navigator.onLine (true) at startup', () => {
      const { isOnline } = mod.useOfflineMode()
      expect(isOnline.value).toBe(true)
    })

    it('isOnline reflects navigator.onLine (false) at startup', async () => {
      Object.defineProperty(navigator, 'onLine', { value: false, configurable: true })
      vi.resetModules()
      const freshMod: OfflineModule = await import('@/composables/useOfflineMode')
      const { isOnline } = freshMod.useOfflineMode()
      expect(isOnline.value).toBe(false)
    })

    it('isSyncing is false initially', () => {
      const { isSyncing } = mod.useOfflineMode()
      expect(isSyncing.value).toBe(false)
    })

    it('lastSyncTime is null initially', () => {
      const { lastSyncTime } = mod.useOfflineMode()
      expect(lastSyncTime.value).toBeNull()
    })
  })

  // ─── online / offline events ──────────────────────────────────────────────

  describe('network event listeners', () => {
    it('sets isOnline to false when the "offline" event fires', () => {
      const { isOnline } = mod.useOfflineMode()
      window.dispatchEvent(new Event('offline'))
      expect(isOnline.value).toBe(false)
    })

    it('sets isOnline back to true when "online" fires after "offline"', () => {
      const { isOnline } = mod.useOfflineMode()
      window.dispatchEvent(new Event('offline'))
      window.dispatchEvent(new Event('online'))
      expect(isOnline.value).toBe(true)
    })
  })

  // ─── syncData ─────────────────────────────────────────────────────────────

  describe('syncData', () => {
    it('skips sync and leaves isSyncing false when offline', async () => {
      const { syncData, isSyncing } = mod.useOfflineMode()
      window.dispatchEvent(new Event('offline'))

      await syncData('http://api.example.com')

      expect(isSyncing.value).toBe(false)
    })

    it('resets isSyncing to false after a failed fetch', async () => {
      const { syncData, isSyncing } = mod.useOfflineMode()
      vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network error')))

      await syncData('http://api.example.com')

      expect(isSyncing.value).toBe(false)
    })

    it('resets isSyncing to false when API returns non-ok status', async () => {
      const { syncData, isSyncing } = mod.useOfflineMode()
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }))

      await syncData('http://api.example.com')

      expect(isSyncing.value).toBe(false)
    })
  })

  // ─── store names constant ─────────────────────────────────────────────────

  describe('STORES constant', () => {
    it('exposes the expected store name keys', () => {
      const { STORES } = mod.useOfflineMode()
      expect(STORES.ROUTES).toBe('routes')
      expect(STORES.STATIONS).toBe('stations')
      expect(STORES.SHAPES).toBe('shapes')
      expect(STORES.METADATA).toBe('metadata')
    })
  })
})
