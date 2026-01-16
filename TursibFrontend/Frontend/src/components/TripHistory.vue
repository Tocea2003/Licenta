<template>
  <div v-if="isVisible" class="trip-history-panel">
    <div class="panel-header">
      <h3 class="panel-title">📜 Istoric călătorii</h3>
      <button @click="close" class="close-btn">
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
          placeholder="Caută în istoric..."
          class="search-input"
        />
        <button v-if="searchQuery" @click="searchQuery = ''" class="clear-search-btn">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M12 4L4 12M4 4l8 8" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
          </svg>
        </button>
      </div>

      <!-- Clear all button -->
      <div v-if="filteredHistory.length > 0" class="actions-bar">
        <span class="history-count">{{ filteredHistory.length }} călătorii</span>
        <button @click="confirmClearAll" class="clear-all-btn">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M3 4h10M6 4V3a1 1 0 011-1h2a1 1 0 011 1v1M5 4v9a1 1 0 001 1h4a1 1 0 001-1V4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
          </svg>
          Șterge tot
        </button>
      </div>

      <!-- Empty state -->
      <div v-if="filteredHistory.length === 0" class="empty-state">
        <svg class="empty-icon" width="64" height="64" viewBox="0 0 64 64" fill="none">
          <circle cx="32" cy="32" r="30" stroke="currentColor" stroke-width="2" opacity="0.2"/>
          <path d="M32 20v16M32 44v2" stroke="currentColor" stroke-width="3" stroke-linecap="round"/>
        </svg>
        <p class="empty-text">{{ searchQuery ? 'Nu s-au găsit călătorii' : 'Nicio călătorie salvată' }}</p>
        <p class="empty-subtext">{{ searchQuery ? 'Încearcă alt termen de căutare' : 'Călătoriile tale vor apărea aici' }}</p>
      </div>

      <!-- History list -->
      <div v-else class="history-list">
        <div 
          v-for="trip in filteredHistory" 
          :key="trip.id" 
          class="trip-item"
          @click="selectTrip(trip)"
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
          
          <button @click.stop="deleteTrip(trip.id)" class="delete-btn">
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
  if (confirm('Sigur vrei să ștergi tot istoricul? Această acțiune nu poate fi anulată.')) {
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
  top: 80px;
  right: 20px;
  width: 420px;
  max-width: calc(100vw - 40px);
  max-height: calc(100vh - 120px);
  background: rgba(255, 255, 255, 0.98);
  backdrop-filter: blur(16px);
  border-radius: 16px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.12);
  z-index: 1000;
  overflow: hidden;
  animation: slideIn 0.3s ease-out;
  border: 1px solid rgba(0, 0, 0, 0.1);
}

@media (prefers-color-scheme: dark) {
  .trip-history-panel {
    background: rgba(30, 30, 35, 0.98);
    box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
    border: 1px solid rgba(255, 255, 255, 0.1);
  }
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateX(20px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
}

.panel-title {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
}

.close-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  border-radius: 8px;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: white;
  transition: all 0.2s;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: scale(1.05);
}

.panel-content {
  padding: 16px;
  max-height: calc(100vh - 200px);
  overflow-y: auto;
}

.search-container {
  position: relative;
  margin-bottom: 12px;
}

.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: #9ca3af;
}

.search-input {
  width: 100%;
  padding: 10px 40px 10px 40px;
  border: 2px solid #e5e7eb;
  border-radius: 10px;
  font-size: 14px;
  transition: all 0.2s;
  background: white;
  color: #1f2937;
}

.search-input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

@media (prefers-color-scheme: dark) {
  .search-input {
    background: rgba(40, 40, 50, 0.6);
    border-color: rgba(255, 255, 255, 0.15);
    color: #e5e7eb;
  }
  
  .search-input:focus {
    border-color: #667eea;
  }
}

.clear-search-btn {
  position: absolute;
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
  background: rgba(0, 0, 0, 0.1);
  border: none;
  border-radius: 6px;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: #6b7280;
  transition: all 0.2s;
}

.clear-search-btn:hover {
  background: rgba(0, 0, 0, 0.15);
}

@media (prefers-color-scheme: dark) {
  .clear-search-btn {
    background: rgba(255, 255, 255, 0.1);
    color: #9ca3af;
  }
  
  .clear-search-btn:hover {
    background: rgba(255, 255, 255, 0.15);
  }
}

.actions-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding: 8px 0;
}

.history-count {
  font-size: 13px;
  color: #6b7280;
  font-weight: 600;
}

@media (prefers-color-scheme: dark) {
  .history-count {
    color: #9ca3af;
  }
}

.clear-all-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 8px;
  color: #ef4444;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.clear-all-btn:hover {
  background: rgba(239, 68, 68, 0.15);
  border-color: rgba(239, 68, 68, 0.4);
}

.empty-state {
  text-align: center;
  padding: 40px 20px;
}

.empty-icon {
  color: #d1d5db;
  margin-bottom: 16px;
}

@media (prefers-color-scheme: dark) {
  .empty-icon {
    color: #4b5563;
  }
}

.empty-text {
  font-size: 16px;
  font-weight: 700;
  color: #4b5563;
  margin: 0 0 8px 0;
}

.empty-subtext {
  font-size: 14px;
  color: #9ca3af;
  margin: 0;
}

@media (prefers-color-scheme: dark) {
  .empty-text {
    color: #d1d5db;
  }
  
  .empty-subtext {
    color: #6b7280;
  }
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.trip-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 12px;
  background: white;
  border: 1.5px solid #e5e7eb;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.trip-item:hover {
  border-color: #667eea;
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.15);
  transform: translateY(-2px);
}

@media (prefers-color-scheme: dark) {
  .trip-item {
    background: rgba(40, 40, 50, 0.6);
    border-color: rgba(255, 255, 255, 0.15);
  }
  
  .trip-item:hover {
    border-color: #667eea;
  }
}

.trip-icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%);
  border-radius: 10px;
  color: #667eea;
}

.trip-details {
  flex: 1;
  min-width: 0;
}

.trip-route {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
  font-weight: 600;
  font-size: 14px;
}

.trip-start,
.trip-end {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #1f2937;
}

@media (prefers-color-scheme: dark) {
  .trip-start,
  .trip-end {
    color: #e5e7eb;
  }
}

.trip-arrow {
  flex-shrink: 0;
  color: #9ca3af;
}

.trip-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  font-size: 12px;
  color: #6b7280;
}

@media (prefers-color-scheme: dark) {
  .trip-meta {
    color: #9ca3af;
  }
}

.trip-time,
.trip-duration {
  font-weight: 600;
}

.trip-separator {
  color: #d1d5db;
}

.trip-buses {
  display: flex;
  align-items: center;
  gap: 4px;
}

.bus-badge {
  display: inline-block;
  padding: 2px 6px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 800;
}

.transfer-icon {
  color: #9ca3af;
  font-weight: 700;
}

.delete-btn {
  flex-shrink: 0;
  background: rgba(239, 68, 68, 0.1);
  border: none;
  border-radius: 8px;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: #ef4444;
  transition: all 0.2s;
  opacity: 0.6;
}

.delete-btn:hover {
  opacity: 1;
  background: rgba(239, 68, 68, 0.15);
  transform: scale(1.05);
}

/* Scrollbar styling */
.panel-content::-webkit-scrollbar {
  width: 6px;
}

.panel-content::-webkit-scrollbar-track {
  background: transparent;
}

.panel-content::-webkit-scrollbar-thumb {
  background: rgba(209, 213, 219, 0.8);
  border-radius: 10px;
}

.panel-content::-webkit-scrollbar-thumb:hover {
  background: rgba(156, 163, 175, 0.9);
}

@media (prefers-color-scheme: dark) {
  .panel-content::-webkit-scrollbar-thumb {
    background: rgba(255, 255, 255, 0.2);
  }
  
  .panel-content::-webkit-scrollbar-thumb:hover {
    background: rgba(255, 255, 255, 0.3);
  }
}

@media (max-width: 768px) {
  .trip-history-panel {
    top: 10px;
    right: 10px;
    width: calc(100vw - 20px);
  }
}
</style>
