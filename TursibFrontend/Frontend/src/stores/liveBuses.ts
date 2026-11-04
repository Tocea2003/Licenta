import { ref, shallowRef } from 'vue'
import { defineStore } from 'pinia'
import { getDatabase, onValue, ref as dbRef, type Unsubscribe } from 'firebase/database'
import type { HubConnection } from '@microsoft/signalr'
import type { BusLocation } from '@/types/bus'
import { parseBusLocations } from '@/utils/liveBuses'
import { liveHubUrl, signalrEnabled, type LiveSource } from '@/utils/liveSource'

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

/**
 * Sursa unică de poziții live pentru toată aplicația (MapView, AdminBuses, AdminAnalytics).
 *
 * Pornește imediat pe Firebase (harta nu stă goală) și, în paralel, încearcă hub-ul SignalR
 * al backend-ului (pipeline-ul MQTT → Kafka). La primele date de pe hub trece pe SignalR și
 * oprește Firebase; dacă hub-ul se închide definitiv, revine pe Firebase.
 * Pe Render hub-ul nu există (404), deci rămâne pe Firebase.
 */
export const useLiveBusesStore = defineStore('liveBuses', () => {
  const buses = shallowRef<BusLocation[]>([])
  const connected = ref(false)
  const error = ref<string | null>(null)
  /** momentul ultimei actualizări primite (ms) */
  const lastUpdate = ref<number | null>(null)
  /** de unde vin pozițiile acum */
  const source = ref<LiveSource | null>(null)

  let subscribers = 0
  let firebaseUnsubscribe: Unsubscribe | null = null
  let hub: HubConnection | null = null
  /** pozițiile primite pe hub, după id (route_X_bus_Y) */
  let hubPositions: Record<string, unknown> = {}

  function startFirebase() {
    if (firebaseUnsubscribe) return
    firebaseUnsubscribe = onValue(
      dbRef(getDatabase(), 'bus_locations'),
      snapshot => {
        if (source.value === 'signalr') return
        buses.value = parseBusLocations(snapshot.val())
        source.value = 'firebase'
        connected.value = true
        error.value = null
        lastUpdate.value = Date.now()
      },
      err => {
        if (source.value === 'signalr') return
        connected.value = false
        error.value = err.message
      }
    )
  }

  function stopFirebase() {
    firebaseUnsubscribe?.()
    firebaseUnsubscribe = null
  }

  /** Publică pozițiile de pe hub; trece pe SignalR abia când există date. */
  function applyHubPositions() {
    const parsed = parseBusLocations(hubPositions)
    if (parsed.length === 0 && source.value !== 'signalr') return
    buses.value = parsed
    source.value = 'signalr'
    connected.value = true
    error.value = null
    lastUpdate.value = Date.now()
    stopFirebase()
  }

  function withId(list: Array<{ vehicleId: string }>) {
    for (const position of list) hubPositions[position.vehicleId] = position
  }

  function fallBackToFirebase() {
    if (subscribers === 0) return
    if (source.value === 'signalr') source.value = null
    startFirebase()
  }

  async function startSignalR() {
    if (hub || !signalrEnabled(import.meta.env.VITE_LIVE_SOURCE)) return
    const signalR = await import('@microsoft/signalr')
    if (subscribers === 0 || hub) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(liveHubUrl(API_URL), { withCredentials: false })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build()
    hub = connection

    connection.on('snapshot', (list: Array<{ vehicleId: string }>) => {
      hubPositions = {}
      withId(list)
      applyHubPositions()
    })
    connection.on('positions', (list: Array<{ vehicleId: string }>) => {
      withId(list)
      applyHubPositions()
    })
    connection.on('removed', (ids: string[]) => {
      for (const id of ids) delete hubPositions[id]
      applyHubPositions()
    })
    connection.onreconnecting(() => {
      connected.value = false
    })
    connection.onreconnected(() => {
      connected.value = source.value === 'signalr'
    })
    connection.onclose(() => {
      if (hub === connection) hub = null
      hubPositions = {}
      fallBackToFirebase()
    })

    try {
      await connection.start()
    } catch {
      // Hub indisponibil (ex. 404 pe Render): rămânem pe Firebase
      if (hub === connection) hub = null
    }
  }

  /** Pornește ascultarea (o singură conexiune, indiferent câte componente o cer). */
  function subscribe() {
    subscribers++
    if (subscribers > 1) return
    startFirebase()
    void startSignalR()
  }

  /** Oprește ascultarea când ultima componentă interesată se demontează. */
  function unsubscribeOne() {
    subscribers = Math.max(0, subscribers - 1)
    if (subscribers > 0) return
    stopFirebase()
    const connection = hub
    hub = null
    hubPositions = {}
    void connection?.stop()
    connected.value = false
    source.value = null
  }

  return { buses, connected, error, lastUpdate, source, subscribe, unsubscribe: unsubscribeOne }
})
