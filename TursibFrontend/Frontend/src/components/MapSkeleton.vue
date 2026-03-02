<template>
  <div class="map-skeleton">
    <div class="skeleton-map-container">
      <SkeletonLoader variant="rectangular" width="100%" height="100%" :animated="true" />
      
      <!-- Floating elements to simulate map controls -->
      <div class="skeleton-controls">
        <SkeletonLoader variant="circular" :width="40" :height="40" />
        <SkeletonLoader variant="circular" :width="40" :height="40" />
      </div>
      
      <!-- Simulate search bar -->
      <div class="skeleton-search">
        <SkeletonLoader variant="rounded" width="100%" :height="48" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import SkeletonLoader from './SkeletonLoader.vue'
</script>

<style scoped>
.map-skeleton {
  position: relative;
  width: 100%;
  height: 100%;
  background: var(--bg-secondary);
  border-radius: 12px;
  overflow: hidden;
}

.skeleton-map-container {
  position: relative;
  width: 100%;
  height: 100%;
}

.skeleton-controls {
  position: absolute;
  bottom: 120px;
  right: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  z-index: 10;
}

.skeleton-search {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  z-index: 10;
}

/* Add subtle pattern to make it look more like a map */
.skeleton-map-container::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-image: 
    linear-gradient(90deg, transparent 49%, rgba(0, 0, 0, 0.03) 49%, rgba(0, 0, 0, 0.03) 51%, transparent 51%),
    linear-gradient(0deg, transparent 49%, rgba(0, 0, 0, 0.03) 49%, rgba(0, 0, 0, 0.03) 51%, transparent 51%);
  background-size: 60px 60px;
  z-index: 1;
}
</style>
