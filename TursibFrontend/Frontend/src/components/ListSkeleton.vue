<template>
  <div class="list-skeleton">
    <StationCardSkeleton v-for="i in count" :key="i" />
  </div>
</template>

<script setup lang="ts">
import StationCardSkeleton from './StationCardSkeleton.vue'

withDefaults(
  defineProps<{
    count?: number
  }>(),
  {
    count: 3,
  }
)
</script>

<style scoped>
.list-skeleton {
  padding: 16px;
}
</style>
