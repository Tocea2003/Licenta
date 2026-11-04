<template>
  <div class="settings-page">
    <header>
      <h1 class="page-title">{{ t('settings') }}</h1>
      <p class="page-subtitle">{{ t('personalizeExperience') }}</p>
    </header>

    <!-- Notificări -->
    <section class="settings-section" aria-labelledby="sec-notifications">
      <h2 id="sec-notifications" class="section-label">{{ t('notifications') }}</h2>
      <div class="settings-card">
        <div class="row">
          <span class="row-icon" aria-hidden="true"><Bell :size="18" /></span>
          <span class="row-text">
            <span id="lbl-push" class="row-title">{{ t('pushNotifications') }}</span>
            <span class="row-desc">{{ t('pushNotificationsDesc') }}</span>
          </span>
          <button type="button" role="switch" class="switch" :aria-checked="notificationsEnabled" aria-labelledby="lbl-push" @click="toggleNotifications"></button>
        </div>
        <div class="row">
          <span class="row-icon" aria-hidden="true"><Volume2 :size="18" /></span>
          <span class="row-text">
            <span id="lbl-sound" class="row-title">{{ t('sound') }}</span>
            <span class="row-desc">{{ t('onNewNotifications') }}</span>
          </span>
          <button type="button" role="switch" class="switch" :aria-checked="soundEnabled" aria-labelledby="lbl-sound" @click="soundEnabled = !soundEnabled"></button>
        </div>
        <div class="row">
          <span class="row-icon" aria-hidden="true"><Vibrate :size="18" /></span>
          <span class="row-text"><span id="lbl-haptics" class="row-title">{{ t('vibration') }}</span></span>
          <button type="button" role="switch" class="switch" :aria-checked="hapticsEnabled" aria-labelledby="lbl-haptics" @click="hapticsEnabled = !hapticsEnabled"></button>
        </div>
      </div>
    </section>

    <!-- Aspect -->
    <section class="settings-section" aria-labelledby="sec-appearance">
      <h2 id="sec-appearance" class="section-label">{{ t('appearance') }}</h2>
      <div class="settings-card">
        <div class="row row--stacked">
          <span class="row-title">{{ t('darkMode') }}</span>
          <div class="segmented" role="radiogroup" :aria-label="t('darkMode')">
            <button
              v-for="opt in themeOptions"
              :key="opt.value"
              type="button"
              role="radio"
              :aria-checked="themeMode === opt.value"
              :class="{ active: themeMode === opt.value }"
              @click="setThemeMode(opt.value)"
            >
              <component :is="opt.icon" :size="16" aria-hidden="true" />{{ t(opt.labelKey) }}
            </button>
          </div>
        </div>
        <div class="row row--stacked">
          <span class="row-title">{{ t('language') }}</span>
          <div class="segmented" role="radiogroup" :aria-label="t('language')">
            <button
              v-for="lang in languageOptions"
              :key="lang"
              type="button"
              role="radio"
              :aria-checked="currentLanguage === lang"
              :class="{ active: currentLanguage === lang }"
              @click="setLanguage(lang)"
            >{{ getLanguageDisplay(lang) }}</button>
          </div>
        </div>
      </div>
    </section>

    <!-- Cont și statistici -->
    <section class="settings-section" aria-labelledby="sec-account">
      <h2 id="sec-account" class="section-label">{{ t('account') }}</h2>
      <div class="settings-card">
        <router-link to="/statistics" class="row row--link">
          <span class="row-icon" aria-hidden="true"><BarChart3 :size="18" /></span>
          <span class="row-text">
            <span class="row-title">{{ t('myStatistics') }}</span>
            <span class="row-desc">{{ t('usageStatisticsDesc') }}</span>
          </span>
          <ChevronRight :size="18" class="chevron" aria-hidden="true" />
        </router-link>
        <router-link to="/about" class="row row--link">
          <span class="row-icon" aria-hidden="true"><ShieldCheck :size="18" /></span>
          <span class="row-text"><span class="row-title">{{ t('dataPrivacy') }}</span></span>
          <ChevronRight :size="18" class="chevron" aria-hidden="true" />
        </router-link>
        <button v-if="isLoggedIn" type="button" class="row row--link row--danger" @click="handleLogout">
          <span class="row-icon" aria-hidden="true"><LogOut :size="18" /></span>
          <span class="row-text"><span class="row-title">{{ t('logout') }}</span></span>
        </button>
        <router-link v-else to="/login" class="row row--link">
          <span class="row-icon" aria-hidden="true"><LogIn :size="18" /></span>
          <span class="row-text"><span class="row-title">{{ t('loginButton') }}</span></span>
          <ChevronRight :size="18" class="chevron" aria-hidden="true" />
        </router-link>
      </div>
    </section>

    <!-- Informații -->
    <section class="settings-section" aria-labelledby="sec-info">
      <h2 id="sec-info" class="section-label">{{ t('information') }}</h2>
      <div class="settings-card">
        <button type="button" class="row row--link" @click="showTutorial">
          <span class="row-icon" aria-hidden="true"><GraduationCap :size="18" /></span>
          <span class="row-text">
            <span class="row-title">{{ t('viewTutorial') }}</span>
            <span class="row-desc">{{ t('viewTutorialDesc') }}</span>
          </span>
          <ChevronRight :size="18" class="chevron" aria-hidden="true" />
        </button>
        <router-link to="/about" class="row row--link">
          <span class="row-icon" aria-hidden="true"><Info :size="18" /></span>
          <span class="row-text"><span class="row-title">{{ t('aboutApp') }}</span></span>
          <ChevronRight :size="18" class="chevron" aria-hidden="true" />
        </router-link>
        <div class="row">
          <span class="row-icon" aria-hidden="true"><Tag :size="18" /></span>
          <span class="row-text"><span class="row-title">{{ t('version') }}</span></span>
          <span class="row-value">1.0.0</span>
        </div>
      </div>
    </section>

    <p class="footer-note">{{ t('settingsFooter') }}</p>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useDarkMode } from '@/composables/useDarkMode'
import { useLanguage, type Language } from '@/composables/useLanguage'
import { useOnboarding } from '@/composables/useOnboarding'
import { authService } from '@/services/adminService'
import {
  BarChart3, Bell, ChevronRight, GraduationCap, Info, LogIn, LogOut, Monitor, Moon, ShieldCheck, Sun, Tag, Vibrate, Volume2
} from 'lucide-vue-next'

const router = useRouter()
const { startOnboarding } = useOnboarding()

const showTutorial = () => {
  startOnboarding()
  router.push('/')
}

const { isDarkMode, toggleDarkMode } = useDarkMode()
const { currentLanguage, setLanguage, t } = useLanguage()

const notificationsEnabled = ref(false)
const soundEnabled = ref(true)
const hapticsEnabled = ref(false)

const languageOptions: Language[] = ['ro', 'en', 'de']

const themeOptions = [
  { value: 'light' as const, labelKey: 'themeLight', icon: Sun },
  { value: 'dark' as const, labelKey: 'themeDark', icon: Moon },
  { value: 'auto' as const, labelKey: 'themeAuto', icon: Monitor },
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
  if (language === 'ro') return 'Română'
  if (language === 'en') return 'English'
  return 'Deutsch'
}

const toggleNotifications = () => {
  notificationsEnabled.value = !notificationsEnabled.value
  if (notificationsEnabled.value && 'Notification' in window) {
    Notification.requestPermission().then(permission => {
      notificationsEnabled.value = permission === 'granted'
    })
  }
}

const isLoggedIn = ref(authService.isAuthenticated())

const handleLogout = () => {
  authService.logout()
  isLoggedIn.value = false
  router.push('/')
}
</script>

<style scoped>
.settings-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 20px 16px 32px;
}

.page-subtitle {
  margin-top: 4px;
}

.settings-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.section-label {
  margin: 0;
  font-family: var(--font-sans);
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.settings-card {
  display: flex;
  flex-direction: column;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  overflow: hidden;
}

.row {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 56px;
  padding: 10px 14px;
  border: none;
  border-bottom: 1px solid var(--border-subtle);
  background: none;
  color: var(--text-primary);
  font: inherit;
  text-align: left;
  text-decoration: none;
}

.row:last-child {
  border-bottom: none;
}

.row--stacked {
  flex-direction: column;
  align-items: stretch;
  gap: 10px;
  padding: 14px;
}

.row--link {
  width: 100%;
  cursor: pointer;
}

.row--link:hover {
  background: var(--bg-secondary);
  color: var(--text-primary);
  text-decoration: none;
}

.row--link:focus-visible {
  outline: none;
  box-shadow: inset var(--ring-focus);
}

.row--danger {
  color: var(--color-danger);
}

.row--danger:hover {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.row-icon {
  width: 36px;
  height: 36px;
  flex-shrink: 0;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.row--danger .row-icon {
  background: var(--color-danger-soft);
}

.row-text {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.row-title {
  font-size: 15px;
  font-weight: var(--fw-semibold);
}

.row-desc {
  font-size: 13px;
  color: var(--text-secondary);
}

.row-value {
  font-size: 14px;
  color: var(--text-secondary);
}

.chevron {
  color: var(--text-tertiary);
}

/* Comutator */
.switch {
  position: relative;
  width: 48px;
  height: 28px;
  flex-shrink: 0;
  border: none;
  border-radius: 14px;
  background: var(--border-secondary);
  cursor: pointer;
  transition: background 0.2s ease;
}

.switch::after {
  content: '';
  position: absolute;
  top: 3px;
  left: 3px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 2px rgba(22, 24, 29, 0.25);
  transition: transform 0.2s ease;
}

.switch[aria-checked='true'] {
  background: var(--accent-primary);
}

.switch[aria-checked='true']::after {
  transform: translateX(20px);
}

.switch:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

/* Control segmentat */
.segmented {
  display: flex;
  gap: 4px;
  padding: 4px;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
}

.segmented button {
  flex: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 40px;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--text-secondary);
  font: inherit;
  font-size: 14px;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.segmented button.active {
  background: var(--bg-primary);
  color: var(--text-primary);
  box-shadow: 0 1px 2px rgba(22, 24, 29, 0.1);
}

.footer-note {
  margin: 0;
  font-size: 13px;
  text-align: center;
  color: var(--text-secondary);
}
</style>
