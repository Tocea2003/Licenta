<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <div class="logo-container">
          <div class="logo-icon">🚌</div>
          <h1>{{ t('adminTitle') }}</h1>
        </div>
        <p class="subtitle">{{ t('adminWelcome') }}</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
        <div class="form-group">
          <label for="username">
            <span class="label-icon">👤</span>
            {{ t('username') }}
          </label>
          <input
            id="username"
            v-model="credentials.username"
            type="text"
            :placeholder="t('adminUsernamePlaceholder')"
            required
            autocomplete="username"
            :disabled="isLoading"
          />
        </div>

        <div class="form-group">
          <label for="password">
            <span class="label-icon">🔒</span>
            {{ t('password') }}
          </label>
          <input
            id="password"
            v-model="credentials.password"
            type="password"
            :placeholder="t('adminPasswordPlaceholder')"
            required
            autocomplete="current-password"
            :disabled="isLoading"
          />
        </div>

        <Transition name="fade">
          <div v-if="errorMessage" class="error-banner">
            <span class="error-icon">⚠️</span>
            <span>{{ errorMessage }}</span>
          </div>
        </Transition>

        <button type="submit" class="btn-login" :disabled="isLoading">
          <span v-if="isLoading" class="spinner"></span>
          <span v-else class="login-icon">→</span>
          {{ isLoading ? t('connecting') : t('authentication') }}
        </button>

        <div class="login-info">
          <small>💡 {{ t('adminAccessRestricted') }}</small>
        </div>
      </form>

      <div class="login-footer">
        <router-link to="/" class="back-link">
          <span>←</span>
          {{ t('adminBackToMap') }}
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
.login-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--gradient-bg);
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
  right: -50px;
  animation: float 20s infinite;
}

.circle-2 {
  width: 200px;
  height: 200px;
  bottom: -50px;
  left: -30px;
  animation: float 15s infinite reverse;
}

.circle-3 {
  width: 150px;
  height: 150px;
  top: 50%;
  left: 10%;
  animation: float 25s infinite;
}

@keyframes float {
  0%, 100% { transform: translate(0, 0) scale(1); }
  33% { transform: translate(30px, -30px) scale(1.1); }
  66% { transform: translate(-20px, 20px) scale(0.9); }
}

.login-card {
  background: var(--bg-primary);
  border-radius: 24px;
  padding: 48px 40px;
  width: 100%;
  max-width: 440px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  position: relative;
  z-index: 1;
  animation: slideUp 0.5s ease-out;
}

@keyframes slideUp {
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
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin-bottom: 12px;
}

.logo-icon {
  font-size: 48px;
  animation: bounce 2s ease-in-out infinite;
}

@keyframes bounce {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}

.login-header h1 {
  font-size: 32px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.subtitle {
  font-size: 15px;
  color: var(--text-secondary);
  margin: 8px 0 0;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
}

.label-icon {
  font-size: 18px;
}

.form-group input {
  padding: 14px 16px;
  border: 2px solid var(--border-color);
  border-radius: 12px;
  font-size: 15px;
  transition: all 0.2s;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.form-group input:focus {
  outline: none;
  border-color: #667eea;
  background: var(--bg-primary);
  box-shadow: 0 0 0 4px rgba(102, 126, 234, 0.1);
}

.form-group input:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  background: #fee2e2;
  border: 1px solid #fca5a5;
  border-radius: 10px;
  color: #dc2626;
  font-size: 14px;
  font-weight: 500;
}

.error-icon {
  font-size: 18px;
}

.btn-login {
  padding: 16px 24px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin-top: 10px;
}

.btn-login:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(102, 126, 234, 0.4);
}

.btn-login:active:not(:disabled) {
  transform: translateY(0);
}

.btn-login:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.spinner {
  width: 18px;
  height: 18px;
  border: 3px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.login-icon {
  font-size: 20px;
  font-weight: bold;
}

.login-info {
  text-align: center;
  margin-top: 10px;
}

.login-info small {
  color: #64748b;
  font-size: 13px;
}

.login-footer {
  margin-top: 24px;
  text-align: center;
  padding-top: 24px;
  border-top: 1px solid #e2e8f0;
}

.back-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #667eea;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.2s;
}

.back-link:hover {
  color: #764ba2;
  gap: 8px;
}

.fade-enter-active, .fade-leave-active {
  transition: all 0.3s ease;
}

.fade-enter-from, .fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

@media (max-width: 640px) {
  .login-card {
    padding: 32px 24px;
  }
  
  .logo-icon {
    font-size: 40px;
  }
  
  .login-header h1 {
    font-size: 28px;
  }
}
</style>
