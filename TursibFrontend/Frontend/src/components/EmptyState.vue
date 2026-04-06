<template>
  <div class="empty-state" :class="{ compact }">
    <div class="empty-icon">{{ icon }}</div>
    <h3>{{ title }}</h3>
    <p>{{ description }}</p>
    <slot name="action">
      <button v-if="actionText" @click="handleAction" class="btn-action">
        {{ actionText }}
      </button>
    </slot>
  </div>
</template>

<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    icon?: string
    title: string
    description?: string
    actionText?: string
    compact?: boolean
  }>(),
  {
    icon: '📭',
    compact: false,
  }
)

const emit = defineEmits<{
  action: []
}>()

const handleAction = () => {
  emit('action')
}
</script>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 64px 32px;
  min-height: 400px;
}

.empty-state.compact {
  padding: 32px 24px;
  min-height: 200px;
}

.empty-icon {
  font-size: 5rem;
  margin-bottom: 24px;
  animation: float 3s ease-in-out infinite;
  opacity: 0.8;
}

@keyframes float {
  0%, 100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

.empty-state h3 {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.empty-state p {
  font-size: 1rem;
  color: var(--text-secondary);
  margin: 0 0 24px 0;
  max-width: 400px;
  line-height: 1.6;
}

.btn-action {
  padding: 12px 32px;
  background: var(--gradient-primary);
  color: white;
  border: none;
  border-radius: var(--radius-md, 12px);
  font-weight: 600;
  font-size: 1rem;
  cursor: pointer;
  transition: transform 0.15s ease, box-shadow 0.2s ease;
  min-height: 44px;
}

.btn-action:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(59, 130, 246, 0.35);
}

.btn-action:active {
  transform: translateY(0);
}

/* Compact mode adjustments */
.empty-state.compact .empty-icon {
  font-size: 3rem;
  margin-bottom: 16px;
}

.empty-state.compact h3 {
  font-size: 1.25rem;
}

.empty-state.compact p {
  font-size: 0.95rem;
  margin-bottom: 16px;
}
</style>
