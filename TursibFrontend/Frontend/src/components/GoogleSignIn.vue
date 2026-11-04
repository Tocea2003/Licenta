<template>
  <div class="google-auth-container">
    <button type="button" @click="handleGoogleLogin" class="google-signin-btn" :disabled="isLoading">
      <svg v-if="!isLoading" class="google-icon" viewBox="0 0 24 24" width="24" height="24">
        <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
        <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
        <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
        <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
      </svg>

      <svg v-else class="loading-spinner" width="24" height="24" viewBox="0 0 24 24">
        <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3" fill="none" opacity="0.25"/>
        <path fill="currentColor" d="M12 2a10 10 0 0 1 10 10h-3a7 7 0 0 0-7-7V2z"/>
      </svg>

      <span class="button-text">{{ isLoading ? t('connecting') : t('continueWithGoogle') }}</span>
    </button>

    <div v-if="errorMessage" class="error-message">
      <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
        <circle cx="10" cy="10" r="9" stroke="currentColor" stroke-width="2"/>
        <path d="M10 6v4M10 14h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
      </svg>
      <span>{{ errorMessage }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useLanguage } from '@/composables/useLanguage'
import { ref, onMounted } from 'vue'
import { googleAuthService } from '../services/googleAuthService'

const { t } = useLanguage()

const emit = defineEmits<{
  success: [user: any]
  error: [error: string]
}>()

const isLoading = ref(false)
const errorMessage = ref('')

const handleGoogleLogin = () => {
  googleAuthService.startLogin()
}

const handleCodeFromRedirect = async (code: string) => {
  isLoading.value = true
  errorMessage.value = ''
  googleAuthService.clearUrlCode()

  try {
    const result = await googleAuthService.exchangeCode(code)
    if (!result) {
      throw new Error('Nu s-a putut autentifica cu Google')
    }
    emit('success', result)
  } catch (error: any) {
    errorMessage.value = error.message || 'Eroare la autentificare'
    emit('error', errorMessage.value)
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  const code = googleAuthService.getCodeFromUrl()
  if (code) {
    handleCodeFromRedirect(code)
  }
})
</script>

<style scoped>
.google-auth-container {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 12px;
}

.google-signin-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  width: 100%;
  min-height: 52px;
  padding: 0 20px;
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  font: inherit;
  font-size: 16px;
  font-weight: var(--fw-bold);
  color: var(--text-primary);
  cursor: pointer;
  transition: var(--transition-colors);
}

.google-signin-btn:hover:not(:disabled) {
  background: var(--bg-tertiary);
}

.google-signin-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.google-icon { flex-shrink: 0; }
.button-text { white-space: nowrap; }

.loading-spinner { animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.error-message {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  background: var(--color-danger-soft);
  border-radius: var(--radius-md);
  color: var(--color-danger);
  font-size: 14px;
  font-weight: var(--fw-semibold);
}

</style>
