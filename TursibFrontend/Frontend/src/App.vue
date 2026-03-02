<script setup lang="ts">
import { onMounted } from 'vue'
import { useOnboarding } from '@/composables/useOnboarding'
import OnboardingTutorial from '@/components/OnboardingTutorial.vue'

const { showOnboarding } = useOnboarding()

onMounted(() => {
  console.log('🚀 App mounted')
})
</script>

<template>
  <!-- Onboarding Tutorial -->
  <OnboardingTutorial :show="showOnboarding" @close="showOnboarding = false" />
  
  <!-- Page Transitions -->
  <router-view v-slot="{ Component, route }">
    <Transition :name="route.meta.transition as string || 'fade'" mode="out-in">
      <component :is="Component" :key="route.path" />
    </Transition>
  </router-view>
</template>

<style>
/* Reset global */
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body, #app {
  height: 100%;
  font-family: Inter, system-ui, Avenir, Helvetica, Arial, sans-serif;
}

/* Page Transition Animations */

/* Fade Transition (default) */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* Slide Transition */
.slide-enter-active,
.slide-leave-active {
  transition: all 0.3s ease;
}

.slide-enter-from {
  transform: translateX(20px);
  opacity: 0;
}

.slide-leave-to {
  transform: translateX(-20px);
  opacity: 0;
}

/* Slide Up Transition (for modals/sheets) */
.slide-up-enter-active,
.slide-up-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-up-enter-from {
  transform: translateY(100%);
  opacity: 0;
}

.slide-up-leave-to {
  transform: translateY(100%);
  opacity: 0;
}

/* Scale Transition (for pop-ups) */
.scale-enter-active,
.scale-leave-active {
  transition: all 0.2s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.scale-enter-from,
.scale-leave-to {
  transform: scale(0.9);
  opacity: 0;
}

/* Zoom Transition */
.zoom-enter-active,
.zoom-leave-active {
  transition: all 0.3s ease;
}

.zoom-enter-from {
  transform: scale(1.1);
  opacity: 0;
}

.zoom-leave-to {
  transform: scale(0.9);
  opacity: 0;
}
</style>