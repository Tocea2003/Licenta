<template>
  <div class="google-auth-container">
    <!-- Buton custom Google Sign-In -->
    <button @click="handleGoogleLogin" class="google-signin-btn" :disabled="isLoading">
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
      
      <span class="button-text">{{ isLoading ? 'Se conectează...' : 'Continuă cu Google' }}</span>
    </button>

    <!-- Google Sign-In button oficial (ascuns, folosit programatic) -->
    <div ref="googleButtonRef" class="google-button-container"></div>

    <!-- Eroare -->
    <div v-if="errorMessage" class="error-message">
      <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
        <circle cx="10" cy="10" r="9" stroke="#ef4444" stroke-width="2"/>
        <path d="M10 6v4M10 14h.01" stroke="#ef4444" stroke-width="2" stroke-linecap="round"/>
      </svg>
      <span>{{ errorMessage }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { googleAuthService, type GoogleAuthResponse } from '../services/googleAuthService'
import { authService } from '../services/adminService'

const emit = defineEmits<{
  success: [user: any]
  error: [error: string]
}>()

const googleButtonRef = ref<HTMLElement | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
const isInitialized = ref(false)

const handleGoogleResponse = async (response: GoogleAuthResponse) => {
  console.log('📝 Google response received')
  isLoading.value = true
  errorMessage.value = ''

  try {
    // Parse JWT token pentru a vedea datele utilizatorului
    const googleUser = googleAuthService.parseJwt(response.credential)
    console.log('👤 Google user:', googleUser)

    // Validează token-ul pe backend
    const result = await googleAuthService.validateToken(response.credential)
    
    if (!result) {
      throw new Error('Nu s-a putut valida token-ul Google')
    }

    console.log('✅ Login cu Google reușit!')
    emit('success', result)
  } catch (error: any) {
    console.error('❌ Eroare la login cu Google:', error)
    errorMessage.value = error.message || 'Eroare la autentificare'
    emit('error', errorMessage.value)
  } finally {
    isLoading.value = false
  }
}

const handleGoogleLogin = () => {
  if (!isInitialized.value) {
    errorMessage.value = 'Google Sign-In nu este încă inițializat'
    return
  }

  // Trigger click pe butonul Google ascuns
  const googleBtn = googleButtonRef.value?.querySelector('div[role="button"]') as HTMLElement
  if (googleBtn) {
    googleBtn.click()
  } else {
    errorMessage.value = 'Butonul Google nu este disponibil'
  }
}

const initializeGoogleSignIn = async () => {
  try {
    await googleAuthService.initialize()

    if (googleButtonRef.value) {
      googleAuthService.renderButton(googleButtonRef.value, handleGoogleResponse)
      isInitialized.value = true
    }
  } catch (error: any) {
    console.error('❌ Eroare la inițializarea Google Sign-In:', error)
    if (error?.message === 'GOOGLE_NOT_CONFIGURED') {
      errorMessage.value = 'Autentificarea Google nu este configurată. Contactează administratorul.'
    } else {
      errorMessage.value = 'Nu s-a putut inițializa autentificarea Google'
    }
  }
}

onMounted(() => {
  initializeGoogleSignIn()
})

onUnmounted(() => {
  googleAuthService.signOut()
})
</script>

<style scoped>
.google-auth-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.google-signin-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 12px 24px;
  background: white;
  border: 2px solid #dadce0;
  border-radius: 8px;
  font-size: 15px;
  font-weight: 600;
  color: #3c4043;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 280px;
  font-family: 'Roboto', 'Arial', sans-serif;
}

.google-signin-btn:hover:not(:disabled) {
  background: #f8f9fa;
  border-color: #d2d4d8;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.google-signin-btn:active:not(:disabled) {
  background: #f1f3f4;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
}

.google-signin-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

@media (prefers-color-scheme: dark) {
  .google-signin-btn {
    background: #2d2d2d;
    border-color: #5f6368;
    color: #e8eaed;
  }
  
  .google-signin-btn:hover:not(:disabled) {
    background: #3c3c3c;
    border-color: #6f7378;
  }
  
  .google-signin-btn:active:not(:disabled) {
    background: #454545;
  }
}

.google-icon {
  flex-shrink: 0;
}

.button-text {
  white-space: nowrap;
}

.loading-spinner {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.google-button-container {
  position: absolute;
  opacity: 0;
  pointer-events: none;
  width: 0;
  height: 0;
  overflow: hidden;
}

.error-message {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 8px;
  color: #991b1b;
  font-size: 14px;
  animation: slideIn 0.3s ease-out;
}

@media (prefers-color-scheme: dark) {
  .error-message {
    background: rgba(239, 68, 68, 0.1);
    border-color: rgba(239, 68, 68, 0.3);
    color: #fca5a5;
  }
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>
