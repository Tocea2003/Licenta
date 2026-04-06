<template>
  <nav class="bottom-nav">
    <router-link
      to="/"
      class="nav-item"
      :class="{ active: isActive('/') }"
    >
      <div class="nav-icon-wrap">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M3 12L12 3L21 12M5 10V20C5 20.5523 5.44772 21 6 21H9V15C9 14.4477 9.44772 14 10 14H14C14.5523 14 15 14.4477 15 15V21H18C18.5523 21 19 20.5523 19 20V10"
            stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <span>Hartă</span>
    </router-link>

    <button
      class="nav-item"
      :class="{ active: isPlanActive }"
      @click="openPlan"
    >
      <div class="nav-icon-wrap">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M9 11L12 14L22 4M21 12V19C21 19.5304 20.7893 20.0391 20.4142 20.4142C20.0391 20.7893 19.5304 21 19 21H5C4.46957 21 3.96086 20.7893 3.58579 20.4142C3.21071 20.0391 3 19.5304 3 19V5C3 4.46957 3.21071 3.96086 3.58579 3.58579C3.96086 3.21071 4.46957 3 5 3H16"
            stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <span>Planificare</span>
    </button>

    <router-link
      to="/favorites"
      class="nav-item"
      :class="{ active: isActive('/favorites') }"
    >
      <div class="nav-icon-wrap">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M20.84 4.61C20.3292 4.099 19.7228 3.69364 19.0554 3.41708C18.3879 3.14052 17.6725 2.99817 16.95 2.99817C16.2275 2.99817 15.5121 3.14052 14.8446 3.41708C14.1772 3.69364 13.5708 4.099 13.06 4.61L12 5.67L10.94 4.61C9.9083 3.57831 8.50903 2.99871 7.05 2.99871C5.59096 2.99871 4.19169 3.57831 3.16 4.61C2.1283 5.64169 1.54871 7.04097 1.54871 8.5C1.54871 9.95903 2.1283 11.3583 3.16 12.39L4.22 13.45L12 21.23L19.78 13.45L20.84 12.39C21.351 11.8792 21.7563 11.2728 22.0329 10.6053C22.3095 9.93789 22.4518 9.22248 22.4518 8.5C22.4518 7.77752 22.3095 7.06211 22.0329 6.39469C21.7563 5.72728 21.351 5.12084 20.84 4.61V4.61Z"
            stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <span>Favorite</span>
    </router-link>

    <router-link
      to="/settings"
      class="nav-item"
      :class="{ active: isActive('/settings') }"
    >
      <div class="nav-icon-wrap">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
          <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"
            stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </div>
      <span>Setări</span>
    </router-link>
  </nav>
</template>

<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { computed } from 'vue'

const route = useRoute()
const router = useRouter()

const isActive = (path: string) => {
  return computed(() => route.path === path)
}

const isPlanActive = computed(() => route.path === '/' && route.query.tab === 'plan')

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
  background: rgba(var(--bg-primary-rgb, 255,255,255), 0.85);
  background: var(--bg-primary);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border-top: 1px solid var(--border-primary);
  display: flex;
  justify-content: space-around;
  align-items: center;
  padding: 8px 8px max(8px, env(safe-area-inset-bottom));
  z-index: var(--z-overlay, 100);
  box-shadow: 0 -1px 0 var(--border-primary), 0 -4px 16px rgba(15, 23, 42, 0.06);
}

.nav-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  padding: 8px 20px;
  text-decoration: none;
  color: var(--text-tertiary);
  transition: color 0.2s ease, background-color 0.2s ease, transform 0.15s ease;
  border-radius: 14px;
  min-width: 64px;
  position: relative;
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  -webkit-tap-highlight-color: transparent;
}

.nav-item svg {
  width: 22px;
  height: 22px;
  transition: transform 0.2s cubic-bezier(0.34, 1.56, 0.64, 1), color 0.2s ease;
  flex-shrink: 0;
}

.nav-item span {
  font-size: 10px;
  font-weight: 500;
  letter-spacing: 0.01em;
  transition: color 0.2s ease, font-weight 0.1s ease;
  line-height: 1;
}

.nav-item:hover {
  color: var(--accent-primary);
}

.nav-item:active {
  transform: scale(0.93);
}

/* Active state — pill background */
.nav-item.active {
  color: var(--accent-primary);
}

.nav-item.active .nav-icon-wrap {
  background: var(--accent-primary-soft);
}

.nav-item.active svg {
  transform: scale(1.1);
}

.nav-item.active span {
  font-weight: 600;
  color: var(--accent-primary);
}

/* Icon wrapper for pill effect */
.nav-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 28px;
  border-radius: 14px;
  transition: background-color 0.2s ease;
}

@media (min-width: 768px) {
  .bottom-nav {
    display: none;
  }
}

@media (max-width: 380px) {
  .nav-item {
    padding: 8px 10px;
    min-width: 0;
    flex: 1;
  }
  .nav-icon-wrap {
    width: 36px;
  }
}
</style>
