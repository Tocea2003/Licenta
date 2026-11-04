<template>
  <main class="auth-page">
    <div class="auth-card">
      <router-link to="/" class="auth-brand">
        <span class="auth-brand-mark" aria-hidden="true"><ShieldCheck :size="20" /></span>
        <span class="auth-brand-name">Tursib · Admin</span>
      </router-link>

      <header>
        <h1 class="auth-title">{{ t('adminTitle') }}</h1>
        <p class="auth-subtitle">{{ t('adminWelcome') }}</p>
      </header>

      <form @submit.prevent="handleLogin" class="auth-form">
        <div class="auth-field">
          <label for="username" class="auth-label">{{ t('username') }}</label>
          <input
            id="username"
            v-model="credentials.username"
            type="text"
            class="auth-input"
            :placeholder="t('adminUsernamePlaceholder')"
            required
            autocomplete="username"
            :disabled="isLoading"
          />
        </div>

        <div class="auth-field">
          <label for="password" class="auth-label">{{ t('password') }}</label>
          <input
            id="password"
            v-model="credentials.password"
            type="password"
            class="auth-input"
            :placeholder="t('adminPasswordPlaceholder')"
            required
            autocomplete="current-password"
            :disabled="isLoading"
          />
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="auth-error" role="alert">
            <AlertTriangle :size="18" aria-hidden="true" />
            <span>{{ errorMessage }}</span>
          </div>
        </Transition>

        <button type="submit" class="auth-submit" :disabled="isLoading">
          <span v-if="isLoading" class="auth-spinner" aria-hidden="true"></span>
          <LogIn v-else :size="18" aria-hidden="true" />
          {{ isLoading ? t('connecting') : t('authentication') }}
        </button>

        <p class="auth-hint">{{ t('adminAccessRestricted') }}</p>
      </form>

      <footer class="auth-footer">
        <router-link to="/" class="auth-back"><ArrowLeft :size="16" aria-hidden="true" />{{ t('adminBackToMap') }}</router-link>
      </footer>
    </div>
  </main>
</template>

<script setup lang="ts">
import { AlertTriangle, ArrowLeft, LogIn, ShieldCheck } from 'lucide-vue-next'
import '@/assets/auth.css'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { authService } from '@/services/adminService'
import { useLanguage } from '@/composables/useLanguage'

const router = useRouter()
const { t } = useLanguage()

const credentials = ref({
  username: '',
  password: ''
})

const isLoading = ref(false)
const errorMessage = ref('')

const handleLogin = async () => {
  isLoading.value = true
  errorMessage.value = ''

  try {
    console.log('🔐 Attempting login with:', credentials.value.username)
    const response = await authService.login(credentials.value)
    
    console.log('✅ Login successful:', response)
    
    // Salvează token și user info
    const adminUser = {
      username: response.username,
      role: response.role
    }
    localStorage.setItem('admin_token', response.token)
    localStorage.setItem('admin_user', JSON.stringify(adminUser))

    console.log('💾 Token saved:', {
      token: response.token.substring(0, 20) + '...',
      user: adminUser
    })
    
    // Redirect la dashboard după ce token-ul e salvat
    await router.push('/admin/routes')
    console.log('✅ Redirect complete')
  } catch (error: any) {
    console.error('❌ Login error:', error)
    console.error('Error details:', error.response?.data)
    errorMessage.value = error.response?.data?.message || t('adminWrongCredentials')
  } finally {
    isLoading.value = false
  }
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
