<template>
  <Transition name="slide-down">
    <div v-if="!isOnline" class="offline-banner">
      <div class="offline-content">
        <span class="offline-icon">📴</span>
        <div class="offline-text">
          <strong>{{ t('offlineMode') }}</strong>
          <span class="offline-message">
            {{ t('offlineUsingLocalData') }}
          </span>
        </div>
      </div>
      <button v-if="isSyncing" class="sync-button syncing" disabled>
        <span class="spinner"></span>
        {{ t('syncing') }}
      </button>
      <button v-else @click="handleSync" class="sync-button">
        🔄 {{ t('sync') }}
      </button>
    </div>
  </Transition>
  
  <Transition name="slide-down">
    <div v-if="showSyncSuccess" class="sync-success">
      ✅ {{ t('syncSuccess') }}
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useOfflineMode } from '@/composables/useOfflineMode'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()
const { isOnline, isSyncing, syncData } = useOfflineMode()
const showSyncSuccess = ref(false)

const handleSync = async () => {
  try {
    const apiBaseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5022'
    await syncData(apiBaseUrl)
    
    showSyncSuccess.value = true
    setTimeout(() => {
      showSyncSuccess.value = false
    }, 3000)
  } catch (error) {
    console.error('Sync failed:', error)
  }
}

// Auto-sync when coming back online
watch(isOnline, (online) => {
  if (online) {
    handleSync()
  }
})
</script>

<style scoped>
.offline-banner {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
  color: white;
  padding: 12px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 9999;
  backdrop-filter: blur(10px);
}

.offline-content {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
}

.offline-icon {
  font-size: 24px;
}

.offline-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.offline-text strong {
  font-size: 14px;
  font-weight: 600;
}

.offline-message {
  font-size: 12px;
  opacity: 0.9;
}

.sync-button {
  background: rgba(255, 255, 255, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.3);
  color: white;
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 6px;
}

.sync-button:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.3);
  transform: translateY(-1px);
}

.sync-button:disabled {
  cursor: not-allowed;
  opacity: 0.7;
}

.sync-button.syncing {
  padding-left: 12px;
}

.spinner {
  width: 14px;
  height: 14px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.sync-success {
  position: fixed;
  top: 60px;
  left: 50%;
  transform: translateX(-50%);
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  color: white;
  padding: 12px 24px;
  border-radius: 12px;
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
  z-index: 9998;
  font-size: 14px;
  font-weight: 500;
}

/* Transitions */
.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.3s ease;
}

.slide-down-enter-from {
  transform: translateY(-100%);
  opacity: 0;
}

.slide-down-leave-to {
  transform: translateY(-100%);
  opacity: 0;
}

@media (max-width: 768px) {
  .offline-banner {
    padding: 10px 16px;
  }

  .offline-text {
    font-size: 12px;
  }

  .offline-message {
    display: none;
  }

  .sync-button {
    padding: 6px 12px;
    font-size: 12px;
  }
}
</style>
