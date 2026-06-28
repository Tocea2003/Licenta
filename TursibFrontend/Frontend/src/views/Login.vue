<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <div class="logo-container">
          <div class="logo-icon">🚌</div>
          <h1>{{ t('loginTitle') }}</h1>
        </div>
        <p class="subtitle">{{ t('loginSubtitle') }}</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
        <div class="form-group">
          <label for="username">
            <span class="label-icon">👤</span>
            {{ t('username') }}
          </label>
          <div class="input-wrapper">
            <input
              id="username"
              v-model="credentials.username"
              type="text"
              :placeholder="t('enterUsername')"
              required
              autocomplete="username"
              :disabled="isLoading"
              :class="{ 
                'input-valid': usernameValid === true,
                'input-invalid': usernameValid === false
              }"
            />
            <span v-if="usernameValid !== null" class="validation-icon">
              {{ usernameValid ? '✓' : '✗' }}
            </span>
          </div>
        </div>

        <div class="form-group">
          <label for="password">
            <span class="label-icon">🔒</span>
            {{ t('password') }}
          </label>
          <div class="input-wrapper">
            <input
              id="password"
              v-model="credentials.password"
              :type="showPassword ? 'text' : 'password'"
              :placeholder="t('enterPassword')"
              required
              autocomplete="current-password"
              :disabled="isLoading"
              :class="{ 
                'input-valid': passwordValid === true,
                'input-invalid': passwordValid === false
              }"
            />
            <button 
              type="button" 
              class="toggle-password"
              @click="togglePasswordVisibility"
              :aria-label="showPassword ? t('hidePassword') : t('showPassword')"
            >
              {{ showPassword ? '👁️' : '👁️‍🗨️' }}
            </button>
          </div>
        </div>

        <div class="remember-me-wrapper">
          <label class="remember-me-label">
            <input 
              type="checkbox" 
              v-model="rememberMe"
              class="remember-checkbox"
            />
            <span class="checkbox-custom"></span>
            <span>{{ t('rememberMe') }}</span>
          </label>
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="error-banner">
            <span class="error-icon">⚠️</span>
            <span>{{ errorMessage }}</span>
          </div>
        </Transition>

        <button type="submit" class="btn-login" :disabled="isLoading || !credentials.username || !credentials.password">
          <span v-if="isLoading" class="spinner"></span>
          <span v-else class="login-icon">→</span>
          {{ isLoading ? t('connecting') : t('loginButton') }}
        </button>

        <div class="login-info">
          <small>💡 {{ t('loginHint') }}</small>
        </div>
      </form>

      <div class="divider">
        <span>{{ t('or') }}</span>
      </div>

      <GoogleSignIn 
        @success="handleGoogleSuccess"
        @error="handleGoogleError"
      />

      <div class="login-footer">
        <router-link to="/signup" class="signup-link">
          {{ t('noAccount') }} <strong>{{ t('createOne') }}</strong>
        </router-link>
        <router-link to="/" class="back-link">
          <span>←</span>
          {{ t('backToMap') }}
        </router-link>
      </div>
    </div>
    
    <div class="background-decoration">
      <div class="circle circle-1"></div>
      <div class="circle circle-2"></div>
      <div class="circle circle-3"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { authService } from '@/services/adminService'
import GoogleSignIn from '@/components/GoogleSignIn.vue'
import { useLanguage } from '@/composables/useLanguage'

const router = useRouter()
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
.login-container {
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--gradient-bg);
  position: relative;
  overflow-x: hidden;
  overflow-y: auto;
  padding: 20px;
}

.background-decoration {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
}

.circle {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
}

.circle-1 {
  width: 300px;
  height: 300px;
  top: -100px;
  right: -100px;
  animation: float 20s ease-in-out infinite;
}

.circle-2 {
  width: 200px;
  height: 200px;
  bottom: -50px;
  left: -50px;
  animation: float 15s ease-in-out infinite reverse;
}

.circle-3 {
  width: 150px;
  height: 150px;
  top: 50%;
  left: 10%;
  animation: float 25s ease-in-out infinite;
}

@keyframes float {
  0%, 100% {
    transform: translateY(0) rotate(0deg);
  }
  50% {
    transform: translateY(-30px) rotate(180deg);
  }
}

.login-card {
  background: var(--bg-primary);
  backdrop-filter: blur(10px);
  border-radius: 24px;
  padding: 48px;
  max-width: 480px;
  width: 100%;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  position: relative;
  z-index: 1;
  animation: slideIn 0.5s ease-out;
  /* margin auto keeps the card centered when it fits, but still lets the
     full card scroll into view on short screens (overflow stays reachable) */
  margin: auto;
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.login-header {
  text-align: center;
  margin-bottom: 40px;
}

.logo-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.logo-icon {
  font-size: 56px;
  animation: bounce 2s ease-in-out infinite;
}

@keyframes bounce {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

h1 {
  font-size: 28px;
  font-weight: 800;
  margin: 0;
  background: var(--gradient-primary);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  letter-spacing: -0.02em;
}

.subtitle {
  color: var(--text-secondary);
  margin: 8px 0 0 0;
  font-size: 14px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: var(--text-primary);
  font-size: 14px;
}

.label-icon {
  font-size: 16px;
}

.input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.input-wrapper input {
  flex: 1;
  padding-right: 45px;
}

.validation-icon {
  position: absolute;
  right: 14px;
  font-size: 18px;
  pointer-events: none;
  transition: all 0.3s ease;
}

.toggle-password {
  position: absolute;
  right: 12px;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 18px;
  padding: 4px;
  transition: all 0.2s ease;
  opacity: 0.6;
}

.toggle-password:hover {
  opacity: 1;
  transform: scale(1.1);
}

input.input-valid {
  border-color: var(--color-success);
  background-color: var(--color-success-soft);
}

input.input-invalid {
  border-color: var(--color-danger);
  background-color: var(--color-danger-soft);
}

input {
  padding: 14px 16px;
  border: 2px solid var(--border-color);
  border-radius: 12px;
  font-size: 15px;
  transition: all 0.3s ease;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

input:focus {
  outline: none;
  border-color: var(--accent-primary);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.12);
}

input:disabled {
  background-color: var(--bg-tertiary);
  cursor: not-allowed;
  opacity: 0.6;
}

.remember-me-wrapper {
  margin: -8px 0 8px 0;
}

.remember-me-label {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  user-select: none;
  font-size: 14px;
  color: var(--text-secondary);
  transition: color 0.3s ease;
}

.remember-me-label:hover {
  color: var(--text-primary);
}

.remember-checkbox {
  position: absolute;
  opacity: 0;
  cursor: pointer;
}

.checkbox-custom {
  width: 20px;
  height: 20px;
  border: 2px solid var(--border-color);
  border-radius: 6px;
  position: relative;
  transition: all 0.3s ease;
  background: var(--bg-secondary);
}

.remember-checkbox:checked + .checkbox-custom {
  background: var(--gradient-primary);
  border-color: var(--accent-primary);
}

.remember-checkbox:checked + .checkbox-custom::after {
  content: '✓';
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  color: white;
  font-size: 14px;
  font-weight: bold;
}

.error-banner {
  background: var(--color-danger-soft);
  color: var(--color-danger);
  border: 1px solid rgba(239, 68, 68, 0.3);
  padding: 12px 16px;
  border-radius: var(--radius-md, 12px);
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  animation: shake 0.5s ease;
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-10px); }
  75% { transform: translateX(10px); }
}

.error-icon {
  font-size: 18px;
}

.btn-login {
  background: var(--gradient-primary);
  color: white;
  border: none;
  padding: 16px 24px;
  border-radius: 12px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.3s ease;
  margin-top: 8px;
}

.btn-login:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(102, 126, 234, 0.4);
}

.btn-login:active:not(:disabled) {
  transform: translateY(0);
}

.btn-login:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.spinner {
  width: 18px;
  height: 18px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.login-icon {
  font-size: 20px;
}

.login-info {
  text-align: center;
  color: var(--text-secondary);
  margin-top: -8px;
}

.login-footer {
  margin-top: 32px;
  padding-top: 24px;
  border-top: 1px solid var(--border-primary);
  display: flex;
  flex-direction: column;
  gap: 16px;
  align-items: center;
  position: relative;
  z-index: 2;
}

.signup-link {
  color: var(--accent-primary);
  text-decoration: none;
  font-size: 14px;
  transition: color 0.3s ease;
}

.signup-link:hover {
  color: var(--accent-secondary);
}

.signup-link strong {
  font-weight: 700;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text-secondary);
  text-decoration: none;
  font-size: 14px;
  transition: all 0.3s ease;
}

.back-link:hover {
  color: var(--text-primary);
  transform: translateX(-4px);
}

.fade-enter-active, .fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from, .fade-leave-to {
  opacity: 0;
}

.divider {
  position: relative;
  text-align: center;
  margin: 32px 0;
}

.divider::before {
  content: '';
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  height: 1px;
  background: linear-gradient(to right, transparent, var(--border-primary), transparent);
}

.divider span {
  position: relative;
  display: inline-block;
  padding: 0 16px;
  background: var(--bg-primary);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 1px;
}

@media (max-width: 640px) {
  .login-card {
    padding: 32px 24px;
  }

  h1 {
    font-size: 24px;
  }

  .logo-icon {
    font-size: 48px;
  }
}
</style>
