<template>
  <div class="settings-page">
    <div class="header">
      <h1>⚙️ {{ t('settings') }}</h1>
      <p class="subtitle">{{ t('personalizeExperience') }}</p>
    </div>

    <div class="content">
      <div class="content-wrap">
      <!-- Notifications Section -->
      <div class="setting-section">
        <div class="section-label">🔔 {{ t('notifications') }}</div>
        <div class="section-card">
          <div class="section-row">
            <div class="row-info">
              <div class="row-title">{{ t('pushNotifications') }}</div>
              <div class="row-desc">{{ t('pushNotificationsDesc') }}</div>
            </div>
            <button
              @click="toggleNotifications"
              class="toggle-btn"
              :class="{ active: notificationsEnabled }"
            >
              <span class="toggle-slider"></span>
            </button>
          </div>
          <div class="section-row">
            <div class="row-info">
              <div class="row-title">Sunet</div>
              <div class="row-desc">La notificări noi</div>
            </div>
            <button
              @click="soundEnabled = !soundEnabled"
              class="toggle-btn"
              :class="{ active: soundEnabled }"
            >
              <span class="toggle-slider"></span>
            </button>
          </div>
          <div class="section-row last">
            <div class="row-info">
              <div class="row-title">Vibrație</div>
            </div>
            <button
              @click="hapticsEnabled = !hapticsEnabled"
              class="toggle-btn"
              :class="{ active: hapticsEnabled }"
            >
              <span class="toggle-slider"></span>
            </button>
          </div>
        </div>
      </div>

      <!-- Appearance Section -->
      <div class="setting-section">
        <div class="section-label">🎨 {{ t('appearance') }}</div>
        <div class="section-card">
          <div class="section-row">
            <div class="row-info">
              <div class="row-title">{{ t('darkMode') }}</div>
            </div>
            <div class="segmented">
              <button
                v-for="opt in themeOptions"
                :key="opt.value"
                class="seg-btn"
                :class="{ active: themeMode === opt.value }"
                @click="setThemeMode(opt.value)"
              >{{ opt.label }}</button>
            </div>
          </div>
          <div class="section-row last">
            <div class="row-info">
              <div class="row-title">{{ t('language') }}</div>
            </div>
            <div class="segmented">
              <button
                v-for="lang in languageOptions"
                :key="lang"
                class="seg-btn"
                :class="{ active: currentLanguage === lang }"
                @click="setLanguage(lang)"
              >{{ getLanguageDisplay(lang) }}</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Statistics Section -->
      <div class="setting-section">
        <div class="section-label">📊 {{ t('statistics') }}</div>
        <div class="section-card">
          <router-link to="/statistics" class="section-row link-row last">
            <span class="link-icon">📊</span>
            <div class="row-info">
              <div class="row-title">{{ t('myStatistics') }}</div>
              <div class="row-desc">{{ t('usageStatisticsDesc') }}</div>
            </div>
            <svg class="chevron" width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </router-link>
        </div>
      </div>

      <!-- Account Section -->
      <div class="setting-section">
        <div class="section-label">👤 {{ t('account') }}</div>
        <div class="section-card">
          <router-link to="/login" class="section-row link-row">
            <span class="link-icon">👤</span>
            <div class="row-info"><div class="row-title">Profil</div></div>
            <svg class="chevron" width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </router-link>
          <router-link to="/login" class="section-row link-row">
            <span class="link-icon">🔒</span>
            <div class="row-info"><div class="row-title">Schimbă parola</div></div>
            <svg class="chevron" width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </router-link>
          <router-link to="/about" class="section-row link-row">
            <span class="link-icon">📊</span>
            <div class="row-info"><div class="row-title">Date și confidențialitate</div></div>
            <svg class="chevron" width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </router-link>
          <div class="section-row link-row last danger" @click="handleLogout">
            <span class="link-icon">🚪</span>
            <div class="row-info"><div class="row-title">Deconectare</div></div>
          </div>
        </div>
      </div>

      <!-- Info Section -->
      <div class="setting-section">
        <div class="section-label">ℹ️ {{ t('information') }}</div>
        <div class="section-card">
          <div class="section-row">
            <div class="row-info"><div class="row-title">{{ t('version') }}</div></div>
            <span class="row-value">1.0.0</span>
          </div>
          <router-link to="/about" class="section-row link-row last">
            <span class="link-icon">ℹ️</span>
            <div class="row-info">
              <div class="row-title">{{ t('aboutApp') }}</div>
            </div>
            <svg class="chevron" width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path d="M9 18l6-6-6-6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </router-link>
        </div>
      </div>

      <div class="footer-note">Tursib Tracker · Aplicație web pentru urmărirea în timp real a autobuzelor · v1.0.0 · made in Sibiu 🇷🇴</div>
      </div><!-- /content-wrap -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useDarkMode } from '@/composables/useDarkMode'
import { useLanguage, type Language } from '@/composables/useLanguage'

const { isDarkMode, toggleDarkMode } = useDarkMode()
const { currentLanguage, setLanguage, t } = useLanguage()

const notificationsEnabled = ref(false)
const soundEnabled = ref(true)
const hapticsEnabled = ref(false)

const languageOptions: Language[] = ['ro', 'en', 'de']

const themeOptions = [
  { value: 'light', label: '☀️ Light' },
  { value: 'dark',  label: '🌙 Dark' },
  { value: 'auto',  label: 'Auto' },
]

const themeMode = ref<'light' | 'dark' | 'auto'>(isDarkMode.value ? 'dark' : 'light')

const setThemeMode = (mode: 'light' | 'dark' | 'auto') => {
  themeMode.value = mode
  if (mode === 'dark' && !isDarkMode.value) toggleDarkMode()
  if (mode === 'light' && isDarkMode.value) toggleDarkMode()
  if (mode === 'auto') {
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
    if (prefersDark !== isDarkMode.value) toggleDarkMode()
  }
}

watch(isDarkMode, (val) => {
  if (themeMode.value !== 'auto') {
    themeMode.value = val ? 'dark' : 'light'
  }
})

const getLanguageDisplay = (language: Language) => {
  if (language === 'ro') return '🇷🇴 RO'
  if (language === 'en') return '🇬🇧 EN'
  return '🇩🇪 DE'
}

const toggleNotifications = () => {
  notificationsEnabled.value = !notificationsEnabled.value
  if (notificationsEnabled.value && 'Notification' in window) {
    Notification.requestPermission().then(permission => {
      notificationsEnabled.value = permission === 'granted'
    })
  }
}

const handleLogout = () => {
  // logout logic placeholder
}
</script>

<style scoped>
.settings-page {
  min-height: 100%;
  background: var(--gradient-bg);
  padding-bottom: var(--space-6);
}

.header {
  background: var(--gradient-primary);
  padding: var(--space-8) var(--space-6);
  color: white;
  text-align: center;
}

.header h1 {
  margin: 0 0 var(--space-2) 0;
  font-size: var(--text-3xl);
  font-weight: 800;
  color: white;
}

.subtitle {
  margin: 0;
  font-size: var(--text-sm);
  opacity: 0.9;
  color: white;
}

.content {
  padding: var(--space-6) var(--space-4);
  display: flex;
  flex-direction: column;
}

.content-wrap {
  max-width: 600px;
  margin: 0 auto;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

/* Section container */
.setting-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.section-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.07em;
  padding: 0 4px;
}

/* Grouped card */
.section-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  overflow: hidden;
  box-shadow: var(--shadow-xs);
}

/* Row inside card */
.section-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--bg-tertiary);
  text-decoration: none;
  color: inherit;
}

.section-row.last,
.section-row:last-child {
  border-bottom: none;
}

.link-row {
  cursor: pointer;
  transition: background 0.15s;
}

.link-row:hover {
  background: var(--bg-secondary);
}

.link-icon {
  font-size: 18px;
  flex-shrink: 0;
}

.chevron {
  color: var(--text-tertiary);
  flex-shrink: 0;
}

.row-info {
  flex: 1;
  min-width: 0;
}

.row-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
  line-height: 1.2;
}

.row-desc {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 2px;
  line-height: 1.3;
}

.row-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-tertiary);
}

.danger .row-title {
  color: var(--color-danger);
}

/* Toggle */
.toggle-btn {
  position: relative;
  width: 48px;
  height: 28px;
  background: var(--border-secondary);
  border: none;
  border-radius: var(--radius-full);
  cursor: pointer;
  transition: background 0.25s ease;
  flex-shrink: 0;
  -webkit-tap-highlight-color: transparent;
}

.toggle-btn.active {
  background: var(--accent-primary);
}

.toggle-slider {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 22px;
  height: 22px;
  background: white;
  border-radius: 50%;
  transition: transform 0.25s var(--ease-out-back, cubic-bezier(0.34, 1.56, 0.64, 1));
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
}

.toggle-btn.active .toggle-slider {
  transform: translateX(20px);
}

/* Segmented control */
.segmented {
  display: inline-flex;
  background: var(--bg-tertiary);
  border-radius: 10px;
  padding: 3px;
  gap: 2px;
  flex-shrink: 0;
}

.seg-btn {
  padding: 6px 10px;
  border-radius: 8px;
  border: 0;
  cursor: pointer;
  background: transparent;
  color: var(--text-secondary);
  font: 500 12px/1 var(--font-sans, 'Inter', sans-serif);
  transition: all 0.15s;
  white-space: nowrap;
}

.seg-btn.active {
  background: var(--bg-primary);
  color: var(--text-primary);
  font-weight: 600;
  box-shadow: var(--shadow-xs);
}

/* Footer */
.footer-note {
  text-align: center;
  font-size: var(--text-xs);
  font-weight: var(--fw-medium);
  color: var(--text-tertiary);
  line-height: 1.5;
  padding: var(--space-2) 0 var(--space-4);
}

/* Responsive: segmented control pe telefoane mici */
@media (max-width: 400px) {
  .section-row {
    flex-wrap: wrap;
    gap: var(--space-2);
  }
  .segmented {
    flex-wrap: wrap;
  }
  .seg-btn {
    font-size: 11px;
    padding: 5px 7px;
  }
}
</style>
