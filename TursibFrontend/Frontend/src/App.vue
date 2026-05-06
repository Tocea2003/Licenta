<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useOnboarding } from '@/composables/useOnboarding'
import OnboardingTutorial from '@/components/OnboardingTutorial.vue'
import HomeView from '@/views/HomeView.vue'

const { showOnboarding } = useOnboarding()
const route = useRoute()
const router = useRouter()

// Routes that replace the map entirely (full-page)
const FULL_PAGE_PREFIXES = ['/admin', '/loginadmin', '/login', '/signup']

const isFullPageRoute = computed(() =>
  FULL_PAGE_PREFIXES.some(p => route.path === p || route.path.startsWith(p + '/'))
)

// Secondary app routes show as a panel over the map
const isPanelRoute = computed(() =>
  !isFullPageRoute.value && route.path !== '/'
)

function goHome() {
  router.push('/')
}

onMounted(() => {
  console.log('🚀 App mounted')
})
</script>

<template>
  <OnboardingTutorial
    v-if="!isFullPageRoute"
    :show="showOnboarding"
    @close="showOnboarding = false"
  />

  <!-- Map always visible in background for all non-admin/auth routes -->
  <HomeView v-if="!isFullPageRoute" class="map-background" />

  <!-- Panel overlay for secondary routes (tickets, settings, favorites, etc.) -->
  <Transition name="panel-slide">
    <div
      v-if="isPanelRoute"
      class="panel-overlay"
      role="dialog"
      aria-modal="true"
      @click.self="goHome"
    >
      <div class="panel-sheet">
        <div class="panel-header">
          <div class="panel-handle"></div>
          <button class="panel-close" @click="goHome" aria-label="Închide">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M18 6L6 18M6 6l12 12" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"/>
            </svg>
          </button>
        </div>
        <RouterView />
      </div>
    </div>
  </Transition>

  <!-- Full-page routes: login, signup, admin -->
  <RouterView v-if="isFullPageRoute" />
</template>

<style>
/* ---- Global reset ---- */
*, *::before, *::after { margin: 0; padding: 0; box-sizing: border-box; }

html, body, #app {
  height: 100%;
  font-family: Inter, system-ui, Avenir, Helvetica, Arial, sans-serif;
}

/* ---- Map always in background ---- */
.map-background {
  position: fixed !important;
  inset: 0;
  z-index: 0;
}

/* ---- Panel overlay backdrop ---- */
.panel-overlay {
  position: fixed;
  inset: 0;
  z-index: 51;
  background: rgba(0, 0, 0, 0.45);
  backdrop-filter: blur(3px);
  -webkit-backdrop-filter: blur(3px);
  display: flex;
  align-items: flex-end;
}

/* ---- Panel sheet (slides from bottom on mobile) ---- */
.panel-sheet {
  position: relative;
  width: 100%;
  max-height: calc(100dvh - var(--nav-height, 70px));
  background: var(--bg-secondary);
  border-radius: 20px 20px 0 0;
  overflow-y: auto;
  overscroll-behavior: contain;
  -webkit-overflow-scrolling: touch;
  display: flex;
  flex-direction: column;
}

/* ---- Sticky drag handle + close button ---- */
.panel-header {
  position: sticky;
  top: 0;
  z-index: 5;
  background: var(--bg-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 10px 12px 8px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-primary);
}

.panel-handle {
  width: 36px;
  height: 4px;
  border-radius: 2px;
  background: var(--border-secondary);
}

.panel-close {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-primary);
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
  flex-shrink: 0;
}
.panel-close:hover {
  background: var(--bg-elevated);
  color: var(--text-primary);
  border-color: var(--border-secondary);
}

/* ---- Desktop: panel slides from right ---- */
@media (min-width: 768px) {
  .panel-overlay {
    align-items: stretch;
    justify-content: flex-end;
  }
  .panel-sheet {
    width: 500px;
    max-height: 100dvh;
    border-radius: 20px 0 0 20px;
    box-shadow: -8px 0 40px rgba(0, 0, 0, 0.2);
  }
  .panel-handle {
    display: none;
  }
  .panel-header {
    justify-content: flex-start;
    padding: 16px 20px;
  }
  .panel-close {
    position: static;
    transform: none;
  }
}

/* ---- Panel slide transition ---- */
.panel-slide-enter-active {
  transition: opacity 0.3s ease;
}
.panel-slide-enter-active .panel-sheet {
  transition: transform 0.35s cubic-bezier(0.32, 0.72, 0, 1);
}
.panel-slide-leave-active {
  transition: opacity 0.25s ease;
}
.panel-slide-leave-active .panel-sheet {
  transition: transform 0.25s ease-in;
}
.panel-slide-enter-from,
.panel-slide-leave-to {
  opacity: 0;
}
.panel-slide-enter-from .panel-sheet,
.panel-slide-leave-to .panel-sheet {
  transform: translateY(100%);
}
@media (min-width: 768px) {
  .panel-slide-enter-from .panel-sheet,
  .panel-slide-leave-to .panel-sheet {
    transform: translateX(100%);
  }
}

/* ---- Legacy page transitions (used by full-page routes) ---- */
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.slide-enter-active, .slide-leave-active { transition: all 0.3s ease; }
.slide-enter-from { transform: translateX(20px); opacity: 0; }
.slide-leave-to  { transform: translateX(-20px); opacity: 0; }

.slide-up-enter-active, .slide-up-leave-active { transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1); }
.slide-up-enter-from, .slide-up-leave-to { transform: translateY(100%); opacity: 0; }

.scale-enter-active, .scale-leave-active { transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1); }
.scale-enter-from, .scale-leave-to { transform: scale(0.9); opacity: 0; }

.zoom-enter-active, .zoom-leave-active { transition: all 0.3s ease; }
.zoom-enter-from { transform: scale(1.1); opacity: 0; }
.zoom-leave-to   { transform: scale(0.9); opacity: 0; }
</style>
