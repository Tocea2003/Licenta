import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { liveHubUrl, signalrEnabled } from '@/utils/liveSource'

// ---- Firebase mock: captures the onValue callback ----
const firebase = vi.hoisted(() => ({
  callback: null as null | ((snap: { val: () => unknown }) => void),
  unsubscribe: vi.fn(),
}))
vi.mock('firebase/database', () => ({
  getDatabase: vi.fn(() => ({})),
  ref: vi.fn(() => ({})),
  onValue: vi.fn((_ref: unknown, cb: (snap: { val: () => unknown }) => void) => {
    firebase.callback = cb
    return firebase.unsubscribe
  }),
}))

// ---- SignalR mock: a fake connection whose handlers the test can fire ----
const hub = vi.hoisted(() => ({
  handlers: {} as Record<string, (arg: unknown) => void>,
  onclose: null as null | (() => void),
  start: vi.fn(),
  stop: vi.fn(),
}))
vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        on: (name: string, handler: (arg: unknown) => void) => { hub.handlers[name] = handler },
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: (handler: () => void) => { hub.onclose = handler },
        start: hub.start,
        stop: hub.stop,
      }
    }
  }
  return { HubConnectionBuilder, LogLevel: { None: 6 } }
})

const { useLiveBusesStore } = await import('./liveBuses')

// The store loads @microsoft/signalr lazily; wait until it has started the connection
const hubStarted = () => vi.waitFor(() => expect(hub.start).toHaveBeenCalled())
const position = (vehicleId: string, latitude = 45.79) => ({
  vehicleId, routeId: 1, latitude, longitude: 24.15, timestamp: 1, speed: 20, heading: 0,
})

describe('liveSource config', () => {
  it('derives the hub URL from the API URL', () => {
    expect(liveHubUrl('http://localhost:5022/api')).toBe('http://localhost:5022/hubs/vehicles')
    expect(liveHubUrl('https://x.onrender.com/api/')).toBe('https://x.onrender.com/hubs/vehicles')
  })

  it('only disables SignalR when explicitly set to firebase', () => {
    expect(signalrEnabled(undefined)).toBe(true)
    expect(signalrEnabled('auto')).toBe(true)
    expect(signalrEnabled(' Firebase ')).toBe(false)
  })
})

describe('liveBuses store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    firebase.callback = null
    firebase.unsubscribe.mockClear()
    hub.handlers = {}
    hub.onclose = null
    hub.start.mockReset().mockResolvedValue(undefined)
    hub.stop.mockReset().mockResolvedValue(undefined)
  })

  it('shows Firebase data immediately', async () => {
    const store = useLiveBusesStore()
    store.subscribe()
    firebase.callback!({ val: () => ({ route_1_bus_0: { latitude: 45.7, longitude: 24.1, routeId: 1 } }) })

    expect(store.source).toBe('firebase')
    expect(store.buses.map(b => b.id)).toEqual(['route_1_bus_0'])
    await hubStarted()
  })

  it('switches to SignalR on the first hub snapshot and stops Firebase', async () => {
    const store = useLiveBusesStore()
    store.subscribe()
    await hubStarted()
    firebase.callback!({ val: () => ({ route_9_bus_0: { latitude: 45.7, longitude: 24.1, routeId: 9 } }) })

    hub.handlers.snapshot!([position('route_1_bus_0'), position('route_1_bus_1')])

    expect(store.source).toBe('signalr')
    expect(store.buses.map(b => b.id).sort()).toEqual(['route_1_bus_0', 'route_1_bus_1'])
    expect(firebase.unsubscribe).toHaveBeenCalled()
  })

  it('applies batched updates and removals from the hub', async () => {
    const store = useLiveBusesStore()
    store.subscribe()
    await hubStarted()
    hub.handlers.snapshot!([position('route_1_bus_0'), position('route_1_bus_1')])

    hub.handlers.positions!([position('route_1_bus_0', 45.8)])
    expect(store.buses.find(b => b.id === 'route_1_bus_0')?.latitude).toBe(45.8)

    hub.handlers.removed!(['route_1_bus_1'])
    expect(store.buses.map(b => b.id)).toEqual(['route_1_bus_0'])
  })

  it('stays on Firebase while the hub is connected but empty', async () => {
    const store = useLiveBusesStore()
    store.subscribe()
    await hubStarted()
    firebase.callback!({ val: () => ({ route_9_bus_0: { latitude: 45.7, longitude: 24.1, routeId: 9 } }) })

    hub.handlers.snapshot!([])

    expect(store.source).toBe('firebase')
    expect(store.buses).toHaveLength(1)
  })

  it('stays on Firebase when the hub cannot connect (e.g. 404 on Render)', async () => {
    hub.start.mockRejectedValue(new Error('404'))
    const store = useLiveBusesStore()
    store.subscribe()
    await hubStarted()
    firebase.callback!({ val: () => ({ route_9_bus_0: { latitude: 45.7, longitude: 24.1, routeId: 9 } }) })

    expect(store.source).toBe('firebase')
    expect(firebase.unsubscribe).not.toHaveBeenCalled()
  })

  it('falls back to Firebase when the hub closes for good', async () => {
    const store = useLiveBusesStore()
    store.subscribe()
    await hubStarted()
    hub.handlers.snapshot!([position('route_1_bus_0')])
    expect(store.source).toBe('signalr')

    hub.onclose!()
    firebase.callback!({ val: () => ({ route_9_bus_0: { latitude: 45.7, longitude: 24.1, routeId: 9 } }) })

    expect(store.source).toBe('firebase')
    expect(store.buses.map(b => b.id)).toEqual(['route_9_bus_0'])
  })

  it('opens one connection for many subscribers and closes it after the last', async () => {
    const store = useLiveBusesStore()
    store.subscribe()
    store.subscribe()
    await hubStarted()
    expect(hub.start).toHaveBeenCalledTimes(1)

    store.unsubscribe()
    expect(hub.stop).not.toHaveBeenCalled()
    store.unsubscribe()
    expect(hub.stop).toHaveBeenCalledTimes(1)
    expect(store.source).toBeNull()
  })
})
