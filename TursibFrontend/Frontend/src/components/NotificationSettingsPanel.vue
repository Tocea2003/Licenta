<template>
  <Transition name="slide-up">
    <div v-if="isOpen" class="notification-settings-panel">
      <div class="panel-header">
        <div class="header-content">
          <h3>🔔 {{ t('notificationSettings') }}</h3>
          <p class="subtitle">{{ t('customizeAlerts') }}</p>
        </div>
        <button @click="$emit('close')" class="close-btn" :aria-label="t('close')">
          ✕
        </button>
      </div>

      <div class="panel-content">
        <!-- Main Toggle -->
        <div class="setting-section">
          <div class="setting-item main-toggle">
            <div class="setting-info">
              <div class="setting-label">
                <span class="setting-icon">🔔</span>
                {{ t('activeNotifications') }}
              </div>
              <p class="setting-description">{{ t('getAlertsWhenBusesApproach') }}</p>
            </div>
            <label class="toggle-switch">
              <input 
                type="checkbox" 
                v-model="localSettings.enabled"
                @change="handleToggleChange"
              />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>

        <!-- Advanced Settings -->
        <Transition name="fade">
          <div v-if="localSettings.enabled" class="advanced-settings">
            <!-- ETA Threshold -->
            <div class="setting-section">
              <div class="section-title">
                <span class="title-icon">⏰</span>
                {{ t('whenToNotify') }}
              </div>
              <div class="setting-item">
                <div class="setting-info">
                  <div class="setting-label">{{ t('timeUntilArrival') }}</div>
                  <p class="setting-description">{{ t('notifyWhenBusAt', '', { n: localSettings.etaThresholdMinutes }) }}</p>
                </div>
                <div class="slider-container">
                  <input 
                    type="range" 
                    v-model.number="localSettings.etaThresholdMinutes"
                    min="1"
                    max="10"
                    step="1"
                    class="time-slider"
                    @change="saveSettings"
                  />
                  <span class="slider-value">{{ localSettings.etaThresholdMinutes }} min</span>
                </div>
              </div>
            </div>

            <!-- Alert Types -->
            <div class="setting-section">
              <div class="section-title">
                <span class="title-icon">🎯</span>
                {{ t('alertTypes') }}
              </div>
              
              <div class="setting-item">
                <div class="setting-info">
                  <div class="setting-label">
                    <span class="setting-icon">⏰</span>
                    {{ t('delayAlerts') }}
                  </div>
                  <p class="setting-description">{{ t('notifyWhenDelayed') }}</p>
                </div>
                <label class="toggle-switch small">
                  <input 
                    type="checkbox" 
                    v-model="localSettings.delayAlerts"
                    @change="saveSettings"
                  />
                  <span class="toggle-slider"></span>
                </label>
              </div>

              <div class="setting-item">
                <div class="setting-info">
                  <div class="setting-label">
                    <span class="setting-icon">🚌</span>
                    {{ t('crowdingAlerts') }}
                  </div>
                  <p class="setting-description">{{ t('findOutWhenFull') }}</p>
                </div>
                <label class="toggle-switch small">
                  <input 
                    type="checkbox" 
                    v-model="localSettings.crowdingAlerts"
                    @change="saveSettings"
                  />
                  <span class="toggle-slider"></span>
                </label>
              </div>
            </div>

            <!-- Favorite Routes -->
            <div class="setting-section">
              <div class="section-title">
                <span class="title-icon">⭐</span>
                {{ t('favoriteRoutes') }}
                <button @click="showRouteSelector = !showRouteSelector" class="add-route-btn">
                  {{ showRouteSelector ? '✕' : '+' }}
                </button>
              </div>

              <Transition name="fade">
                <div v-if="showRouteSelector" class="route-selector">
                  <input 
                    v-model="newRouteId" 
                    type="number" 
                    :placeholder="t('enterRouteNumber')"
                    class="route-input"
                    @keyup.enter="addRoute"
                  />
                  <button @click="addRoute" class="add-btn">{{ t('add') }}</button>
                </div>
              </Transition>

              <div class="favorite-routes-list">
                <TransitionGroup name="route-badge">
                  <div 
                    v-for="routeId in localSettings.favoriteRoutes" 
                    :key="`route-${routeId}`"
                    class="route-badge"
                  >
                    <span class="route-number">{{ t('busLine') }} {{ routeId }}</span>
                    <button 
                      @click="removeRoute(routeId)" 
                      class="remove-route-btn"
                      :aria-label="t('removeRoute')"
                    >
                      ✕
                    </button>
                  </div>
                  <div 
                    v-if="localSettings.favoriteRoutes.length === 0" 
                    key="empty"
                    class="empty-state"
                  >
                    <span class="empty-icon">⭐</span>
                    <p>{{ t('noFavoriteRoutes') }}</p>
                    <small>{{ t('addRoutesForPriority') }}</small>
                  </div>
                </TransitionGroup>
              </div>
            </div>

            <!-- Test Notification -->
            <div class="setting-section">
              <button @click="sendTestNotification" class="test-notification-btn" :disabled="isSendingTest">
                <span v-if="isSendingTest" class="spinner"></span>
                <span v-else class="btn-icon">🧪</span>
                {{ t('testNotification') }}
              </button>
            </div>
          </div>
        </Transition>

        <!-- Notification History -->
        <div class="setting-section">
          <div class="section-title">
            <span class="title-icon">📜</span>
            {{ t('notificationHistory') }}
            <button 
              v-if="history.length > 0"
              @click="clearHistory" 
              class="clear-history-btn"
            >
              {{ t('clearAll') }}
            </button>
          </div>

          <div class="notification-history">
            <TransitionGroup name="history-item" tag="div" class="history-list">
              <div 
                v-for="item in recentHistory" 
                :key="item.id"
                class="history-item"
                :class="`history-${item.type}`"
              >
                <div class="history-icon">{{ getHistoryIcon(item.type) }}</div>
                <div class="history-content">
                  <div class="history-message">{{ item.message }}</div>
                  <div class="history-meta">
                    <span class="history-station">{{ item.station }}</span>
                    <span class="separator">•</span>
                    <span class="history-time">{{ formatTime(item.timestamp) }}</span>
                  </div>
                </div>
              </div>
              <div 
                v-if="history.length === 0" 
                key="empty-history"
                class="empty-state small"
              >
                <span class="empty-icon">📭</span>
                <p>{{ t('noNotificationsYet') }}</p>
              </div>
            </TransitionGroup>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()
import { 
  useNotifications,
  type NotificationSettings,
  type NotificationHistory
} from '@/composables/useNotifications'

interface Props {
  isOpen: boolean
}

const props = defineProps<Props>()
const emit = defineEmits(['close', 'settingsUpdated'])

const {
  getNotificationSettings,
  getNotificationHistory,
  updateNotificationSettings,
  addFavoriteRoute,
  removeFavoriteRoute,
  clearNotificationHistory,
  requestNotificationPermission
} = useNotifications()

const localSettings = ref<NotificationSettings>({ ...getNotificationSettings() })
const history = ref<NotificationHistory[]>([...getNotificationHistory()])
const showRouteSelector = ref(false)
const newRouteId = ref<number | null>(null)
const isSendingTest = ref(false)

const recentHistory = computed(() => {
  return history.value.slice(0, 10)
})

// Load history from localStorage on mount
onMounted(() => {
  const saved = localStorage.getItem('notification_history')
  if (saved) {
    try {
      history.value = JSON.parse(saved)
    } catch (e) {
      console.error('Failed to load history:', e)
    }
  }
})

// Watch for history updates
watch(() => getNotificationHistory(), (newHistory) => {
  history.value = [...newHistory]
}, { deep: true })

const saveSettings = () => {
  updateNotificationSettings(localSettings.value)
  emit('settingsUpdated', localSettings.value)
}

const handleToggleChange = async () => {
  if (localSettings.value.enabled) {
    const hasPermission = await requestNotificationPermission()
    if (!hasPermission) {
      localSettings.value.enabled = false
      return
    }
  }
  saveSettings()
}

const addRoute = () => {
  if (newRouteId.value && !localSettings.value.favoriteRoutes.includes(newRouteId.value)) {
    addFavoriteRoute(newRouteId.value)
    localSettings.value.favoriteRoutes.push(newRouteId.value)
    newRouteId.value = null
    showRouteSelector.value = false
    saveSettings()
  }
}

const removeRoute = (routeId: number) => {
  removeFavoriteRoute(routeId)
  const index = localSettings.value.favoriteRoutes.indexOf(routeId)
  if (index > -1) {
    localSettings.value.favoriteRoutes.splice(index, 1)
  }
  saveSettings()
}

const clearHistory = () => {
  clearNotificationHistory()
  history.value = []
}

const sendTestNotification = async () => {
  isSendingTest.value = true
  
  try {
    const hasPermission = await requestNotificationPermission()
    if (!hasPermission) {
      alert('Trebuie să accepți notificările pentru a trimite o notificare de test')
      return
    }

    const testNotif = new Notification('🧪 Notificare de test', {
      body: 'Notificările funcționează corect! 🎉',
      icon: '/front-of-bus.png',
      badge: '/bus-station.png',
      tag: 'test-notification',
      requireInteraction: false
    })

    testNotif.onclick = () => {
      window.focus()
      testNotif.close()
    }

    setTimeout(() => {
      try {
        testNotif.close()
      } catch (e) {
        // Ignore
      }
    }, 5000)
  } catch (error) {
    console.error('Failed to send test notification:', error)
    alert('Eroare la trimiterea notificării de test')
  } finally {
    setTimeout(() => {
      isSendingTest.value = false
    }, 1000)
  }
}

const getHistoryIcon = (type: string): string => {
  const icons = {
    arrival: '🚌',
    delay: '⏰',
    crowding: '🚌',
    favorite: '⭐'
  }
  return icons[type as keyof typeof icons] || '🔔'
}

const formatTime = (timestamp: Date): string => {
  const date = new Date(timestamp)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const minutes = Math.floor(diff / 60000)
  
  if (minutes < 1) return 'Acum'
  if (minutes < 60) return `${minutes} min`
  if (minutes < 1440) return `${Math.floor(minutes / 60)} ore`
  return `${Math.floor(minutes / 1440)} zile`
}
</script>

<style scoped>
.notification-settings-panel {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  background: var(--bg-primary);
  border-radius: 24px 24px 0 0;
  box-shadow: 0 -8px 32px rgba(0, 0, 0, 0.2);
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  z-index: 1100;
  animation: slideUp 0.3s ease-out;
}

@keyframes slideUp {
  from {
    transform: translateY(100%);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px 24px 16px;
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

.header-content h3 {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
}

.subtitle {
  margin: 4px 0 0 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.close-btn {
  background: var(--bg-secondary);
  border: none;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  font-size: 20px;
  cursor: pointer;
  color: var(--text-secondary);
  transition: all 0.3s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.close-btn:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
  transform: rotate(90deg);
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 16px 24px 24px;
}

.setting-section {
  margin-bottom: 24px;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 12px;
}

.title-icon {
  font-size: 18px;
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  background: var(--bg-secondary);
  border-radius: 12px;
  margin-bottom: 8px;
  transition: all 0.3s ease;
}

.setting-item:hover {
  background: var(--bg-tertiary);
}

.setting-item.main-toggle {
  padding: 20px;
  border: 2px solid var(--border-color);
}

.setting-info {
  flex: 1;
}

.setting-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.setting-icon {
  font-size: 16px;
}

.setting-description {
  margin: 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.toggle-switch {
  position: relative;
  display: inline-block;
  width: 52px;
  height: 28px;
  flex-shrink: 0;
}

.toggle-switch.small {
  width: 44px;
  height: 24px;
}

.toggle-switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: absolute;
  cursor: pointer;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: var(--border-color);
  transition: 0.3s;
  border-radius: 34px;
}

.toggle-slider:before {
  position: absolute;
  content: "";
  height: 20px;
  width: 20px;
  left: 4px;
  bottom: 4px;
  background-color: white;
  transition: 0.3s;
  border-radius: 50%;
}

.toggle-switch.small .toggle-slider:before {
  height: 16px;
  width: 16px;
}

input:checked + .toggle-slider {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

input:checked + .toggle-slider:before {
  transform: translateX(24px);
}

.toggle-switch.small input:checked + .toggle-slider:before {
  transform: translateX(20px);
}

.slider-container {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: flex-end;
  min-width: 150px;
}

.time-slider {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  background: var(--border-color);
  outline: none;
  -webkit-appearance: none;
}

.time-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.4);
}

.time-slider::-moz-range-thumb {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  cursor: pointer;
  border: none;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.4);
}

.slider-value {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-primary);
}

.add-route-btn,
.clear-history-btn {
  margin-left: auto;
  background: var(--bg-tertiary);
  border: none;
  padding: 6px 12px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.add-route-btn:hover,
.clear-history-btn:hover {
  background: var(--border-color);
  color: var(--text-primary);
}

.route-selector {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.route-input {
  flex: 1;
  padding: 10px 14px;
  border: 2px solid var(--border-color);
  border-radius: 10px;
  background: var(--bg-tertiary);
  color: var(--text-primary);
  font-size: 14px;
}

.route-input:focus {
  outline: none;
  border-color: #667eea;
}

.add-btn {
  padding: 10px 20px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.add-btn:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.favorite-routes-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.route-badge {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 20px;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.2s ease;
}

.route-badge:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.route-number {
  display: flex;
  align-items: center;
  gap: 4px;
}

.remove-route-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 12px;
  color: white;
  transition: all 0.2s ease;
}

.remove-route-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: rotate(90deg);
}

.test-notification-btn {
  width: 100%;
  padding: 14px 20px;
  background: linear-gradient(135deg, #48bb78 0%, #38a169 100%);
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.3s ease;
}

.test-notification-btn:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(72, 187, 120, 0.4);
}

.test-notification-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-icon {
  font-size: 18px;
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.notification-history {
  max-height: 300px;
  overflow-y: auto;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.history-item {
  display: flex;
  gap: 12px;
  padding: 12px;
  background: var(--bg-secondary);
  border-radius: 10px;
  transition: all 0.2s ease;
}

.history-item:hover {
  background: var(--bg-tertiary);
}

.history-icon {
  font-size: 24px;
  flex-shrink: 0;
}

.history-content {
  flex: 1;
  min-width: 0;
}

.history-message {
  font-size: 14px;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.history-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-secondary);
}

.separator {
  color: var(--border-color);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px;
  text-align: center;
}

.empty-state.small {
  padding: 24px;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 12px;
  opacity: 0.5;
}

.empty-state p {
  margin: 0 0 4px 0;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-secondary);
}

.empty-state small {
  font-size: 13px;
  color: var(--text-tertiary);
}

.advanced-settings {
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: all 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

.route-badge-move,
.route-badge-enter-active,
.route-badge-leave-active {
  transition: all 0.3s ease;
}

.route-badge-enter-from,
.route-badge-leave-to {
  opacity: 0;
  transform: scale(0.8);
}

.history-item-move,
.history-item-enter-active,
.history-item-leave-active {
  transition: all 0.3s ease;
}

.history-item-enter-from,
.history-item-leave-to {
  opacity: 0;
  transform: translateX(-20px);
}

/* Responsive */
@media (max-width: 768px) {
  .notification-settings-panel {
    max-height: 90vh;
  }

  .panel-header {
    padding: 20px 20px 14px;
  }

  .panel-content {
    padding: 14px 20px 20px;
  }

  .setting-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .toggle-switch,
  .slider-container {
    align-self: flex-end;
  }
}
</style>
