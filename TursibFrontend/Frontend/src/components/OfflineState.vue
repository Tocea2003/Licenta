<template>
  <div class="offline-state">
    <div class="offline-animation">
      <div class="wifi-icon">
        <div class="wifi-arc arc-1"></div>
        <div class="wifi-arc arc-2"></div>
        <div class="wifi-arc arc-3"></div>
        <div class="wifi-dot"></div>
        <div class="wifi-slash"></div>
      </div>
    </div>
    
    <h2>{{ title }}</h2>
    <p>{{ description }}</p>
    
    <div class="offline-actions">
      <button @click="handleRetry" class="btn-retry" :disabled="retrying">
        <span v-if="retrying" class="spinner-small"></span>
        {{ retrying ? 'Se verifică...' : '🔄 Încearcă din nou' }}
      </button>
      
      <button v-if="showOfflineMode" @click="handleOfflineMode" class="btn-offline">
        📱 Continuă Offline
      </button>
    </div>
    
    <div class="tips">
      <h3>Sugestii:</h3>
      <ul>
        <li>Verifică conexiunea la internet</li>
        <li>Verifică setările Wi-Fi sau datele mobile</li>
        <li>Încearcă să reîncarci pagina</li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const props = withDefaults(
  defineProps<{
    title?: string
    description?: string
    showOfflineMode?: boolean
  }>(),
  {
    title: 'Fără conexiune la internet',
    description: 'Nu ne putem conecta la server. Verifică conexiunea și încearcă din nou.',
    showOfflineMode: false,
  }
)

const emit = defineEmits<{
  retry: []
  offlineMode: []
}>()

const retrying = ref(false)

const handleRetry = async () => {
  retrying.value = true
  
  // Simulate network check
  await new Promise(resolve => setTimeout(resolve, 1000))
  
  retrying.value = false
  emit('retry')
}

const handleOfflineMode = () => {
  emit('offlineMode')
}
</script>

<style scoped>
.offline-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 48px 32px;
  min-height: 500px;
  background: var(--bg-primary);
}

/* WiFi Icon Animation */
.offline-animation {
  margin-bottom: 32px;
  position: relative;
}

.wifi-icon {
  position: relative;
  width: 100px;
  height: 100px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.wifi-arc {
  position: absolute;
  border: 4px solid #94a3b8;
  border-radius: 50%;
  opacity: 0.3;
}

.arc-1 {
  width: 80px;
  height: 80px;
  border-bottom-color: transparent;
  border-left-color: transparent;
  border-right-color: transparent;
  animation: pulse-arc 2s ease-in-out infinite;
}

.arc-2 {
  width: 60px;
  height: 60px;
  border-bottom-color: transparent;
  border-left-color: transparent;
  border-right-color: transparent;
  animation: pulse-arc 2s ease-in-out 0.3s infinite;
}

.arc-3 {
  width: 40px;
  height: 40px;
  border-bottom-color: transparent;
  border-left-color: transparent;
  border-right-color: transparent;
  animation: pulse-arc 2s ease-in-out 0.6s infinite;
}

@keyframes pulse-arc {
  0%, 100% {
    opacity: 0.3;
    transform: scale(1);
  }
  50% {
    opacity: 0.6;
    transform: scale(1.1);
  }
}

.wifi-dot {
  width: 12px;
  height: 12px;
  background: #94a3b8;
  border-radius: 50%;
  position: absolute;
  bottom: 20px;
}

.wifi-slash {
  position: absolute;
  width: 100px;
  height: 4px;
  background: #ef4444;
  transform: rotate(-45deg);
  border-radius: 2px;
  box-shadow: 0 2px 8px rgba(239, 68, 68, 0.4);
}

/* Content */
h2 {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

p {
  font-size: 1rem;
  color: var(--text-secondary);
  margin: 0 0 32px 0;
  max-width: 400px;
  line-height: 1.6;
}

/* Actions */
.offline-actions {
  display: flex;
  gap: 16px;
  margin-bottom: 48px;
  flex-wrap: wrap;
  justify-content: center;
}

.btn-retry,
.btn-offline {
  padding: 12px 28px;
  border-radius: 12px;
  font-weight: 600;
  font-size: 1rem;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn-retry {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  color: white;
}

.btn-retry:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 16px rgba(59, 130, 246, 0.3);
}

.btn-retry:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-offline {
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border-primary);
}

.btn-offline:hover {
  background: var(--bg-tertiary);
}

.spinner-small {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* Tips */
.tips {
  background: var(--bg-secondary);
  border: 1px solid var(--border-primary);
  border-radius: 16px;
  padding: 24px;
  max-width: 400px;
  width: 100%;
}

.tips h3 {
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 16px 0;
  text-align: left;
}

.tips ul {
  list-style: none;
  padding: 0;
  margin: 0;
  text-align: left;
}

.tips li {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 8px 0;
  color: var(--text-secondary);
  font-size: 0.95rem;
}

.tips li::before {
  content: '•';
  color: #3b82f6;
  font-weight: 700;
  font-size: 1.2rem;
}

/* Responsive */
@media (max-width: 640px) {
  .offline-state {
    padding: 32px 24px;
  }

  h2 {
    font-size: 1.5rem;
  }

  .offline-actions {
    flex-direction: column;
    width: 100%;
  }

  .btn-retry,
  .btn-offline {
    width: 100%;
    justify-content: center;
  }
}
</style>
