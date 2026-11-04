<template>
  <main class="auth-page">
    <div class="auth-card">
      <router-link to="/" class="auth-brand">
        <span class="auth-brand-mark" aria-hidden="true"><BusFront :size="20" /></span>
        <span class="auth-brand-name">Tursib</span>
      </router-link>

      <header>
        <h1 class="auth-title">{{ t('loginTitle') }}</h1>
        <p class="auth-subtitle">{{ t('loginSubtitle') }}</p>
      </header>

      <form @submit.prevent="handleLogin" class="auth-form">
        <div class="auth-field">
          <label for="username" class="auth-label">{{ t('username') }}</label>
          <div class="auth-input-wrap">
            <input
              id="username"
              v-model="credentials.username"
              type="text"
              class="auth-input"
              :class="{ 'is-valid': usernameValid === true, 'is-invalid': usernameValid === false }"
              :placeholder="t('enterUsername')"
              required
              autocomplete="username"
              :disabled="isLoading"
            />
            <span v-if="usernameValid !== null" class="auth-input-status" :class="usernameValid ? 'is-valid' : 'is-invalid'" aria-hidden="true">
              <Check v-if="usernameValid" :size="18" />
              <X v-else :size="18" />
            </span>
          </div>
        </div>

        <div class="auth-field">
          <label for="password" class="auth-label">{{ t('password') }}</label>
          <div class="auth-input-wrap">
            <input
              id="password"
              v-model="credentials.password"
              :type="showPassword ? 'text' : 'password'"
              class="auth-input"
              :class="{ 'is-valid': passwordValid === true, 'is-invalid': passwordValid === false }"
              :placeholder="t('enterPassword')"
              required
              autocomplete="current-password"
              :disabled="isLoading"
            />
            <button
              type="button"
              class="auth-input-action"
              @click="togglePasswordVisibility"
              :aria-label="showPassword ? t('hidePassword') : t('showPassword')"
            >
              <EyeOff v-if="showPassword" :size="18" aria-hidden="true" />
              <Eye v-else :size="18" aria-hidden="true" />
            </button>
          </div>
        </div>

        <label class="auth-check">
          <input type="checkbox" v-model="rememberMe" />
          <span>{{ t('rememberMe') }}</span>
        </label>

        <Transition name="fade">
          <div v-if="errorMessage" class="auth-error" role="alert">
            <AlertTriangle :size="18" aria-hidden="true" />
            <span>{{ errorMessage }}</span>
          </div>
        </Transition>

        <button type="submit" class="auth-submit" :disabled="isLoading || !credentials.username || !credentials.password">
          <span v-if="isLoading" class="auth-spinner" aria-hidden="true"></span>
          <LogIn v-else :size="18" aria-hidden="true" />
          {{ isLoading ? t('connecting') : t('loginButton') }}
        </button>

        <p class="auth-hint">{{ t('loginHint') }}</p>
      </form>

      <div class="auth-divider">{{ t('or') }}</div>

      <GoogleSignIn @success="handleGoogleSuccess" @error="handleGoogleError" />

      <footer class="auth-footer">
        <router-link to="/signup">{{ t('noAccount') }} <strong>{{ t('createOne') }}</strong></router-link>
        <router-link to="/" class="auth-back"><ArrowLeft :size="16" aria-hidden="true" />{{ t('backToMap') }}</router-link>
      </footer>
    </div>
  </main>
</template>

<script setup lang="ts">
import { AlertTriangle, ArrowLeft, BusFront, Check, Eye, EyeOff, LogIn, X } from 'lucide-vue-next'
import '@/assets/auth.css'
import { ref, watch, onMounted } from 'vue'
import { authService } from '@/services/adminService'
import GoogleSignIn from '@/components/GoogleSignIn.vue'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const credentials = ref({
  username: '',
  password: ''
})

const isLoading = ref(false)
const errorMessage = ref('')
const rememberMe = ref(false)
const showPassword = ref(false)
const usernameValid = ref<boolean | null>(null)
const passwordValid = ref<boolean | null>(null)

// Check for saved credentials on mount
onMounted(() => {
  const savedUsername = localStorage.getItem('saved_username')
  if (savedUsername) {
    credentials.value.username = savedUsername
    rememberMe.value = true
  }
})

// Live validation
watch(() => credentials.value.username, (newVal) => {
  if (!newVal) {
    usernameValid.value = null
    return
  }
  usernameValid.value = newVal.length >= 3
})

watch(() => credentials.value.password, (newVal) => {
  if (!newVal) {
    passwordValid.value = null
    return
  }
  passwordValid.value = newVal.length >= 6
})

const handleLogin = async () => {
  isLoading.value = true
  errorMessage.value = ''

  try {
    console.log('🔐 Attempting login with:', credentials.value.username)
    const response = await authService.login(credentials.value)
    
    console.log('✅ Login successful:', response)
    
    // Save credentials if remember me is checked
    if (rememberMe.value) {
      localStorage.setItem('saved_username', credentials.value.username)
    } else {
      localStorage.removeItem('saved_username')
    }
    
    // Salvează token și user info
    localStorage.setItem('token', response.token)
    localStorage.setItem('user', JSON.stringify({
      username: response.username,
      role: response.role
    }))

    console.log('💾 Token saved, reloading page to initialize favorites')
    
    // Reload page pentru a reinițializa toate composable-urile cu noul token
    window.location.href = '/'
    console.log('✅ Redirect complete')
  } catch (error: any) {
    console.error('❌ Login error:', error)
    console.error('Error details:', error.response?.data)
    errorMessage.value = error.response?.data?.message || t('wrongCredentials')
  } finally {
    isLoading.value = false
  }
}

const togglePasswordVisibility = () => {
  showPassword.value = !showPassword.value
}

const handleGoogleSuccess = (userData: any) => {
  console.log('✅ Google login successful:', userData)
  
  // Salvează token și user info
  localStorage.setItem('token', userData.token)
  localStorage.setItem('user', JSON.stringify({
    username: userData.username,
    role: userData.role
  }))

  console.log('💾 Token saved, reloading page to initialize favorites')
  
  // Reload page pentru a reinițializa toate composable-urile cu noul token
  window.location.href = '/'
}

const handleGoogleError = (error: string) => {
  console.error('❌ Google login error:', error)
  errorMessage.value = error || t('googleLoginError')
}
</script>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
