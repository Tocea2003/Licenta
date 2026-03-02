# 🎨 UI Components - Quick Reference Card

## 🚀 Quick Import Guide

```typescript
// Composables
import { useDarkMode } from '@/composables/useDarkMode'
import { useOnboarding } from '@/composables/useOnboarding'

// Skeletons
import SkeletonLoader from '@/components/SkeletonLoader.vue'
import StationCardSkeleton from '@/components/StationCardSkeleton.vue'
import MapSkeleton from '@/components/MapSkeleton.vue'
import ListSkeleton from '@/components/ListSkeleton.vue'

// Error States
import EmptyState from '@/components/EmptyState.vue'
import OfflineState from '@/components/OfflineState.vue'

// Tutorial
import OnboardingTutorial from '@/components/OnboardingTutorial.vue'
```

---

## 💀 Skeleton Loaders - Copy & Paste Examples

### Text Line
```vue
<SkeletonLoader variant="text" width="60%" :height="20" />
```

### Circle (Avatar/Icon)
```vue
<SkeletonLoader variant="circular" :width="48" :height="48" />
```

### Card
```vue
<SkeletonLoader variant="rounded" width="100%" :height="120" />
```

### Station Card
```vue
<StationCardSkeleton />
```

### List of Cards
```vue
<ListSkeleton :count="5" />
```

### Map
```vue
<MapSkeleton />
```

### Custom Skeleton Layout
```vue
<div class="skeleton-card">
  <SkeletonLoader variant="circular" :width="60" :height="60" />
  <div class="skeleton-content">
    <SkeletonLoader variant="text" width="80%" :height="24" />
    <SkeletonLoader variant="text" width="60%" :height="16" />
  </div>
</div>
```

---

## 🎭 EmptyState - Copy & Paste Examples

### Basic Empty State
```vue
<EmptyState
  icon="📭"
  title="Nicio căutare recentă"
  description="Căutările tale vor apărea aici"
/>
```

### With Action Button
```vue
<EmptyState
  icon="⭐"
  title="Nicio locație salvată"
  description="Adaugă locațiile tale favorite"
  actionText="Adaugă Prima Locație"
  @action="handleAddLocation"
/>
```

### Compact Mode
```vue
<EmptyState
  icon="🚌"
  title="Nu sunt rezultate"
  description="Încearcă o altă căutare"
  compact
/>
```

### Custom Action Slot
```vue
<EmptyState icon="📍" title="Nicio rută găsită">
  <template #action>
    <button @click="retry" class="custom-btn">Reîncearcă</button>
  </template>
</EmptyState>
```

---

## 🌐 OfflineState - Copy & Paste Examples

### Basic Offline
```vue
<OfflineState @retry="loadData" />
```

### With Offline Mode
```vue
<OfflineState
  title="Conexiune pierdută"
  description="Nu te putem conecta la server"
  :showOfflineMode="true"
  @retry="handleRetry"
  @offlineMode="enableOfflineMode"
/>
```

### Custom Messages
```vue
<OfflineState
  title="Server-ul nu răspunde"
  description="Serverul este temporar indisponibil"
  @retry="loadData"
/>
```

---

## 🌙 Dark Mode - Copy & Paste Examples

### Basic Toggle
```vue
<script setup>
import { useDarkMode } from '@/composables/useDarkMode'

const { isDarkMode, toggleDarkMode } = useDarkMode()
</script>

<template>
  <button @click="toggleDarkMode">
    {{ isDarkMode ? '☀️ Light' : '🌙 Dark' }}
  </button>
</template>
```

### Set Specific Mode
```vue
<script setup>
import { useDarkMode } from '@/composables/useDarkMode'

const { setDarkMode } = useDarkMode()

const enableDark = () => setDarkMode(true)
const enableLight = () => setDarkMode(false)
</script>
```

### Check Current Mode
```vue
<script setup>
import { useDarkMode } from '@/composables/useDarkMode'

const { isDarkMode } = useDarkMode()
</script>

<template>
  <p v-if="isDarkMode">Dark mode active</p>
  <p v-else>Light mode active</p>
</template>
```

---

## 👋 Onboarding - Copy & Paste Examples

### Show Onboarding
```vue
<script setup>
import { useOnboarding } from '@/composables/useOnboarding'
import OnboardingTutorial from '@/components/OnboardingTutorial.vue'

const { showOnboarding } = useOnboarding()
</script>

<template>
  <OnboardingTutorial :show="showOnboarding" @close="showOnboarding = false" />
</template>
```

### Manual Trigger
```vue
<script setup>
import { useOnboarding } from '@/composables/useOnboarding'

const { startOnboarding } = useOnboarding()
</script>

<template>
  <button @click="startOnboarding">
    Arată Tutorial
  </button>
</template>
```

### Reset for Testing
```vue
<script setup>
import { useOnboarding } from '@/composables/useOnboarding'

const { resetOnboarding } = useOnboarding()
</script>

<template>
  <button @click="resetOnboarding">
    Reset Tutorial (Dev Only)
  </button>
</template>
```

---

## ✨ Page Transitions - Copy & Paste Examples

### Router Configuration
```typescript
// router/index.ts
{
  path: '/settings',
  component: SettingsView,
  meta: { transition: 'slide' }  // Custom transition
}

// Available transitions:
// - 'fade' (default)
// - 'slide'
// - 'slide-up'
// - 'scale'
// - 'zoom'
```

### App.vue Setup (Already Done)
```vue
<template>
  <router-view v-slot="{ Component, route }">
    <Transition :name="route.meta.transition || 'fade'" mode="out-in">
      <component :is="Component" :key="route.path" />
    </Transition>
  </router-view>
</template>
```

---

## 📋 Common Patterns

### Loading State with Skeleton
```vue
<template>
  <div v-if="loading">
    <ListSkeleton :count="3" />
  </div>
  
  <div v-else-if="error">
    <OfflineState @retry="loadData" />
  </div>
  
  <div v-else-if="items.length === 0">
    <EmptyState
      icon="📭"
      title="Nicio înregistrare"
      description="Datele vor apărea aici"
    />
  </div>
  
  <div v-else>
    <!-- Real content -->
    <div v-for="item in items" :key="item.id">
      {{ item.name }}
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import ListSkeleton from '@/components/ListSkeleton.vue'
import OfflineState from '@/components/OfflineState.vue'
import EmptyState from '@/components/EmptyState.vue'

const loading = ref(true)
const error = ref(false)
const items = ref([])

onMounted(async () => {
  try {
    // Load data
    items.value = await fetchData()
  } catch (e) {
    error.value = true
  } finally {
    loading.value = false
  }
})
</script>
```

### Search with States
```vue
<template>
  <!-- Loading -->
  <div v-if="searching">
    <SkeletonLoader variant="text" width="100%" />
    <ListSkeleton :count="5" />
  </div>
  
  <!-- Empty search -->
  <EmptyState
    v-else-if="query && results.length === 0"
    icon="🔍"
    title="Niciun rezultat"
    description="Încearcă o altă căutare"
  />
  
  <!-- Empty initial state -->
  <EmptyState
    v-else-if="!query"
    icon="🔎"
    title="Începe o căutare"
    description="Caută stații, rute sau adrese"
    compact
  />
  
  <!-- Results -->
  <div v-else>
    <div v-for="result in results" :key="result.id">
      {{ result.name }}
    </div>
  </div>
</template>
```

### Form with Dark Mode
```vue
<script setup>
import { useDarkMode } from '@/composables/useDarkMode'

const { isDarkMode } = useDarkMode()
</script>

<template>
  <form :class="{ 'dark-form': isDarkMode }">
    <input type="text" placeholder="Nume" />
    <button type="submit">Salvează</button>
  </form>
</template>

<style scoped>
form {
  background: var(--bg-primary);
  color: var(--text-primary);
}

.dark-form input {
  background: var(--bg-secondary);
  border-color: var(--border-primary);
}
</style>
```

---

## 🎨 CSS Variables Reference

Use these in your components for automatic dark mode support:

```css
/* Light Mode Variables */
--bg-primary: #ffffff
--bg-secondary: #f8fafc
--bg-tertiary: #f1f5f9
--text-primary: #1e293b
--text-secondary: #64748b
--border-primary: #e2e8f0
--border-secondary: #cbd5e1

/* Dark Mode (applied when html has .dark class) */
:root.dark {
  --bg-primary: #0f172a
  --bg-secondary: #1e293b
  --bg-tertiary: #334155
  --text-primary: #f1f5f9
  --text-secondary: #94a3b8
  --border-primary: #334155
  --border-secondary: #475569
}

/* Usage */
.my-component {
  background: var(--bg-primary);
  color: var(--text-primary);
  border: 1px solid var(--border-primary);
}
```

---

## ⚡ Performance Tips

### Lazy Load Components
```typescript
// Don't import at top if not always needed
const EmptyState = defineAsyncComponent(() => 
  import('@/components/EmptyState.vue')
)
```

### Reduce Skeleton Count
```vue
<!-- Don't overdo it -->
<ListSkeleton :count="3" />  <!-- Good -->
<ListSkeleton :count="20" /> <!-- Too many -->
```

### Disable Animation on Low-End Devices
```vue
<SkeletonLoader :animated="!isLowEndDevice" />

<script>
const isLowEndDevice = navigator.hardwareConcurrency <= 2
</script>
```

---

## 🐛 Common Issues & Solutions

### Issue: Skeleton doesn't animate
**Solution:** Check CSS variables are defined
```css
/* Make sure these exist */
--skeleton-base: #e5e7eb
--skeleton-shimmer: #f3f4f6
```

### Issue: Dark mode doesn't persist
**Solution:** Ensure useDarkMode() is called in App.vue or root component

### Issue: Onboarding shows every time
**Solution:** Check localStorage key
```javascript
localStorage.getItem('tursib_onboarding_completed')
// Should return: "1.0"
```

### Issue: Page transitions feel janky
**Solution:** Reduce transition duration
```css
.fade-enter-active {
  transition: opacity 0.15s ease; /* Faster */
}
```

---

## 📱 Responsive Design

All components are mobile-friendly by default:

```vue
<EmptyState />        <!-- Auto-responsive -->
<SkeletonLoader />    <!-- Auto-responsive -->
<OfflineState />      <!-- Auto-responsive -->
<OnboardingTutorial /> <!-- Auto-responsive -->
```

Custom breakpoints for your components:
```css
@media (max-width: 640px) {
  /* Mobile styles */
}

@media (min-width: 641px) and (max-width: 1024px) {
  /* Tablet styles */
}

@media (min-width: 1025px) {
  /* Desktop styles */
}
```

---

## ✅ Checklist for New Pages

When creating a new page/view:

- [ ] Add loading state with skeleton loaders
- [ ] Add empty state for no data
- [ ] Add error state for failures
- [ ] Test in dark mode
- [ ] Add page transition in router
- [ ] Test on mobile devices
- [ ] Check accessibility (keyboard navigation)

---

## 🎯 Pro Tips

1. **Combine components** for better UX:
   ```vue
   <OfflineState v-if="networkError" />
   <EmptyState v-else-if="noData" />
   <ListSkeleton v-else-if="loading" />
   <RealContent v-else />
   ```

2. **Use semantic icons** in EmptyState:
   - 📭 No data
   - 🔍 No search results
   - ⭐ No favorites
   - 🚌 No buses
   - 📍 No locations

3. **Match skeleton to content**:
   ```vue
   <!-- If real content has circular avatar -->
   <SkeletonLoader variant="circular" :width="48" :height="48" />
   
   <!-- If real content has text -->
   <SkeletonLoader variant="text" width="80%" />
   ```

4. **Transition timing**:
   - Fast actions: 150-200ms
   - Normal navigation: 200-300ms
   - Heavy pages: 300-400ms (max)

---

**Need more help?** Check [UI_UX_IMPROVEMENTS.md](./UI_UX_IMPROVEMENTS.md) for full documentation.

