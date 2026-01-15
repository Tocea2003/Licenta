import { ref, watch, onMounted } from 'vue'

const STORAGE_KEY = 'tursib_dark_mode'

// State global pentru dark mode
const isDarkMode = ref(false)
let isInitialized = false

// Încarcă preferința din localStorage
const loadDarkModePreference = () => {
  if (isInitialized) return
  
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored !== null) {
      isDarkMode.value = stored === 'true'
    } else {
      // Verifică preferința sistemului
      isDarkMode.value = window.matchMedia('(prefers-color-scheme: dark)').matches
    }
    
    applyDarkMode(isDarkMode.value)
    console.log('✅ Dark mode loaded:', isDarkMode.value)
  } catch (error) {
    console.error('❌ Error loading dark mode preference:', error)
  }
  
  isInitialized = true
}

// Aplică dark mode la document
const applyDarkMode = (dark: boolean) => {
  if (dark) {
    document.documentElement.classList.add('dark')
  } else {
    document.documentElement.classList.remove('dark')
  }
}

// Salvează preferința în localStorage
const saveDarkModePreference = (dark: boolean) => {
  try {
    localStorage.setItem(STORAGE_KEY, dark.toString())
    console.log('💾 Dark mode saved:', dark)
  } catch (error) {
    console.error('❌ Error saving dark mode preference:', error)
  }
}

export const useDarkMode = () => {
  // Inițializează la prima utilizare
  if (!isInitialized) {
    loadDarkModePreference()
  }
  
  // Toggle dark mode
  const toggleDarkMode = () => {
    isDarkMode.value = !isDarkMode.value
    applyDarkMode(isDarkMode.value)
    saveDarkModePreference(isDarkMode.value)
  }
  
  // Setează direct dark mode
  const setDarkMode = (dark: boolean) => {
    isDarkMode.value = dark
    applyDarkMode(dark)
    saveDarkModePreference(dark)
  }
  
  // Watch pentru schimbări
  watch(isDarkMode, (newValue) => {
    applyDarkMode(newValue)
    saveDarkModePreference(newValue)
  })
  
  return {
    isDarkMode,
    toggleDarkMode,
    setDarkMode
  }
}
