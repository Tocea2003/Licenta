<template>
  <div class="station-card-skeleton">
    <div class="skeleton-header">
      <SkeletonLoader variant="circular" :width="48" :height="48" />
      <div class="skeleton-info">
        <SkeletonLoader variant="text" width="60%" :height="20" />
        <SkeletonLoader variant="text" width="40%" :height="16" />
      </div>
    </div>
    
    <div class="skeleton-routes">
      <SkeletonLoader variant="rounded" width="60px" :height="28" />
      <SkeletonLoader variant="rounded" width="60px" :height="28" />
      <SkeletonLoader variant="rounded" width="60px" :height="28" />
    </div>
    
    <div class="skeleton-footer">
      <SkeletonLoader variant="text" width="50%" :height="14" />
    </div>
  </div>
</template>

<script setup lang="ts">
import SkeletonLoader from './SkeletonLoader.vue'
</script>

<style scoped>
.station-card-skeleton {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 16px;
  padding: 16px;
  margin-bottom: 12px;
}

.skeleton-header {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.skeleton-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.skeleton-routes {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.skeleton-footer {
  padding-top: 12px;
  border-top: 1px solid var(--border-primary);
}
</style>
