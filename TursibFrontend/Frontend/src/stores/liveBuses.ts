import { ref, shallowRef } from 'vue'
import { defineStore } from 'pinia'
import { getDatabase, onValue, ref as dbRef, type Unsubscribe } from 'firebase/database'
import type { BusLocation } from '@/types/bus'
import { parseBusLocations } from '@/utils/liveBuses'

/**
 * Sursa unică de poziții live pentru toată aplicația.
 * Acum citește din Firebase; în etapa următoare sursa devine SignalR, fără schimbări în componente.
 */
export const useLiveBusesStore = defineStore('liveBuses', () => {
  const buses = shallowRef<BusLocation[]>([])
  const connected = ref(false)
  const error = ref<string | null>(null)
  /** momentul ultimei actualizări primite (ms) */
  const lastUpdate = ref<number | null>(null)

  let unsubscribe: Unsubscribe | null = null
  let subscribers = 0

  /** Pornește ascultarea (o singură conexiune, indiferent câte componente o cer). */
  function subscribe() {
    subscribers++
    if (unsubscribe) return

    unsubscribe = onValue(
      dbRef(getDatabase(), 'bus_locations'),
      snapshot => {
        buses.value = parseBusLocations(snapshot.val())
        connected.value = true
        error.value = null
        lastUpdate.value = Date.now()
      },
      err => {
        connected.value = false
        error.value = err.message
      }
    )
  }

  /** Oprește ascultarea când ultima componentă interesată se demontează. */
  function unsubscribeOne() {
    subscribers = Math.max(0, subscribers - 1)
    if (subscribers === 0 && unsubscribe) {
      unsubscribe()
      unsubscribe = null
      connected.value = false
    }
  }

  return { buses, connected, error, lastUpdate, subscribe, unsubscribe: unsubscribeOne }
})
