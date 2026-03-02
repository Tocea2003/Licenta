<template>
  <div class="skeleton" :class="[variant, { animated }]" :style="customStyle"></div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    variant?: 'text' | 'circular' | 'rectangular' | 'rounded'
    width?: string | number
    height?: string | number
    animated?: boolean
  }>(),
  {
    variant: 'text',
    animated: true,
  }
)

const customStyle = computed(() => {
  const style: Record<string, string> = {}
  
  if (props.width) {
    style.width = typeof props.width === 'number' ? `${props.width}px` : props.width
  }
  
  if (props.height) {
    style.height = typeof props.height === 'number' ? `${props.height}px` : props.height
  }
  
  return style
})
</script>

<style scoped>
.skeleton {
  background: linear-gradient(
    90deg,
    var(--skeleton-base, #e5e7eb) 0%,
    var(--skeleton-shimmer, #f3f4f6) 50%,
    var(--skeleton-base, #e5e7eb) 100%
  );
  background-size: 200% 100%;
  border-radius: 4px;
}

:root.dark .skeleton {
  --skeleton-base: #334155;
  --skeleton-shimmer: #475569;
}

.skeleton.animated {
  animation: shimmer 1.5s infinite;
}

@keyframes shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

/* Variants */
.skeleton.text {
  height: 1em;
  margin-bottom: 0.5em;
  border-radius: 4px;
}

.skeleton.circular {
  border-radius: 50%;
  width: 40px;
  height: 40px;
}

.skeleton.rectangular {
  border-radius: 0;
}

.skeleton.rounded {
  border-radius: 12px;
}
</style>
