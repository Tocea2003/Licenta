<template>
  <div class="signup-container">
    <div class="signup-card">
      <div class="signup-header">
        <div class="logo-container">
          <div class="logo-icon">🚌</div>
          <h1>{{ t('signupTitle') }}</h1>
        </div>
        <p class="subtitle">{{ t('signupSubtitle') }}</p>
      </div>

      <form @submit.prevent="handleSignUp" class="signup-form">
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
              :placeholder="t('chooseUsername')"
              required
              autocomplete="username"
              :disabled="isLoading"
              minlength="3"
              :class="{ 
                'input-valid': usernameValid === true,
                'input-invalid': usernameValid === false
              }"
            />
            <span v-if="usernameValid !== null" class="validation-icon">
              {{ usernameValid ? '✓' : '✗' }}
            </span>
          </div>
          <small class="helper-text">{{ t('min3Chars') }}</small>
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
              :placeholder="t('choosePassword')"
              required
              autocomplete="new-password"
              :disabled="isLoading"
              minlength="6"
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
          
          <!-- Password Strength Indicator -->
          <div v-if="credentials.password" class="password-strength">
            <div class="strength-bar">
              <div 
                class="strength-fill" 
                :style="{ 
                  width: `${((passwordStrength?.score ?? 0) / 5) * 100}%`,
                  backgroundColor: passwordStrength?.color ?? '#cbd5e0'
                }"
              ></div>
            </div>
            <small 
              class="strength-label" 
              :style="{ color: passwordStrength?.color ?? '#718096' }"
            >
              {{ passwordStrength?.label ?? '' }}
            </small>
          </div>
          <small class="helper-text">{{ t('min6Chars') }}</small>
        </div>

        <div class="form-group">
          <label for="confirmPassword">
            <span class="label-icon">🔒</span>
            {{ t('confirmPassword') }}
          </label>
          <div class="input-wrapper">
            <input
              id="confirmPassword"
              v-model="confirmPassword"
              :type="showConfirmPassword ? 'text' : 'password'"
              :placeholder="t('confirmYourPassword')"
              required
              autocomplete="new-password"
              :disabled="isLoading"
              :class="{ 
                'input-valid': confirmValid === true,
                'input-invalid': confirmValid === false
              }"
            />
            <button 
              type="button" 
              class="toggle-password"
              @click="toggleConfirmPasswordVisibility"
              :aria-label="showConfirmPassword ? t('hidePassword') : t('showPassword')"
            >
              {{ showConfirmPassword ? '👁️' : '👁️‍🗨️' }}
            </button>
          </div>
          <small v-if="confirmPassword && !isPasswordMatch" class="error-text">
            {{ t('passwordsDoNotMatch') }}
          </small>
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="error-banner">
            <span class="error-icon">⚠️</span>
            <span>{{ errorMessage }}</span>
          </div>
        </Transition>

        <Transition name="fade">
          <div v-if="successMessage" class="success-banner">
            <span class="success-icon">✅</span>
            <span>{{ successMessage }}</span>
          </div>
        </Transition>

        <button type="submit" class="btn-signup" :disabled="isLoading || !isPasswordMatch || !credentials.username || !credentials.password">
          <span v-if="isLoading" class="spinner"></span>
          <span v-else class="signup-icon">✓</span>
          {{ isLoading ? t('creatingAccount') : t('createAccount') }}
        </button>

        <div class="signup-info">
          <small>💡 {{ t('accountHint') }}</small>
        </div>
      </form>

      <div class="signup-footer">
        <router-link to="/login" class="login-link">
          {{ t('alreadyHaveAccount') }} <strong>{{ t('loginNow') }}</strong>
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
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { authService } from '@/services/adminService'
import { useLanguage } from '@/composables/useLanguage'

const router = useRouter()
const { t } = useLanguage()

const credentials = ref({
  username: '',
  password: ''
})

const confirmPassword = ref('')
const isLoading = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const showPassword = ref(false)
const showConfirmPassword = ref(false)

// Live validation states
const usernameValid = ref<boolean | null>(null)
const passwordValid = ref<boolean | null>(null)
const confirmValid = ref<boolean | null>(null)

// Password strength calculation
const passwordStrength = computed(() => {
  const password = credentials.value.password
  if (!password) return { score: 0, label: '', color: '' }
  
  let score = 0
  if (password.length >= 6) score++
  if (password.length >= 10) score++
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) score++
  if (/\d/.test(password)) score++
  if (/[^a-zA-Z\d]/.test(password)) score++
  
  const levels = [
    { score: 0, label: '', color: '' },
    { score: 1, label: t('veryWeak'), color: '#f56565' },
    { score: 2, label: t('weak'), color: '#ed8936' },
    { score: 3, label: t('medium'), color: '#ecc94b' },
    { score: 4, label: t('good'), color: '#48bb78' },
    { score: 5, label: t('excellent'), color: '#38a169' }
  ]
  
  return levels[score]
})

const isPasswordMatch = computed(() => {
  if (!confirmPassword.value) return true
  return credentials.value.password === confirmPassword.value
})

// Live validation watchers
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

watch(confirmPassword, (newVal) => {
  if (!newVal) {
    confirmValid.value = null
    return
  }
  confirmValid.value = newVal === credentials.value.password
})

const handleSignUp = async () => {
  errorMessage.value = ''
  successMessage.value = ''

  // Validare parolă
  if (credentials.value.password !== confirmPassword.value) {
    errorMessage.value = t('passwordsDoNotMatch')
    return
  }

  if (credentials.value.password.length < 6) {
    errorMessage.value = t('min6Chars')
    return
  }

  if (credentials.value.username.length < 3) {
    errorMessage.value = t('min3Chars')
    return
  }

  isLoading.value = true

  try {
    console.log('📝 Attempting registration with:', credentials.value.username)
    await authService.register(credentials.value)
    
    console.log('✅ Registration successful')
    successMessage.value = t('accountCreatedRedirect')

    // Auto-login după înregistrare
    setTimeout(async () => {
      try {
        const response = await authService.login(credentials.value)
        
        // Salvează token și user info
        localStorage.setItem('token', response.token)
        localStorage.setItem('user', JSON.stringify({
          username: response.username,
          role: response.role
        }))

        console.log('✅ Auto-login successful, reloading page')
        window.location.href = '/'
      } catch (error) {
        console.error('❌ Auto-login error:', error)
        // Dacă auto-login eșuează, redirecționează la login
        await router.push('/login')
      }
    }, 1500)
    
  } catch (error: any) {
    console.error('❌ Registration error:', error)
    console.error('Error details:', error.response?.data)
    errorMessage.value = error.response?.data?.message || t('signupGenericError')
  } finally {
    isLoading.value = false
  }
}

const togglePasswordVisibility = () => {
  showPassword.value = !showPassword.value
}

const toggleConfirmPasswordVisibility = () => {
  showConfirmPassword.value = !showConfirmPassword.value
}
</script>

<style scoped>
.signup-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  position: relative;
  overflow: hidden;
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

.signup-card {
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border-radius: 24px;
  padding: 48px;
  max-width: 480px;
  width: 100%;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  position: relative;
  z-index: 1;
  animation: slideIn 0.5s ease-out;
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

.signup-header {
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
  font-weight: 700;
  color: #2d3748;
  margin: 0;
}

.subtitle {
  color: #718096;
  margin: 8px 0 0 0;
  font-size: 14px;
}

.signup-form {
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
  color: #2d3748;
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
  border-color: #48bb78;
  background-color: #f0fff4;
}

input.input-invalid {
  border-color: #f56565;
  background-color: #fff5f5;
}

input {
  padding: 14px 16px;
  border: 2px solid #e2e8f0;
  border-radius: 12px;
  font-size: 15px;
  transition: all 0.3s ease;
  background: white;
}

input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

input:disabled {
  background-color: #f7fafc;
  cursor: not-allowed;
  opacity: 0.6;
}

.password-strength {
  margin-top: 8px;
}

.strength-bar {
  height: 4px;
  background: #e2e8f0;
  border-radius: 2px;
  overflow: hidden;
  margin-bottom: 4px;
}

.strength-fill {
  height: 100%;
  transition: all 0.3s ease;
  border-radius: 2px;
}

.strength-label {
  font-size: 12px;
  font-weight: 600;
  display: block;
}

.helper-text {
  color: #718096;
  font-size: 12px;
  margin-top: 4px;
  display: block;
}

.error-text {
  color: #f56565;
  font-size: 12px;
  margin-top: 4px;
  display: block;
  font-weight: 500;
}

.error-banner {
  background: #fed7d7;
  color: #c53030;
  padding: 12px 16px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  animation: shake 0.5s ease;
}

.success-banner {
  background: #c6f6d5;
  color: #2f855a;
  padding: 12px 16px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-10px); }
  75% { transform: translateX(10px); }
}

.error-icon, .success-icon {
  font-size: 18px;
}

.btn-signup {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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

.btn-signup:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(102, 126, 234, 0.4);
}

.btn-signup:active:not(:disabled) {
  transform: translateY(0);
}

.btn-signup:disabled {
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

.signup-icon {
  font-size: 20px;
}

.signup-info {
  text-align: center;
  color: #718096;
  margin-top: -8px;
}

.signup-footer {
  margin-top: 32px;
  padding-top: 24px;
  border-top: 1px solid #e2e8f0;
  display: flex;
  flex-direction: column;
  gap: 16px;
  align-items: center;
}

.login-link {
  color: #667eea;
  text-decoration: none;
  font-size: 14px;
  transition: color 0.3s ease;
}

.login-link:hover {
  color: #764ba2;
}

.login-link strong {
  font-weight: 700;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #718096;
  text-decoration: none;
  font-size: 14px;
  transition: all 0.3s ease;
}

.back-link:hover {
  color: #2d3748;
  transform: translateX(-4px);
}

.fade-enter-active, .fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from, .fade-leave-to {
  opacity: 0;
}

@media (max-width: 640px) {
  .signup-card {
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
