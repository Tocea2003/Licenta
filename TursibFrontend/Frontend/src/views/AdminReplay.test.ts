import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import AdminReplay from '@/views/AdminReplay.vue'

const T0 = 1_800_000_000

const history = [
  // bus 0: 3 eșantioane la 15 s
  { id: 'route_5_bus_0', routeId: 5, latitude: 45.70, longitude: 24.10, heading: 0, speed: 20, timestamp: T0 },
  { id: 'route_5_bus_0', routeId: 5, latitude: 45.71, longitude: 24.10, heading: 0, speed: 20, timestamp: T0 + 15 },
  { id: 'route_5_bus_0', routeId: 5, latitude: 45.72, longitude: 24.10, heading: 0, speed: 20, timestamp: T0 + 30 },
  // bus 1: apare abia după 60 s
  { id: 'route_5_bus_1', routeId: 5, latitude: 45.80, longitude: 24.20, heading: 90, speed: 10, timestamp: T0 + 60 },
  { id: 'route_5_bus_1', routeId: 5, latitude: 45.81, longitude: 24.20, heading: 90, speed: 10, timestamp: T0 + 75 }
]

const ReplayMapStub = {
  name: 'ReplayMap',
  props: ['frames', 'trails', 'shape', 'color', 'routeLabel', 'label'],
  template: '<div class="map-stub" :data-frames="frames.length" :data-shape="shape.length"></div>'
}

function mockApi(overrides: Record<string, () => unknown> = {}) {
  const fetchMock = vi.fn((url: string) => {
    const path = Object.keys(overrides).find(k => url.includes(k))
    if (path) return Promise.resolve(overrides[path]!())
    if (url.endsWith('/Routes')) return Promise.resolve({ ok: true, status: 200, json: async () => [{ id: 5, routeNumber: '5', name: 'Test', color: '#2563eb' }] })
    if (url.includes('/shapes/route/5')) return Promise.resolve({ ok: true, status: 200, json: async () => ({ points: [{ latitude: 45.7, longitude: 24.1 }, { latitude: 45.8, longitude: 24.2 }] }) })
    if (url.includes('/live/history/bounds')) return Promise.resolve({ ok: true, status: 200, json: async () => ({ first: T0, last: T0 + 75 }) })
    if (url.includes('/live/history')) return Promise.resolve({ ok: true, status: 200, json: async () => history })
    return Promise.resolve({ ok: false, status: 404, json: async () => ({}) })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

async function mountAndLoad() {
  const wrapper = mount(AdminReplay, { global: { stubs: { ReplayMap: ReplayMapStub } } })
  await flushPromises()
  await wrapper.find('select').setValue('5')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  return wrapper
}

describe('AdminReplay', () => {
  beforeEach(() => { vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame', 'performance'] }) })
  afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals() })

  it('disables the player until a history is loaded', async () => {
    mockApi()
    const wrapper = mount(AdminReplay, { global: { stubs: { ReplayMap: ReplayMapStub } } })
    await flushPromises()
    expect(wrapper.find('.btn-play').attributes('disabled')).toBeDefined()
    expect(wrapper.find('.btn-load').attributes('disabled')).toBeDefined()   // nicio linie aleasă
  })

  it('loads the latest hour with data and shows the summary', async () => {
    const fetchMock = mockApi()
    const wrapper = await mountAndLoad()

    expect(fetchMock.mock.calls.some(([u]) => String(u).includes('/live/history/bounds?routeId=5'))).toBe(true)
    const historyCall = fetchMock.mock.calls.map(([u]) => String(u)).find(u => /\/live\/history\?/.test(u))!
    expect(historyCall).toContain('routeId=5')
    expect(historyCall).toContain('from=')

    expect(wrapper.find('.summary').text()).toContain('5 poziții')
    expect(wrapper.find('.summary').text()).toContain('2 autobuze')
    expect(wrapper.find('.btn-play').attributes('disabled')).toBeUndefined()
    expect(wrapper.find('.map-stub').attributes('data-shape')).toBe('2')       // traseul liniei e desenat
  })

  it('shows the buses active at the playhead when the slider moves', async () => {
    mockApi()
    const wrapper = await mountAndLoad()

    // la început doar autobuzul 0 raportează
    expect(wrapper.find('.map-stub').attributes('data-frames')).toBe('1')
    expect(wrapper.findAll('.vehicles li')).toHaveLength(1)

    const slider = wrapper.find('input[type="range"]')
    expect(slider.attributes('min')).toBe(String(T0))
    expect(slider.attributes('max')).toBe(String(T0 + 75))

    await slider.setValue(String(T0 + 65))
    expect(wrapper.findAll('.vehicles li').map(li => li.text()).join(' ')).toContain('route_5_bus_1')
    expect(wrapper.find('.map-stub').attributes('data-frames')).toBe('1')      // bus 0 a încheiat, bus 1 e activ
  })

  it('plays forward at the chosen speed and stops at the end', async () => {
    mockApi()
    const wrapper = await mountAndLoad()
    const slider = () => wrapper.find('input[type="range"]').element as HTMLInputElement
    expect(Number(slider().value)).toBe(T0)

    await wrapper.findAll('.speed').find(b => b.text() === '60×')!.trigger('click')
    await wrapper.find('.btn-play').trigger('click')
    expect(wrapper.find('.btn-play').attributes('aria-label')).toBe('Pauză')

    await vi.advanceTimersByTimeAsync(1100)                                     // ~1 s real × 60 = ~60 s simulate
    expect(Number(slider().value)).toBeGreaterThanOrEqual(T0 + 55)
    expect(Number(slider().value)).toBeLessThan(T0 + 75)

    await vi.advanceTimersByTimeAsync(600)                                      // ajunge la capăt
    expect(Number(slider().value)).toBe(T0 + 75)
    expect(wrapper.find('.btn-play').attributes('aria-label')).toBe('Redă')    // s-a oprit singur
  })

  it('explains an empty result', async () => {
    mockApi({ '/live/history/bounds': () => ({ ok: true, status: 204, json: async () => null }) })
    const wrapper = await mountAndLoad()
    expect(wrapper.find('[role="status"]').text()).toContain('Nu există istoric')
    expect(wrapper.find('.btn-play').attributes('disabled')).toBeDefined()
  })

  it('tells the admin when the server does not record history', async () => {
    mockApi({ '/live/history/bounds': () => ({ ok: false, status: 503, json: async () => ({}) }) })
    const wrapper = await mountAndLoad()
    expect(wrapper.find('[role="alert"]').text()).toContain('History:Enabled')
  })

  it('reports a network failure', async () => {
    const fetchMock = mockApi()
    const wrapper = mount(AdminReplay, { global: { stubs: { ReplayMap: ReplayMapStub } } })
    await flushPromises()
    fetchMock.mockImplementation((url: string) =>
      url.includes('/live/history') ? Promise.reject(new Error('offline')) : Promise.resolve({ ok: true, status: 200, json: async () => [] }))
    await wrapper.find('select').setValue('5')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(wrapper.find('[role="alert"]').text()).toContain('Nu am putut încărca')
  })
})
