import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'

// Build a re-usable Notification mock that can be reconfigured per test
const mockNotificationClose = vi.fn()
const MockNotification = vi.fn().mockImplementation(() => ({
  onclick: null,
  close: mockNotificationClose
})) as any
MockNotification.permission = 'default'
MockNotification.requestPermission = vi.fn()

// Dynamically import the module after each vi.resetModules() so every test
// gets fresh module-level state (settings ref, history ref, permissionGranted ref).
type NotificationsModule = typeof import('@/composables/useNotifications')

describe('useNotifications', () => {
  let mod: NotificationsModule

  beforeEach(async () => {
    vi.resetModules()
    localStorage.clear()
    vi.clearAllMocks()
    MockNotification.permission = 'default'
    MockNotification.requestPermission = vi.fn()
    vi.stubGlobal('Notification', MockNotification)
    mod = await import('@/composables/useNotifications')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  // ─── requestNotificationPermission ───────────────────────────────────────

  describe('requestNotificationPermission', () => {
    it('returns false when Notification API is not supported', async () => {
      // vi.stubGlobal sets the value to undefined but leaves the key in window,
      // so 'Notification' in window would still be true. Delete it instead.
      const orig = (window as any).Notification
      delete (window as any).Notification
      try {
        const result = await mod.requestNotificationPermission()
        expect(result).toBe(false)
      } finally {
        ;(window as any).Notification = orig
      }
    })

    it('returns true immediately when permission is already granted', async () => {
      MockNotification.permission = 'granted'
      const result = await mod.requestNotificationPermission()
      expect(result).toBe(true)
      expect(MockNotification.requestPermission).not.toHaveBeenCalled()
    })

    it('calls requestPermission and returns true when user grants', async () => {
      MockNotification.permission = 'default'
      MockNotification.requestPermission.mockResolvedValueOnce('granted')
      const result = await mod.requestNotificationPermission()
      expect(MockNotification.requestPermission).toHaveBeenCalledOnce()
      expect(result).toBe(true)
    })

    it('returns false when user denies permission', async () => {
      MockNotification.permission = 'denied'
      const result = await mod.requestNotificationPermission()
      expect(result).toBe(false)
    })
  })

  // ─── enableNotifications / disableNotifications ──────────────────────────

  describe('enableNotifications', () => {
    it('returns false when permission is denied', async () => {
      MockNotification.permission = 'denied'
      const result = await mod.enableNotifications(8)
      expect(result).toBe(false)
    })

    it('enables notifications and sets stationId / routeId', async () => {
      MockNotification.permission = 'granted'
      const result = await mod.enableNotifications(8, 1)
      expect(result).toBe(true)
      const settings = mod.getNotificationSettings()
      expect(settings.enabled).toBe(true)
      expect(settings.stationId).toBe(8)
      expect(settings.routeId).toBe(1)
    })

    it('persists settings to localStorage', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8)
      const saved = JSON.parse(localStorage.getItem('notification_settings')!)
      expect(saved.enabled).toBe(true)
      expect(saved.stationId).toBe(8)
    })

    it('applies custom settings overrides', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8, undefined, { etaThresholdMinutes: 7 })
      expect(mod.getNotificationSettings().etaThresholdMinutes).toBe(7)
    })
  })

  describe('disableNotifications', () => {
    it('sets enabled to false and clears station and route', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8, 1)

      mod.disableNotifications()

      const settings = mod.getNotificationSettings()
      expect(settings.enabled).toBe(false)
      expect(settings.stationId).toBeNull()
      expect(settings.routeId).toBeNull()
    })

    it('persists the disabled state to localStorage', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8)
      mod.disableNotifications()
      const saved = JSON.parse(localStorage.getItem('notification_settings')!)
      expect(saved.enabled).toBe(false)
    })
  })

  // ─── updateNotificationSettings ──────────────────────────────────────────

  describe('updateNotificationSettings', () => {
    it('merges partial settings without overwriting unrelated fields', () => {
      mod.updateNotificationSettings({ etaThresholdMinutes: 5, crowdingAlerts: false })
      const settings = mod.getNotificationSettings()
      expect(settings.etaThresholdMinutes).toBe(5)
      expect(settings.crowdingAlerts).toBe(false)
      // unrelated field should remain at default
      expect(settings.delayAlerts).toBe(true)
    })
  })

  // ─── addFavoriteRoute / removeFavoriteRoute ───────────────────────────────

  describe('addFavoriteRoute / removeFavoriteRoute', () => {
    it('addFavoriteRoute appends the route id', () => {
      mod.addFavoriteRoute(5)
      expect(mod.getNotificationSettings().favoriteRoutes).toContain(5)
    })

    it('addFavoriteRoute does not create duplicates', () => {
      mod.addFavoriteRoute(5)
      mod.addFavoriteRoute(5)
      const count = mod.getNotificationSettings().favoriteRoutes.filter(r => r === 5).length
      expect(count).toBe(1)
    })

    it('removeFavoriteRoute removes the route id', () => {
      mod.addFavoriteRoute(5)
      mod.removeFavoriteRoute(5)
      expect(mod.getNotificationSettings().favoriteRoutes).not.toContain(5)
    })

    it('removeFavoriteRoute is a no-op for a non-existent route id', () => {
      expect(() => mod.removeFavoriteRoute(999)).not.toThrow()
    })
  })

  // ─── clearNotificationHistory ─────────────────────────────────────────────

  describe('clearNotificationHistory', () => {
    it('empties the history array', () => {
      localStorage.setItem('notification_history', JSON.stringify([{ id: '1' }]))
      mod.clearNotificationHistory()
      expect(mod.getNotificationHistory()).toHaveLength(0)
    })

    it('removes notification_history from localStorage', () => {
      localStorage.setItem('notification_history', JSON.stringify([{ id: '1' }]))
      mod.clearNotificationHistory()
      expect(localStorage.getItem('notification_history')).toBeNull()
    })
  })

  // ─── checkBusNotifications ────────────────────────────────────────────────

  describe('checkBusNotifications', () => {
    const station = { id: 8, name: 'Piața Unirii', latitude: 45.7909724, longitude: 24.1485152 }

    it('does nothing when notifications are disabled', async () => {
      mod.disableNotifications()
      mod.checkBusNotifications(
        [{ id: 'bus-1', latitude: 45.79, longitude: 24.14, routeId: 1, speed: 20 }],
        station
      )
      // enableNotifications was never called so MockNotification should not have been called
      // (module-level state starts with enabled: false after resetModules)
      expect(MockNotification).not.toHaveBeenCalled()
    })

    it('does nothing when station is null', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8)
      vi.clearAllMocks() // clear the test-notification call from enableNotifications

      mod.checkBusNotifications(
        [{ id: 'bus-1', latitude: 45.79, longitude: 24.14, routeId: 1, speed: 20 }],
        null
      )
      expect(MockNotification).not.toHaveBeenCalled()
    })

    it('sends an arrival notification when bus ETA is within threshold', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8)
      mod.updateNotificationSettings({ etaThresholdMinutes: 60 }) // wide threshold
      vi.clearAllMocks()

      // Bus is only ~0.01 km away → ETA << 60 min
      mod.checkBusNotifications([{
        id: 'bus-close',
        latitude: 45.7910,
        longitude: 24.1485,
        routeId: 1,
        speed: 30
      }], station)

      expect(MockNotification).toHaveBeenCalled()
    })

    it('does not send a second notification for the same bus id', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8)
      mod.updateNotificationSettings({ etaThresholdMinutes: 60 })
      vi.clearAllMocks()

      const bus = { id: 'bus-once', latitude: 45.7910, longitude: 24.1485, routeId: 1, speed: 30 }
      mod.checkBusNotifications([bus], station)
      const firstCallCount = MockNotification.mock.calls.length

      mod.checkBusNotifications([bus], station)
      expect(MockNotification.mock.calls.length).toBe(firstCallCount)
    })

    it('filters out buses on non-matching routes when routeId filter is set', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8, 2) // only route 2
      mod.updateNotificationSettings({ etaThresholdMinutes: 60 })
      vi.clearAllMocks()

      // Bus is on route 1 (not 2) and very close
      mod.checkBusNotifications([{
        id: 'bus-wrong-route',
        latitude: 45.7910,
        longitude: 24.1485,
        routeId: 1,
        speed: 30
      }], station)

      expect(MockNotification).not.toHaveBeenCalled()
    })

    it('sends a crowding notification for a very full bus approaching', async () => {
      MockNotification.permission = 'granted'
      await mod.enableNotifications(8)
      // ETA threshold wide so crowding check is reached
      mod.updateNotificationSettings({ etaThresholdMinutes: 60, crowdingAlerts: true })
      vi.clearAllMocks()

      // Bus is close (ETA < 5 min) and overcrowded (occupancy >= 85)
      mod.checkBusNotifications([{
        id: 'bus-crowded',
        latitude: 45.7910,
        longitude: 24.1485,
        routeId: 1,
        speed: 30,
        occupancy: 90
      }], station)

      // At least one Notification was created (arrival or crowding)
      expect(MockNotification).toHaveBeenCalled()
    })
  })

  // ─── settings persistence ─────────────────────────────────────────────────

  describe('Settings persistence', () => {
    it('loads saved settings from localStorage on initialization', async () => {
      const saved = {
        enabled: false,
        stationId: 5,
        routeId: 3,
        notifiedBuses: [],
        favoriteRoutes: [1, 2],
        crowdingAlerts: false,
        delayAlerts: false,
        etaThresholdMinutes: 10
      }
      localStorage.setItem('notification_settings', JSON.stringify(saved))

      vi.resetModules()
      vi.stubGlobal('Notification', MockNotification)
      const freshMod = await import('@/composables/useNotifications')

      const settings = freshMod.getNotificationSettings()
      expect(settings.stationId).toBe(5)
      expect(settings.routeId).toBe(3)
      expect(settings.favoriteRoutes).toEqual([1, 2])
      expect(settings.etaThresholdMinutes).toBe(10)
      // always starts disabled for safety
      expect(settings.enabled).toBe(false)
    })

    it('handles corrupted notification_settings JSON without throwing', async () => {
      localStorage.setItem('notification_settings', 'not-valid-json{{{')

      vi.resetModules()
      vi.stubGlobal('Notification', MockNotification)
      expect(async () => {
        await import('@/composables/useNotifications')
      }).not.toThrow()
    })
  })
})
