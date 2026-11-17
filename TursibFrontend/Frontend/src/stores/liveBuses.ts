import { ref, shallowRef } from 'vue'
import { defineStore } from 'pinia'
import { getDatabase, onValue, ref as dbRef, type Unsubscribe } from 'firebase/database'
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import type { BunchingAlert, BusLocation } from '@/types/bus'
import { parseBunchingList } from '@/utils/bunching'
import { hubUrlFromApiUrl, mergePositions, parseBusLocations, parsePositionList } from '@/utils/liveBuses'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
/** `signalr` | `firebase` | `auto` (implicit: SignalR, cu Firebase ca rezervă) */
const LIVE_SOURCE = (import.meta.env.VITE_LIVE_SOURCE as string | undefined) ?? 'auto'
/**
 * Dacă SignalR nu a livrat nimic de atâta timp, pornește Firebase ca rezervă. Peste intervalul de raportare al
 * dispozitivelor (în cloud simulatorul publică pe MQTT la 10 s; dispozitivele AVL reale la 10–30 s).
 */
const STALE_AFTER_MS = 35_000
const WATCHDOG_MS = 5_000
const RETRY_CONNECT_MS = 30_000

export type LiveSource = 'signalr' | 'firebase' | null

/**
 * Sursa unică de poziții live pentru toată aplicația (o singură conexiune, ref-counted).
 * Primar: hub-ul SignalR al backend-ului (snapshot la conectare + loturi de poziții).
 * Rezervă: Firebase, pornit automat cât timp SignalR nu livrează date și oprit când își revine.
 * Componentele nu depind de sursă.
 */
export const useLiveBusesStore = defineStore('liveBuses', () => {
  const buses = shallowRef<BusLocation[]>([])
  const connected = ref(false)
  const error = ref<string | null>(null)
  /** momentul ultimei actualizări primite (ms) */
  const lastUpdate = ref<number | null>(null)
  /** sursa care alimentează acum harta */
  const source = ref<LiveSource>(null)
  /** alertele de bunching active acum (vin doar prin SignalR; backend-ul le detectează) */
  const bunching = shallowRef<BunchingAlert[]>([])

  let subscribers = 0
  let hub: HubConnection | null = null
  let firebaseUnsubscribe: Unsubscribe | null = null
  let lastSignalrData = 0
  let watchdog: ReturnType<typeof setInterval> | null = null
  let retryTimer: ReturnType<typeof setTimeout> | null = null

  const signalrFresh = () => Date.now() - lastSignalrData < STALE_AFTER_MS

  function publish(next: BusLocation[], from: Exclude<LiveSource, null>) {
    buses.value = next
    source.value = from
    connected.value = true
    error.value = null
    lastUpdate.value = Date.now()
  }

  // ---- SignalR ----

  async function loadSnapshot() {
    if (!hub || hub.state !== HubConnectionState.Connected) return
    const snapshot = parsePositionList(await hub.invoke('GetSnapshot'))
    if (snapshot.length > 0) {
      lastSignalrData = Date.now()
      publish(snapshot, 'signalr')
    }
    bunching.value = parseBunchingList(await hub.invoke('GetBunching'))
  }

  async function connectSignalR() {
    if (hub) return
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrlFromApiUrl(API_BASE_URL), { withCredentials: false })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()
    hub = connection

    connection.on('positions', (batch: unknown) => {
      const parsed = parsePositionList(batch)
      if (parsed.length === 0) return
      lastSignalrData = Date.now()
      publish(mergePositions(buses.value, parsed), 'signalr')
    })
    connection.on('bunching', (list: unknown) => { bunching.value = parseBunchingList(list) })
    connection.onreconnecting(() => { bunching.value = [] })
    connection.onreconnected(() => { void loadSnapshot().catch(() => {}) })

    try {
      await connection.start()
      await loadSnapshot()
    } catch (err) {
      error.value = err instanceof Error ? err.message : String(err)
      hub = null
      void connection.stop().catch(() => {})
      // backend indisponibil: încercăm din nou mai târziu; între timp ne ține Firebase
      if (subscribers > 0 && !retryTimer) {
        retryTimer = setTimeout(() => {
          retryTimer = null
          if (subscribers > 0) void connectSignalR()
        }, RETRY_CONNECT_MS)
      }
    }
  }

  // ---- Firebase (rezervă) ----

  function startFirebase() {
    if (firebaseUnsubscribe) return
    firebaseUnsubscribe = onValue(
      dbRef(getDatabase(), 'bus_locations'),
      snapshot => {
        if (LIVE_SOURCE !== 'firebase' && signalrFresh()) return // SignalR e sursa activă
        publish(parseBusLocations(snapshot.val()), 'firebase')
      },
      err => {
        connected.value = false
        error.value = err.message
      }
    )
  }

  function stopFirebase() {
    firebaseUnsubscribe?.()
    firebaseUnsubscribe = null
  }

  /** Pornește Firebase doar cât timp SignalR nu e „viu”. */
  function checkSource() {
    if (signalrFresh()) stopFirebase()
    else startFirebase()
  }

  // ---- ciclul de viață ----

  /** Pornește ascultarea (o singură conexiune, indiferent câte componente o cer). */
  function subscribe() {
    subscribers++
    if (subscribers > 1) return

    if (LIVE_SOURCE === 'firebase') {
      startFirebase()
      return
    }
    void connectSignalR()
    if (LIVE_SOURCE === 'auto') {
      startFirebase() // până livrează SignalR primul lot, harta nu rămâne goală
      watchdog = setInterval(checkSource, WATCHDOG_MS)
    }
  }

  /** Oprește ascultarea când ultima componentă interesată se demontează. */
  function unsubscribeOne() {
    subscribers = Math.max(0, subscribers - 1)
    if (subscribers > 0) return

    if (watchdog) clearInterval(watchdog)
    watchdog = null
    if (retryTimer) clearTimeout(retryTimer)
    retryTimer = null
    stopFirebase()
    const connection = hub
    hub = null
    void connection?.stop().catch(() => {})
    connected.value = false
    source.value = null
    bunching.value = []
    lastSignalrData = 0
  }

  return { buses, connected, error, lastUpdate, source, bunching, subscribe, unsubscribe: unsubscribeOne }
})
