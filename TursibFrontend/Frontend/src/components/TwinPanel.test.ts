import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import TwinPanel from '@/components/TwinPanel.vue'

const result = {
  routeId: 9, initialisedFrom: 'even', busCount: 3, durationSeconds: 14400, routeLengthMeters: 5000,
  bunchedPercent: 42.5, meanCv: 0.61, maxCv: 1.4, minRatio: 0.004, bunchingEpisodes: 2,
  series: [{ t: 0, minRatio: 1, cv: 0, bunched: false }, { t: 7200, minRatio: 0.1, cv: 0.9, bunched: true }],
  finalLoopPositions: [100, 200, 300]
}

describe('TwinPanel', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn((url: string) =>
      Promise.resolve(
        url.endsWith('/Routes')
          ? { ok: true, json: async () => [{ id: 9, routeNumber: '9', name: 'Test' }] }
          : { ok: true, json: async () => result }
      )
    )
    vi.stubGlobal('fetch', fetchMock)
  })

  it('keeps Run disabled until a line is chosen', async () => {
    const wrapper = mount(TwinPanel)
    await flushPromises()
    expect(wrapper.find('button.run').attributes('disabled')).toBeDefined()

    await wrapper.find('select').setValue('9')
    expect(wrapper.find('button.run').attributes('disabled')).toBeUndefined()
  })

  it('posts the scenario and lists the run with its metrics', async () => {
    const wrapper = mount(TwinPanel)
    await flushPromises()
    await wrapper.find('select').setValue('9')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const post = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/twin/run'))!
    expect(JSON.parse(post[1].body)).toMatchObject({ routeId: 9, busCount: 3, durationSeconds: 14400, useLiveState: false })

    const row = wrapper.find('tbody tr')
    expect(row.text()).toContain('42.5%')
    expect(row.text()).toContain('fără perturbare')
    expect(wrapper.find('polyline').exists()).toBe(true)
  })

  it('shows the server message when the run is rejected', async () => {
    fetchMock.mockImplementation((url: string) =>
      Promise.resolve(
        url.endsWith('/Routes')
          ? { ok: true, json: async () => [{ id: 9, routeNumber: '9', name: 'Test' }] }
          : { ok: false, json: async () => ({ message: 'busCount invalid' }) }
      )
    )
    const wrapper = mount(TwinPanel)
    await flushPromises()
    await wrapper.find('select').setValue('9')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').text()).toBe('busCount invalid')
    expect(wrapper.find('table').exists()).toBe(false)
  })
})
