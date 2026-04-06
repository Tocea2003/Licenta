<template>
  <div class="settings-page">
    <div class="header">
      <h1>⚙️ {{ t('settings') }}</h1>
      <p class="subtitle">{{ t('personalizeExperience') }}</p>
    </div>

    <div class="content">
      <!-- Dark Mode Section -->
      <div class="setting-section">
        <h2>🎨 {{ t('appearance') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <h3>{{ t('darkMode') }}</h3>
            <p>{{ t('darkModeDesc') }}</p>
          </div>
          <button 
            @click="toggleDarkMode" 
            class="toggle-btn"
            :class="{ active: isDarkMode }"
            :aria-label="isDarkMode ? t('disableDarkMode') : t('enableDarkMode')"
          >
            <span class="toggle-slider"></span>
          </button>
        </div>
      </div>

      <!-- Notifications Section -->
      <div class="setting-section">
        <h2>🔔 {{ t('notifications') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <h3>{{ t('pushNotifications') }}</h3>
            <p>{{ t('pushNotificationsDesc') }}</p>
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

      <!-- Language Section -->
      <div class="setting-section">
        <h2>🌐 {{ t('language') }}</h2>
        <div class="setting-item language-item">
          <div class="setting-info">
            <h3>{{ languageName }}</h3>
            <p>{{ t('switchToLanguage', 'Switch the app to {language}', { language: nextLanguageName }) }}</p>
          </div>
          <div class="language-buttons" role="group" :aria-label="t('language')">
            <button
              v-for="language in languageOptions"
              :key="language"
              @click="setLanguage(language)"
              class="lang-btn"
              :class="{ active: currentLanguage === language }"
              :aria-label="t('switchLanguageAria', 'Switch language to {language}', { language: getLanguageDisplay(language) })"
            >
              {{ getLanguageDisplay(language) }}
            </button>
          </div>
        </div>
      </div>

      <!-- Statistics Section -->
      <div class="setting-section">
        <h2>📊 {{ t('statistics') }}</h2>
        <router-link to="/statistics" class="setting-link">
          <div class="setting-info">
            <h3>{{ t('myStatistics') }}</h3>
            <p>{{ t('usageStatisticsDesc') }}</p>
          </div>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </router-link>
      </div>

      <!-- Account Section -->
      <div class="setting-section">
        <h2>👤 {{ t('account') }}</h2>
        <router-link to="/login" class="setting-link">
          <div class="setting-info">
            <h3>{{ t('authentication') }}</h3>
            <p>{{ t('signInAccountDesc') }}</p>
          </div>
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
            <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </router-link>
      </div>

      <!-- About Section -->
      <div class="setting-section">
        <h2>ℹ️ {{ t('information') }}</h2>
        <div class="info-items">
          <div class="info-item">
            <span class="label">{{ t('version') }}</span>
            <span class="value">1.0.0</span>
          </div>
          <div class="info-item">
            <span class="label">{{ t('build') }}</span>
            <span class="value">2026.01.15</span>
          </div>
          <router-link to="/about" class="setting-link">
            <div class="setting-info">
              <h3>{{ t('aboutApp') }}</h3>
              <p>{{ t('aboutAppDesc') }}</p>
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
import { computed, ref } from 'vue'
import { useDarkMode } from '@/composables/useDarkMode'
import { useLanguage, type Language } from '@/composables/useLanguage'

const { isDarkMode, toggleDarkMode } = useDarkMode()
const { currentLanguage, setLanguage, t } = useLanguage()
const notificationsEnabled = ref(false)
const languageOptions: Language[] = ['ro', 'en', 'de']

const getLanguageDisplay = (language: Language) => {
  if (language === 'ro') return t('romanian')
  if (language === 'en') return t('english')
  return t('german')
}

const languageName = computed(() => getLanguageDisplay(currentLanguage.value))

const nextLanguageName = computed(() => {
  if (currentLanguage.value === 'ro') return getLanguageDisplay('en')
  if (currentLanguage.value === 'en') return getLanguageDisplay('de')
  return getLanguageDisplay('ro')
})

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
  font-size: 11px;
  font-weight: 600;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.07em;
  margin: 0 0 10px 0;
  padding-left: 4px;
}

.setting-item,
.setting-link {
  background: var(--bg-primary);
  border: 1.5px solid var(--border-primary);
  border-radius: var(--radius-lg, 14px);
  padding: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  transition: border-color 0.2s ease, box-shadow 0.2s ease, transform 0.15s ease;
  text-decoration: none;
  color: inherit;
  box-shadow: var(--shadow-xs);
}

.setting-link:hover {
  transform: translateY(-1px);
  box-shadow: var(--shadow-md);
  border-color: var(--border-secondary);
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
  height: 30px;
  background: var(--border-secondary);
  border: none;
  border-radius: var(--radius-full, 999px);
  cursor: pointer;
  transition: background 0.25s ease;
  flex-shrink: 0;
  -webkit-tap-highlight-color: transparent;
}

.toggle-btn.active {
  background: var(--accent-primary);
}

.language-item {
  align-items: center;
}

.language-buttons {
  display: flex;
  gap: 8px;
}

.lang-btn {
  border: 1px solid var(--border-primary);
  border-radius: 8px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-size: 0.8rem;
  font-weight: 700;
  padding: 8px 10px;
  cursor: pointer;
  transition: all 0.2s;
}

.lang-btn:hover {
  border-color: #3b82f6;
}

.lang-btn.active {
  background: #3b82f6;
  color: white;
  border-color: #3b82f6;
}

.toggle-slider {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 24px;
  height: 24px;
  background: white;
  border-radius: 50%;
  transition: transform 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
}

.toggle-btn.active .toggle-slider {
  transform: translateX(22px);
}

/* Info Items */
.info-items {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-item {
  background: var(--bg-primary);
  border: 1.5px solid var(--border-primary);
  border-radius: var(--radius-lg, 14px);
  padding: 14px 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: var(--shadow-xs);
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
