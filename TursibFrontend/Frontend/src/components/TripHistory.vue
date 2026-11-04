<template>
  <div v-if="isVisible" class="trip-history-panel">
    <div class="panel-header">
      <h3 class="panel-title">{{ t('tripHistoryTitle') }}</h3>
      <button type="button" @click="close" class="close-btn" :aria-label="t('close')">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path d="M15 5L5 15M5 5l10 10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
      </button>
    </div>

    <div class="panel-content">
      <!-- Search bar -->
      <div class="search-container">
        <svg class="search-icon" width="20" height="20" viewBox="0 0 20 20" fill="none">
          <circle cx="9" cy="9" r="6" stroke="currentColor" stroke-width="2"/>
          <path d="M13.5 13.5L17 17" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
        <input 
          v-model="searchQuery" 
          type="text" 
          :placeholder="t('searchInHistory')"
          :aria-label="t('searchInHistory')"
          class="search-input"
        />
        <button v-if="searchQuery" type="button" @click="searchQuery = ''" class="clear-search-btn" :aria-label="t('close')">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M12 4L4 12M4 4l8 8" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
          </svg>
        </button>
      </div>

      <!-- Clear all button -->
      <div v-if="filteredHistory.length > 0" class="actions-bar">
        <span class="history-count">{{ filteredHistory.length }} {{ t('tripsCount') }}</span>
        <button type="button" @click="confirmClearAll" class="clear-all-btn">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M3 4h10M6 4V3a1 1 0 011-1h2a1 1 0 011 1v1M5 4v9a1 1 0 001 1h4a1 1 0 001-1V4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
          </svg>
          {{ t('clearAll') }}
        </button>
      </div>

      <!-- Empty state -->
      <div v-if="filteredHistory.length === 0" class="empty-state">
        <svg class="empty-icon" width="64" height="64" viewBox="0 0 64 64" fill="none">
          <circle cx="32" cy="32" r="30" stroke="currentColor" stroke-width="2" opacity="0.2"/>
          <path d="M32 20v16M32 44v2" stroke="currentColor" stroke-width="3" stroke-linecap="round"/>
        </svg>
        <p class="empty-text">{{ searchQuery ? t('noTripsFound2') : t('noTripsSaved') }}</p>
        <p class="empty-subtext">{{ searchQuery ? t('tryDifferentSearch') : t('tripsWillAppearHere') }}</p>
      </div>

      <!-- History list -->
      <div v-else class="history-list">
        <div 
          v-for="trip in filteredHistory" 
          :key="trip.id" 
          class="trip-item"
          role="button"
          tabindex="0"
          @click="selectTrip(trip)"
          @keydown.enter.prevent="selectTrip(trip)"
          @keydown.space.prevent="selectTrip(trip)"
        >
          <div class="trip-icon">
            <svg v-if="trip.routeType === 'direct'" width="24" height="24" viewBox="0 0 24 24" fill="none">
              <rect x="4" y="4" width="16" height="16" rx="2" stroke="currentColor" stroke-width="2"/>
              <circle cx="8" cy="15" r="1" fill="currentColor"/>
              <circle cx="16" cy="15" r="1" fill="currentColor"/>
            </svg>
            <svg v-else width="24" height="24" viewBox="0 0 24 24" fill="none">
              <path d="M10 2v16M4 8l6-6 6 6M4 12l6 6 6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
            </svg>
          </div>
          
          <div class="trip-details">
            <div class="trip-route">
              <span class="trip-start">{{ trip.startLocation }}</span>
              <svg class="trip-arrow" width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path d="M2 8h12M10 4l4 4-4 4" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <span class="trip-end">{{ trip.endLocation }}</span>
            </div>
            
            <div class="trip-meta">
              <span class="trip-time">{{ formatDate(trip.timestamp) }}</span>
              <span class="trip-separator">•</span>
              <span class="trip-buses">
                <template v-for="(line, idx) in trip.routeDetails.busLines" :key="line">
                  <span class="bus-badge">{{ line }}</span>
                  <span v-if="idx < trip.routeDetails.busLines.length - 1" class="transfer-icon">→</span>
                </template>
              </span>
              <span class="trip-separator">•</span>
              <span class="trip-duration">{{ formatDuration(trip.routeDetails.totalDuration) }}</span>
            </div>
          </div>
          
          <button type="button" @click.stop="deleteTrip(trip.id)" class="delete-btn" :aria-label="t('delete')">
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none">
              <path d="M4 5h10M7 5V4a1 1 0 011-1h2a1 1 0 011 1v1M6 5v8a1 1 0 001 1h4a1 1 0 001-1V5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
            </svg>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { tripHistoryService, type TripHistoryItem } from '../services/tripHistoryService'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const props = defineProps<{
  visible: boolean
}>()

const emit = defineEmits<{
  close: []
  selectTrip: [trip: TripHistoryItem]
}>()

const isVisible = computed(() => props.visible)
const searchQuery = ref('')
const history = ref<TripHistoryItem[]>([])

const filteredHistory = computed(() => {
  if (!searchQuery.value) {
    return history.value
  }
  return tripHistoryService.searchHistory(searchQuery.value)
})

const loadHistory = () => {
  history.value = tripHistoryService.getHistory()
}

const selectTrip = (trip: TripHistoryItem) => {
  emit('selectTrip', trip)
  close()
}

const deleteTrip = (id: string) => {
  tripHistoryService.deleteTrip(id)
  loadHistory()
}

const confirmClearAll = () => {
  if (confirm(t('confirmClearHistory'))) {
    tripHistoryService.clearHistory()
    loadHistory()
  }
}

const close = () => {
  emit('close')
}

const formatDate = (timestamp: number): string => {
  return tripHistoryService.formatDate(timestamp)
}

const formatDuration = (minutes: number): string => {
  return tripHistoryService.formatDuration(minutes)
}

onMounted(() => {
  loadHistory()
})

// Reload când devine vizibil
watch(() => props.visible, (newVal) => {
  if (newVal) {
    loadHistory()
  }
})
</script>

<script lang="ts">
import { watch } from 'vue'
</script>

<style scoped>
.trip-history-panel {
  position: fixed;
  top: 84px;
  right: 76px;
  z-index: 1200;
  width: 380px;
  max-height: calc(100dvh - 100px);
  display: flex;
  flex-direction: column;
  background: var(--bg-primary);
  color: var(--text-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-xl);
  font-family: var(--font-sans);
  font-variant-numeric: tabular-nums;
}

.panel-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 8px 10px 20px;
  border-bottom: 1px solid var(--border-subtle);
}

.panel-title {
  margin: 0;
  font-size: 20px;
  font-weight: var(--fw-bold);
}

.close-btn,
.clear-search-btn,
.delete-btn {
  border: none;
  background: transparent;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-md);
  cursor: pointer;
}

.close-btn {
  margin-left: auto;
  width: 44px;
  height: 44px;
}

.close-btn:hover,
.clear-search-btn:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.panel-content {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.search-container {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 44px;
  padding: 0 4px 0 12px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-secondary);
}

.search-container:focus-within {
  border-color: var(--border-focus);
  box-shadow: var(--ring-focus);
}

.search-icon {
  color: var(--text-secondary);
  flex-shrink: 0;
}

.search-input {
  flex: 1;
  min-width: 0;
  border: none;
  background: transparent;
  color: var(--text-primary);
  font: inherit;
  font-size: 15px;
  outline: none;
}

.clear-search-btn {
  width: 36px;
  height: 36px;
}

.actions-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.history-count {
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.clear-all-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 36px;
  padding: 0 10px;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-danger);
  font: inherit;
  font-size: 13px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.clear-all-btn:hover {
  background: var(--color-danger-soft);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 32px 8px;
  text-align: center;
  color: var(--text-tertiary);
}

.empty-text {
  margin: 8px 0 0;
  font-weight: var(--fw-bold);
  color: var(--text-primary);
}

.empty-subtext {
  margin: 0;
  font-size: 14px;
  color: var(--text-secondary);
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.trip-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 6px 10px 10px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  cursor: pointer;
}

.trip-item:hover {
  border-color: var(--border-secondary);
}

.trip-item:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

.trip-icon {
  width: 36px;
  height: 36px;
  flex-shrink: 0;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.trip-icon svg {
  width: 18px;
  height: 18px;
}

.trip-details {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.trip-route {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: var(--fw-bold);
  min-width: 0;
}

.trip-start,
.trip-end {
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.trip-arrow {
  flex-shrink: 0;
  color: var(--text-tertiary);
}

.trip-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  font-size: 12px;
  color: var(--text-secondary);
}

.trip-buses {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.bus-badge {
  min-width: 24px;
  height: 20px;
  padding: 0 6px;
  border-radius: 5px;
  background: var(--bg-inverse);
  color: var(--text-inverse);
  font-family: var(--font-display);
  font-weight: var(--fw-heavy);
  font-size: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.delete-btn {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
}

.delete-btn:hover {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

@media (max-width: 767px) {
  .trip-history-panel {
    top: auto;
    left: 0;
    right: 0;
    bottom: var(--nav-height);
    width: auto;
    max-height: 70dvh;
    border-radius: var(--radius-xl) var(--radius-xl) 0 0;
  }
}
</style>
