<template>
  <main class="not-found-page">
    <div class="not-found-card">
      <span class="error-code">404</span>
      <h1 class="title">{{ t('lostBusTitle') }}</h1>
      <p class="hint">{{ t('pageNotFoundHint') }}</p>

      <div class="actions">
        <button type="button" class="btn btn-primary btn-lg" @click="goHome">
          <MapIcon :size="18" aria-hidden="true" />{{ t('backHome') }}
        </button>
        <button type="button" class="btn btn-secondary btn-lg" @click="goBack">
          <ArrowLeft :size="18" aria-hidden="true" />{{ t('back') }}
        </button>
      </div>

      <nav class="suggestions" :aria-label="t('triedAccessing')">
        <h2>{{ t('triedAccessing') }}</h2>
        <router-link to="/"><Route :size="18" aria-hidden="true" />{{ t('planning') }}</router-link>
        <router-link to="/favorites"><Star :size="18" aria-hidden="true" />{{ t('favorites') }}</router-link>
        <router-link to="/settings"><Settings :size="18" aria-hidden="true" />{{ t('settings') }}</router-link>
      </nav>
    </div>
  </main>
</template>

<script setup lang="ts">
import { ArrowLeft, Map as MapIcon, Route, Settings, Star } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import { useLanguage } from '@/composables/useLanguage'

const router = useRouter()
const { t } = useLanguage()

const goHome = () => {
  router.push('/')
}

const goBack = () => {
  if (window.history.length > 2) {
    router.back()
  } else {
    router.push('/')
  }
}
</script>

<style scoped>
.not-found-page {
  min-height: 60dvh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px 16px;
}

.not-found-card {
  width: 100%;
  max-width: 420px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.error-code {
  font-family: var(--font-display);
  font-size: 72px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.04em;
  line-height: 1;
  color: var(--accent-primary);
}

.title {
  margin: 0;
  font-size: 26px;
  font-weight: var(--fw-heavy);
}

.hint {
  margin: 0;
  color: var(--text-secondary);
  line-height: 1.5;
}

.actions {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 8px;
}

.suggestions {
  display: flex;
  flex-direction: column;
  margin-top: 12px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  overflow: hidden;
}

.suggestions h2 {
  margin: 0;
  padding: 12px 14px 6px;
  font-family: var(--font-sans);
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.suggestions a {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 48px;
  padding: 0 14px;
  border-top: 1px solid var(--border-subtle);
  color: var(--text-primary);
  font-weight: var(--fw-semibold);
}

.suggestions a:hover {
  background: var(--bg-secondary);
  text-decoration: none;
}
</style>
