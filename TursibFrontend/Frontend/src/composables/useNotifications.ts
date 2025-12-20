import { ref, watch } from 'vue'

interface BusLocation {
  id: string
  latitude: number
  longitude: number
  routeId: number
  speed: number
  occupancy?: number
}

interface Station {
  id: number
  name: string
  latitude: number
  longitude: number
}

interface NotificationSettings {
  enabled: boolean
  stationId: number | null
  routeId: number | null
  notifiedBuses: Set<string>
}

const settings = ref<NotificationSettings>({
  enabled: false,
  stationId: null,
  routeId: null,
  notifiedBuses: new Set()
})

const permissionGranted = ref(false)

// Diagnostic function to check notification support
export const checkNotificationSupport = (): {
  supported: boolean
  permission: NotificationPermission | 'not-supported'
  serviceWorkerRegistered: boolean
  details: string[]
} => {
  const details: string[] = []
  
  // Check if Notifications are supported
  const supported = 'Notification' in window
  details.push(`Notification API: ${supported ? '✅ Supported' : '❌ Not supported'}`)
  
  // Check permission
  const permission = supported ? Notification.permission : 'not-supported'
  details.push(`Permission: ${permission}`)
  
  // Check service worker
  const swSupported = 'serviceWorker' in navigator
  details.push(`Service Worker: ${swSupported ? '✅ Supported' : '❌ Not supported'}`)
  
  // Check if service worker is registered
  const serviceWorkerRegistered = swSupported && navigator.serviceWorker.controller !== null
  details.push(`Service Worker Registered: ${serviceWorkerRegistered ? '✅ Yes' : '⚠️ Not yet'}`)
  
  // Check secure context
  const isSecure = window.isSecureContext
  details.push(`Secure Context (HTTPS/localhost): ${isSecure ? '✅ Yes' : '❌ No'}`)
  
  console.log('🔍 Notification Diagnostics:', details.join(' | '))
  
  return { supported, permission, serviceWorkerRegistered, details }
}

// Request notification permission
export const requestNotificationPermission = async (): Promise<boolean> => {
  if (!('Notification' in window)) {
    console.warn('⚠️ Acest browser nu suportă notificări')
    return false
  }

  if (Notification.permission === 'granted') {
    permissionGranted.value = true
    return true
  }

  if (Notification.permission !== 'denied') {
    const permission = await Notification.requestPermission()
    permissionGranted.value = permission === 'granted'
    return permissionGranted.value
  }

  return false
}

// Calculate distance between two points (Haversine)
const calculateDistance = (
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number
): number => {
  const R = 6371 // km
  const dLat = (lat2 - lat1) * Math.PI / 180
  const dLon = (lon2 - lon1) * Math.PI / 180
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) *
    Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) *
    Math.sin(dLon / 2)
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return R * c
}

// Check buses and send notifications
export const checkBusNotifications = (
  buses: BusLocation[],
  station: Station | null
) => {
  if (!settings.value.enabled || !station || !permissionGranted.value) {
    return
  }

  // Double check notification permission
  if (!('Notification' in window) || Notification.permission !== 'granted') {
    console.warn('⚠️ Notificările nu sunt disponibile sau permisiunea a fost revocată')
    return
  }

  buses.forEach((bus) => {
    // Skip if we already notified for this bus
    if (settings.value.notifiedBuses.has(bus.id)) {
      return
    }

    // Skip if route filter is set and doesn't match
    if (settings.value.routeId && bus.routeId !== settings.value.routeId) {
      return
    }

    const distance = calculateDistance(
      bus.latitude,
      bus.longitude,
      station.latitude,
      station.longitude
    )

    const speed = bus.speed || 35 // km/h
    const etaMinutes = (distance / speed) * 60

    // Notifică când autobuzul e la 2 minute
    if (etaMinutes <= 2 && etaMinutes > 0) {
      sendNotification(bus, station, Math.round(etaMinutes))
      settings.value.notifiedBuses.add(bus.id)

      // Reset notification after bus passes (5 min)
      setTimeout(() => {
        settings.value.notifiedBuses.delete(bus.id)
      }, 5 * 60 * 1000)
    }
  })
}

// Send browser notification
const sendNotification = (
  bus: BusLocation,
  station: Station,
  etaMinutes: number
) => {
  try {
    const title = `🚌 Autobuzul Linia ${bus.routeId} se apropie!`
    const body = `Va sosi la ${station.name} în ${etaMinutes} ${etaMinutes === 1 ? 'minut' : 'minute'}`

    const notification = new Notification(title, {
      body,
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: `bus-${bus.id}-${station.id}`,
      requireInteraction: false,
      silent: false
    })

    notification.onclick = () => {
      window.focus()
      notification.close()
    }

    notification.onerror = (error) => {
      console.error('❌ Eroare notificare:', error)
    }

    // Auto close after 10 seconds
    setTimeout(() => {
      try {
        notification.close()
      } catch (e) {
        // Ignore errors on close
      }
    }, 10000)

    console.log(`🔔 Notificare trimisă pentru autobuzul ${bus.id}`)
  } catch (error) {
    console.error('❌ Eroare la crearea notificării:', error)
  }
}

// Enable notifications for a station
export const enableNotifications = async (
  stationId: number,
  routeId?: number
): Promise<boolean> => {
  const hasPermission = await requestNotificationPermission()

  if (!hasPermission) {
    alert('⚠️ Trebuie să accepți notificările în browser pentru a primi alerte!')
    return false
  }

  settings.value.enabled = true
  settings.value.stationId = stationId
  settings.value.routeId = routeId || null
  settings.value.notifiedBuses.clear()

  console.log(`✅ Notificări activate pentru stația ${stationId}`)
  
  // Trimite notificare de test
  try {
    const testNotification = new Notification('🔔 Notificări activate!', {
      body: 'Vei primi alerte când autobuzele se apropie de stație',
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: 'test-notification',
      requireInteraction: false,
      silent: false
    })

    testNotification.onclick = () => {
      window.focus()
      testNotification.close()
    }

    testNotification.onerror = (error) => {
      console.error('❌ Eroare notificare test:', error)
    }

    setTimeout(() => {
      try {
        testNotification.close()
      } catch (e) {
        // Ignore errors on close
      }
    }, 5000)
    console.log('✅ Notificare de test trimisă')
  } catch (error) {
    console.error('❌ Eroare la trimiterea notificării de test:', error)
    // Don't fail the whole operation if test notification fails
  }

  return true
}

// Disable notifications
export const disableNotifications = () => {
  settings.value.enabled = false
  settings.value.stationId = null
  settings.value.routeId = null
  settings.value.notifiedBuses.clear()

  console.log('🔕 Notificări dezactivate')
}

// Get current settings
export const getNotificationSettings = () => settings.value

export const useNotifications = () => {
  return {
    requestNotificationPermission,
    enableNotifications,
    disableNotifications,
    checkBusNotifications,
    getNotificationSettings,
    checkNotificationSupport,
    permissionGranted
  }
}
