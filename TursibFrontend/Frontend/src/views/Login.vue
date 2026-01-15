<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <div class="logo-container">
          <div class="logo-icon">🚌</div>
          <h1>Autentificare Tursib</h1>
        </div>
        <p class="subtitle">Conectează-te pentru a accesa favoritele tale</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
        <div class="form-group">
          <label for="username">
            <span class="label-icon">👤</span>
            Utilizator
          </label>
          <input
            id="username"
            v-model="credentials.username"
            type="text"
            placeholder="Introdu username-ul"
            required
            autocomplete="username"
            :disabled="isLoading"
          />
        </div>

        <div class="form-group">
          <label for="password">
            <span class="label-icon">🔒</span>
            Parolă
          </label>
          <input
            id="password"
            v-model="credentials.password"
            type="password"
            placeholder="Introdu parola"
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
          {{ isLoading ? 'Se conectează...' : 'Autentificare' }}
        </button>

        <div class="login-info">
          <small>💡 Autentifică-te pentru a salva locațiile favorite</small>
        </div>
      </form>

      <div class="login-footer">
        <router-link to="/signup" class="signup-link">
          Nu ai cont? <strong>Creează unul</strong>
        </router-link>
        <router-link to="/" class="back-link">
          <span>←</span>
          Înapoi la hartă
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

const router = useRouter()

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
    errorMessage.value = error.response?.data?.message || 'Username sau parolă incorectă'
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
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
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
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

input:disabled {
  background-color: var(--bg-tertiary);
  cursor: not-allowed;
  opacity: 0.6;
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

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-10px); }
  75% { transform: translateX(10px); }
}

.error-icon {
  font-size: 18px;
}

.btn-login {
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
  color: #718096;
  margin-top: -8px;
}

.login-footer {
  margin-top: 32px;
  padding-top: 24px;
  border-top: 1px solid #e2e8f0;
  display: flex;
  flex-direction: column;
  gap: 16px;
  align-items: center;
}

.signup-link {
  color: #667eea;
  text-decoration: none;
  font-size: 14px;
  transition: color 0.3s ease;
}

.signup-link:hover {
  color: #764ba2;
}

.signup-link strong {
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
