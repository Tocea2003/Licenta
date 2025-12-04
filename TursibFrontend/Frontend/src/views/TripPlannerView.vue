<template>
  <div class="trip-planner-page">
    <TripSearch @route-selected="handleRouteSelected" />
  </div>
</template>

<script setup lang="ts">
import TripSearch from '@/components/TripSearch.vue'
import { useRouter } from 'vue-router'

const router = useRouter()

const handleRouteSelected = (result: any) => {
  console.log('Route selected:', result)
  // Traseul se afișează direct în TripSearch component cu harta
}
</script>

<style scoped>
.trip-planner-page {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 40px 20px;
}
</style>
