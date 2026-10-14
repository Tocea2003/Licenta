<template>
  <nav class="bottom-nav" :aria-label="t('map')">
    <router-link to="/" class="nav-item" :class="{ active: isActive('/') && !isPlanActive }">
      <MapIcon :size="22" aria-hidden="true" />
      <span>{{ t('map') }}</span>
    </router-link>

    <button type="button" class="nav-item" :class="{ active: isPlanActive }" :aria-current="isPlanActive ? 'page' : undefined" @click="openPlan">
      <RouteIcon :size="22" aria-hidden="true" />
      <span>{{ t('planning') }}</span>
    </button>

    <router-link to="/favorites" class="nav-item" :class="{ active: isActive('/favorites') }">
      <Heart :size="22" aria-hidden="true" />
      <span>{{ t('favorites') }}</span>
    </router-link>

    <router-link to="/tickets" class="nav-item" :class="{ active: isTicketsActive }">
      <Ticket :size="22" aria-hidden="true" />
      <span>{{ t('tickets') }}</span>
    </router-link>

    <router-link to="/settings" class="nav-item" :class="{ active: isActive('/settings') }">
      <Settings :size="22" aria-hidden="true" />
      <span>{{ t('settings') }}</span>
    </router-link>
  </nav>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'
import { useLanguage } from '@/composables/useLanguage'
import { Heart, Map as MapIcon, Route as RouteIcon, Settings, Ticket } from 'lucide-vue-next'

const route = useRoute()
const router = useRouter()
const { t } = useLanguage()

const isActive = (path: string) => {
  return route.path === path
}

const isPlanActive = computed(() => route.path === '/' && route.query.tab === 'plan')
const isTicketsActive = computed(() => route.path === '/tickets' || route.path === '/tickets/checkout')

const openPlan = () => {
  router.push({ path: '/', query: { tab: 'plan' } })
}
</script>

<style scoped>
.bottom-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 1100;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  min-height: var(--nav-height);
  padding: 6px 4px max(8px, env(safe-area-inset-bottom));
  background: var(--bg-primary);
  border-top: 1px solid var(--border-subtle);
  font-family: var(--font-sans);
}

.nav-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  min-height: 52px;
  padding: 0 2px;
  border: none;
  border-radius: var(--radius-md);
  background: none;
  color: var(--text-secondary);
  font: inherit;
  text-decoration: none;
  cursor: pointer;
  -webkit-tap-highlight-color: transparent;
}

.nav-item span {
  font-size: 11px;
  font-weight: var(--fw-semibold);
  line-height: 1;
  white-space: nowrap;
}

.nav-item:hover {
  color: var(--text-primary);
  text-decoration: none;
}

.nav-item.active {
  color: var(--accent-primary);
}

.nav-item.active span {
  font-weight: var(--fw-bold);
}

.nav-item:focus-visible {
  outline: none;
  box-shadow: var(--ring-focus);
}

@media (min-width: 768px) {
  .bottom-nav {
    display: none;
  }
}
</style>
