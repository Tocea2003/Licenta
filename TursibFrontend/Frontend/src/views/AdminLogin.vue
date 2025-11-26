<template>
  <div class="login-container">
    <div class="login-card">
      <div class="login-header">
        <h1>🚌 Tursib Admin</h1>
        <p>Autentificare administrator</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
        <div class="form-group">
          <label for="username">Username</label>
          <input
            id="username"
            v-model="credentials.username"
            type="text"
            placeholder="Introdu username"
            required
            autocomplete="username"
          />
        </div>

        <div class="form-group">
          <label for="password">Parolă</label>
          <input
            id="password"
            v-model="credentials.password"
            type="password"
            placeholder="Introdu parola"
            required
            autocomplete="current-password"
          />
        </div>

        <button type="submit" class="btn-login" :disabled="isLoading">
          {{ isLoading ? 'Se conectează...' : 'Login' }}
        </button>

        <p v-if="errorMessage" class="error-message">{{ errorMessage }}</p>
      </form>

      <div class="login-footer">
        <router-link to="/" class="back-link">← Înapoi la hartă</router-link>
      </div>
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
    localStorage.setItem('admin_token', response.token)
    localStorage.setItem('admin_user', JSON.stringify({
      username: response.username,
      role: response.role
    }))

    console.log('💾 Token saved, redirecting to /admin/routes')
    
    // Redirect la dashboard după ce token-ul e salvat
    await router.push('/admin/routes')
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
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
}

.login-card {
  background: white;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  padding: 40px;
  width: 100%;
  max-width: 420px;
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.login-header h1 {
  font-size: 32px;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 8px 0;
}

.login-header p {
  color: #6b7280;
  font-size: 14px;
  margin: 0;
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
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.form-group input {
  padding: 12px 16px;
  border: 2px solid #e5e7eb;
  border-radius: 8px;
  font-size: 16px;
  transition: all 0.2s;
}

.form-group input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.btn-login {
  padding: 14px 24px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  margin-top: 8px;
}

.btn-login:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 16px rgba(102, 126, 234, 0.3);
}

.btn-login:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error-message {
  color: #ef4444;
  font-size: 14px;
  text-align: center;
  margin: 0;
  padding: 12px;
  background: #fee2e2;
  border-radius: 8px;
}

.login-footer {
  margin-top: 24px;
  text-align: center;
}

.back-link {
  color: #667eea;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  transition: color 0.2s;
}

.back-link:hover {
  color: #764ba2;
}
</style>
