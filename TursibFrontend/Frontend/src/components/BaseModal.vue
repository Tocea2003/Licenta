<script setup lang="ts">
import { onMounted, onBeforeUnmount, watch } from 'vue'

const props = defineProps<{
  open: boolean
  title?: string
  size?: 'sm' | 'md' | 'lg'
  closeOnBackdrop?: boolean
}>()

const emit = defineEmits<{ (e: 'close'): void }>()

function handleKey(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.open) emit('close')
}

onMounted(() => window.addEventListener('keydown', handleKey))
onBeforeUnmount(() => window.removeEventListener('keydown', handleKey))

watch(() => props.open, (value) => {
  if (typeof document !== 'undefined') {
    document.body.style.overflow = value ? 'hidden' : ''
  }
})

function onBackdropClick() {
  if (props.closeOnBackdrop !== false) emit('close')
}
</script>

<template>
  <Transition name="modal">
    <div v-if="open" class="modal-backdrop" @click="onBackdropClick">
      <div
        class="modal-panel"
        :class="`modal-${size || 'md'}`"
        role="dialog"
        aria-modal="true"
        :aria-label="title"
        @click.stop
      >
        <header v-if="title || $slots.header" class="modal-header">
          <slot name="header">
            <h2 class="modal-title">{{ title }}</h2>
          </slot>
          <button
            type="button"
            class="modal-close"
            aria-label="Inchide"
            @click="emit('close')"
          >×</button>
        </header>
        <div class="modal-body">
          <slot />
        </div>
        <footer v-if="$slots.footer" class="modal-footer">
          <slot name="footer" />
        </footer>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: var(--z-modal);
  background: var(--bg-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-4);
  backdrop-filter: blur(2px);
}

.modal-panel {
  background: var(--bg-elevated);
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-xl);
  width: 100%;
  max-height: calc(100vh - 2 * var(--space-4));
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.modal-sm { max-width: 420px; }
.modal-md { max-width: 560px; }
.modal-lg { max-width: 720px; }

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--border-primary);
}

.modal-title {
  font-size: var(--text-lg);
  font-weight: var(--fw-semibold);
  color: var(--text-primary);
  margin: 0;
}

.modal-close {
  appearance: none;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  font-size: 24px;
  line-height: 1;
  width: 32px;
  height: 32px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-full);
  cursor: pointer;
  transition: var(--transition-base);
}
.modal-close:hover {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.modal-body {
  padding: var(--space-5);
  overflow-y: auto;
}

.modal-footer {
  padding: var(--space-3) var(--space-5);
  border-top: 1px solid var(--border-primary);
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
  background: var(--bg-secondary);
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.2s ease;
}
.modal-enter-active .modal-panel,
.modal-leave-active .modal-panel {
  transition: transform 0.25s ease, opacity 0.2s ease;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
.modal-enter-from .modal-panel,
.modal-leave-to .modal-panel {
  transform: translateY(12px) scale(0.98);
  opacity: 0;
}
</style>
