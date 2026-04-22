<script setup lang="ts">
defineProps<{
  label?: string
  hint?: string
  error?: string
  required?: boolean
}>()
</script>

<template>
  <div class="form-field">
    <label v-if="label">
      {{ label }}
      <span v-if="required" aria-hidden="true" style="color: var(--color-danger)">*</span>
    </label>
    <slot :hasError="!!error" />
    <div v-if="hint && !error" class="form-hint">{{ hint }}</div>
    <div v-if="error" class="form-error" role="alert">{{ error }}</div>
  </div>
</template>
