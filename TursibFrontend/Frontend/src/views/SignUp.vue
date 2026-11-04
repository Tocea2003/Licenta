<template>
  <main class="auth-page">
    <div class="auth-card">
      <router-link to="/" class="auth-brand">
        <span class="auth-brand-mark" aria-hidden="true"><BusFront :size="20" /></span>
        <span class="auth-brand-name">Tursib</span>
      </router-link>

      <header>
        <h1 class="auth-title">{{ t('signupTitle') }}</h1>
        <p class="auth-subtitle">{{ t('signupSubtitle') }}</p>
      </header>

      <form @submit.prevent="handleSignUp" class="auth-form">
        <div class="auth-field">
          <label for="username" class="auth-label">{{ t('username') }}</label>
          <div class="auth-input-wrap">
            <input
              id="username"
              v-model="credentials.username"
              type="text"
              class="auth-input"
              :class="{ 'is-valid': usernameValid === true, 'is-invalid': usernameValid === false }"
              :placeholder="t('chooseUsername')"
              required
              autocomplete="username"
              :disabled="isLoading"
              minlength="3"
              aria-describedby="username-hint"
            />
            <span v-if="usernameValid !== null" class="auth-input-status" :class="usernameValid ? 'is-valid' : 'is-invalid'" aria-hidden="true">
              <Check v-if="usernameValid" :size="18" />
              <X v-else :size="18" />
            </span>
          </div>
          <small id="username-hint" class="auth-hint">{{ t('min3Chars') }}</small>
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
              :placeholder="t('choosePassword')"
              required
              autocomplete="new-password"
              :disabled="isLoading"
              minlength="6"
              aria-describedby="password-hint"
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

          <div v-if="credentials.password" class="password-strength">
            <div class="strength-bar" aria-hidden="true">
              <div
                class="strength-fill"
                :style="{
                  width: `${((passwordStrength?.score ?? 0) / 5) * 100}%`,
                  backgroundColor: passwordStrength?.color ?? 'var(--border-secondary)'
                }"
              ></div>
            </div>
            <small class="auth-hint">{{ passwordStrength?.label ?? '' }}</small>
          </div>
          <small id="password-hint" class="auth-hint">{{ t('min6Chars') }}</small>
        </div>

        <div class="auth-field">
          <label for="confirmPassword" class="auth-label">{{ t('confirmPassword') }}</label>
          <div class="auth-input-wrap">
            <input
              id="confirmPassword"
              v-model="confirmPassword"
              :type="showConfirmPassword ? 'text' : 'password'"
              class="auth-input"
              :class="{ 'is-valid': confirmValid === true, 'is-invalid': confirmValid === false }"
              :placeholder="t('confirmYourPassword')"
              required
              autocomplete="new-password"
              :disabled="isLoading"
            />
            <button
              type="button"
              class="auth-input-action"
              @click="toggleConfirmPasswordVisibility"
              :aria-label="showConfirmPassword ? t('hidePassword') : t('showPassword')"
            >
              <EyeOff v-if="showConfirmPassword" :size="18" aria-hidden="true" />
              <Eye v-else :size="18" aria-hidden="true" />
            </button>
          </div>
          <small v-if="confirmPassword && !isPasswordMatch" class="auth-hint is-error" role="alert">
            {{ t('passwordsDoNotMatch') }}
          </small>
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="auth-error" role="alert">
            <AlertTriangle :size="18" aria-hidden="true" />
            <span>{{ errorMessage }}</span>
          </div>
        </Transition>

        <Transition name="fade">
          <div v-if="successMessage" class="auth-success" role="status">
            <CheckCircle2 :size="18" aria-hidden="true" />
            <span>{{ successMessage }}</span>
          </div>
        </Transition>

        <button type="submit" class="auth-submit" :disabled="isLoading || !isPasswordMatch || !credentials.username || !credentials.password">
          <span v-if="isLoading" class="auth-spinner" aria-hidden="true"></span>
          <UserPlus v-else :size="18" aria-hidden="true" />
          {{ isLoading ? t('creatingAccount') : t('createAccount') }}
        </button>

        <p class="auth-hint">{{ t('accountHint') }}</p>
      </form>

      <footer class="auth-footer">
        <router-link to="/login">{{ t('alreadyHaveAccount') }} <strong>{{ t('loginNow') }}</strong></router-link>
        <router-link to="/" class="auth-back"><ArrowLeft :size="16" aria-hidden="true" />{{ t('backToMap') }}</router-link>
      </footer>
    </div>
  </main>
</template>

<script setup lang="ts">
import { AlertTriangle, ArrowLeft, BusFront, Check, CheckCircle2, Eye, EyeOff, UserPlus, X } from 'lucide-vue-next'
import '@/assets/auth.css'
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
    { score: 1, label: t('veryWeak'), color: 'var(--color-danger)' },
    { score: 2, label: t('weak'), color: '#ed8936' },
    { score: 3, label: t('medium'), color: '#ecc94b' },
    { score: 4, label: t('good'), color: 'var(--color-success)' },
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
.password-strength {
  display: flex;
  align-items: center;
  gap: 10px;
}

.strength-bar {
  flex: 1;
  height: 6px;
  border-radius: 3px;
  background: var(--bg-tertiary);
  overflow: hidden;
}

.strength-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.2s ease;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
