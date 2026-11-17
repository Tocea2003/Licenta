import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { shallowRef } from 'vue'
import type { BunchingAlert } from '@/types/bus'

const bunching = shallowRef<BunchingAlert[]>([])
const subscribe = vi.fn()
const unsubscribe = vi.fn()

vi.mock('@/stores/liveBuses', () => ({
  useLiveBusesStore: () => ({ bunching: bunching.value, subscribe, unsubscribe })
}))

import BunchingPanel from '@/components/BunchingPanel.vue'

const alert = (extra: Partial<BunchingAlert> = {}): BunchingAlert => ({
  key: '9:f', routeId: 9, routeNumber: '9', leaderId: 'route_9_bus_1', followerId: 'route_9_bus_0',
  gapMeters: 100, idealGapMeters: 3333, ratio: 0.03, severity: 'critical', startedAt: 1, ...extra
})

describe('BunchingPanel', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ recent: [] }) }))
    subscribe.mockClear()
    unsubscribe.mockClear()
  })

  it('shows the empty state when there are no alerts', () => {
    bunching.value = []
    const wrapper = mount(BunchingPanel)
    expect(wrapper.find('.alert-list').exists()).toBe(false)
    expect(wrapper.find('.empty').exists()).toBe(true)
  })

  it('lists active alerts with the line number and gap', () => {
    bunching.value = [alert()]
    const wrapper = mount(BunchingPanel)
    const item = wrapper.find('.alert')
    expect(item.classes()).toContain('alert--critical')
    expect(item.find('.line-pill').text()).toBe('9')
    expect(item.text()).toContain('route_9_bus_0')
    expect(item.text()).toContain('100 m')
  })

  it('subscribes to the live store while mounted and releases it on unmount', () => {
    bunching.value = []
    const wrapper = mount(BunchingPanel)
    expect(subscribe).toHaveBeenCalledTimes(1)
    wrapper.unmount()
    expect(unsubscribe).toHaveBeenCalledTimes(1)
  })
})
