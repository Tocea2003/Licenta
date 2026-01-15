<template>
  <div class="settings-page">
    <div class="header">
      <h1>⚙️ Setări</h1>
      <p class="subtitle">Personalizează experiența ta</p>
    </div>

    <div class="content">
      <!-- Dark Mode Section -->
      <div class="setting-section">
        <h2>🎨 Aspect</h2>
        <div class="setting-item">
          <div class="setting-info">
            <h3>Mod Întunecat</h3>
            <p>Schimbă tema aplicației</p>
          </div>
          <button 
            @click="toggleDarkMode" 
            class="toggle-btn"
            :class="{ active: isDarkMode }"
            :aria-label="isDarkMode ? 'Dezactivează modul întunecat' : 'Activează modul întunecat'"
          >
            <span class="toggle-slider"></span>
          </button>
        </div>
      </div>

      <!-- Notifications Section -->
      <div class="setting-section">
        <h2>🔔 Notificări</h2>
        <div class="setting-item">
          <div class="setting-info">
            <h3>Notificări Push</h3>
            <p>Primește alertes despre autobuze</p>
          </div>
          <button 
            @click="toggleNotifications" 
            class="toggle-btn"
            :class="{ active: notificationsEnabled }"
          >
            <span class="toggle-slider"></span>
          </button>
        </div>
      </div>

      <!-- Statistics Section -->
      <div class="setting-section">
        <h2>📊 Statistici</h2>
        <router-link to="/statistics" class="setting-link">
          <div class="setting-info">
            <h3>Statisticile Mele</h3>
            <p>Vezi statisticile de utilizare</p>
          </div>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </router-link>
      </div>

      <!-- Account Section -->
      <div class="setting-section">
        <h2>👤 Cont</h2>
        <router-link to="/login" class="setting-link">
          <div class="setting-info">
            <h3>Autentificare</h3>
            <p>Conectează-te la cont</p>
          </div>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </router-link>
      </div>

      <!-- About Section -->
      <div class="setting-section">
        <h2>ℹ️ Informații</h2>
        <div class="info-items">
          <div class="info-item">
            <span class="label">Versiune</span>
            <span class="value">1.0.0</span>
          </div>
          <div class="info-item">
            <span class="label">Build</span>
            <span class="value">2026.01.15</span>
          </div>
          <router-link to="/about" class="setting-link">
            <div class="setting-info">
              <h3>Despre Aplicație</h3>
              <p>Informații și dezvoltatori</p>
            </div>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useDarkMode } from '@/composables/useDarkMode'

const { isDarkMode, toggleDarkMode } = useDarkMode()
const notificationsEnabled = ref(false)

const toggleNotifications = () => {
  notificationsEnabled.value = !notificationsEnabled.value
  if (notificationsEnabled.value) {
    // Request notification permission
    if ('Notification' in window) {
      Notification.requestPermission().then(permission => {
        notificationsEnabled.value = permission === 'granted'
      })
    }
  }
}
</script>

<style scoped>
.settings-page {
  min-height: 100vh;
  background: var(--gradient-bg);
  padding-bottom: 100px;
}

.header {
  background: var(--gradient-primary);
  padding: 32px 24px;
  color: white;
  text-align: center;
}

.header h1 {
  margin: 0 0 8px 0;
  font-size: 2rem;
  font-weight: 800;
}

.subtitle {
  margin: 0;
  font-size: 0.95rem;
  opacity: 0.9;
}

.content {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.setting-section h2 {
  font-size: 0.875rem;
  font-weight: 700;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0 0 12px 0;
}

.setting-item,
.setting-link {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 12px;
  padding: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  transition: all 0.2s;
  text-decoration: none;
  color: inherit;
}

.setting-link:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.setting-info {
  flex: 1;
}

.setting-info h3 {
  margin: 0 0 4px 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-primary);
}

.setting-info p {
  margin: 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

/* Toggle Button */
.toggle-btn {
  position: relative;
  width: 52px;
  height: 28px;
  background: var(--border-secondary);
  border: none;
  border-radius: 14px;
  cursor: pointer;
  transition: background 0.3s;
  flex-shrink: 0;
}

.toggle-btn.active {
  background: #3b82f6;
}

.toggle-slider {
  position: absolute;
  top: 2px;
  left: 2px;
  width: 24px;
  height: 24px;
  background: white;
  border-radius: 50%;
  transition: transform 0.3s;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.toggle-btn.active .toggle-slider {
  transform: translateX(24px);
}

/* Info Items */
.info-items {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-item {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 12px;
  padding: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.info-item .label {
  font-size: 0.95rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.info-item .value {
  font-size: 0.95rem;
  color: var(--text-primary);
  font-weight: 600;
}
</style>
