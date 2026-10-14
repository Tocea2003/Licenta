import { describe, it, expect, vi, afterEach } from 'vitest'
import { fetchOsrmRoute } from './osrmService'

const mockFetch = (body: unknown) =>
  vi.spyOn(globalThis, 'fetch').mockResolvedValue({ json: () => Promise.resolve(body) } as Response)

afterEach(() => vi.restoreAllMocks())

describe('fetchOsrmRoute', () => {
  it('builds the URL as lon,lat pairs and converts the geometry to [lat, lon]', async () => {
    const spy = mockFetch({
      code: 'Ok',
      routes: [{ distance: 420, duration: 300, geometry: { coordinates: [[24.1, 45.7], [24.2, 45.8]] } }]
    })

    const route = await fetchOsrmRoute('foot', [{ lat: 45.7, lon: 24.1 }, { lat: 45.8, lon: 24.2 }])

    expect(spy).toHaveBeenCalledWith(
      'https://router.project-osrm.org/route/v1/foot/24.1,45.7;24.2,45.8?overview=full&geometries=geojson'
    )
    expect(route).toEqual({ path: [[45.7, 24.1], [45.8, 24.2]], distance: 420, duration: 300 })
  })

  it('returns null when OSRM finds no route', async () => {
    mockFetch({ code: 'NoRoute', routes: [] })
    expect(await fetchOsrmRoute('driving', [{ lat: 1, lon: 1 }, { lat: 2, lon: 2 }])).toBeNull()
  })

  it('returns null when the request fails', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new Error('offline'))
    expect(await fetchOsrmRoute('foot', [{ lat: 1, lon: 1 }, { lat: 2, lon: 2 }])).toBeNull()
  })

  it('does not call the network with fewer than two points', async () => {
    const spy = mockFetch({})
    expect(await fetchOsrmRoute('foot', [{ lat: 1, lon: 1 }])).toBeNull()
    expect(spy).not.toHaveBeenCalled()
  })
})
