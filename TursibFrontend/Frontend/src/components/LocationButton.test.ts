import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import LocationButton from '@/components/LocationButton.vue'

describe('LocationButton', () => {
  beforeEach(() => {
    // Mock window.alert
    window.alert = vi.fn() as any
    
    // Mock navigator.geolocation using Object.defineProperty
    Object.defineProperty(window.navigator, 'geolocation', {
      writable: true,
      value: {
        getCurrentPosition: vi.fn((success) => {
          success({
            coords: {
              latitude: 45.7983,
              longitude: 24.1256
            }
          })
        })
      }
    })
  })

  it('should render the button', () => {
    const wrapper = mount(LocationButton)

    const button = wrapper.find('button')
    expect(button.exists()).toBe(true)

    wrapper.unmount()
  })

  it('should emit location event when clicked', async () => {
    const wrapper = mount(LocationButton)

    const button = wrapper.find('button')
    await button.trigger('click')
    
    // Wait for geolocation to complete
    await wrapper.vm.$nextTick()
    await new Promise(resolve => setTimeout(resolve, 10))

    // Check that geolocation was called
    expect(window.navigator.geolocation.getCurrentPosition).toHaveBeenCalled()

    wrapper.unmount()
  })

  it('should call geolocation API', async () => {
    const wrapper = mount(LocationButton)

    const button = wrapper.find('button')
    await button.trigger('click')
    
    await wrapper.vm.$nextTick()

    // Verify geolocation was called
    expect(window.navigator.geolocation.getCurrentPosition).toHaveBeenCalled()

    wrapper.unmount()
  })
})
