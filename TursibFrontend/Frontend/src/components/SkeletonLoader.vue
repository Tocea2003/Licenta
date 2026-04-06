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
    var(--bg-tertiary) 0%,
    var(--border-primary) 50%,
    var(--bg-tertiary) 100%
  );
  background-size: 200% 100%;
  border-radius: var(--radius-sm, 4px);
}

.skeleton.animated {
  animation: shimmer 1.5s ease-in-out infinite;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.skeleton.text {
  height: 1em;
  margin-bottom: 0.5em;
  border-radius: var(--radius-sm, 4px);
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
  border-radius: var(--radius-md, 10px);
}
</style>
