import { ref } from 'vue'

const STORAGE_KEY = 'tursib_onboarding_completed'
const CURRENT_VERSION = '1.0'

// State global pentru onboarding
const hasSeenOnboarding = ref(false)
const showOnboarding = ref(false)

// Verifică dacă utilizatorul a văzut onboarding-ul
const checkOnboardingStatus = () => {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored === CURRENT_VERSION) {
      hasSeenOnboarding.value = true
      showOnboarding.value = false
    } else {
      // Utilizator nou sau versiune nouă de onboarding
      hasSeenOnboarding.value = false
      showOnboarding.value = true
    }
    console.log('✅ Onboarding status:', hasSeenOnboarding.value ? 'Completed' : 'Not seen')
  } catch (error) {
    console.error('❌ Error loading onboarding status:', error)
    showOnboarding.value = true
  }
}

// Marchează onboarding-ul ca văzut
const completeOnboarding = () => {
  try {
    localStorage.setItem(STORAGE_KEY, CURRENT_VERSION)
    hasSeenOnboarding.value = true
    showOnboarding.value = false
    console.log('✅ Onboarding completed')
  } catch (error) {
    console.error('❌ Error saving onboarding status:', error)
  }
}

// Resetează onboarding (pentru testing)
const resetOnboarding = () => {
  try {
    localStorage.removeItem(STORAGE_KEY)
    hasSeenOnboarding.value = false
    showOnboarding.value = true
    console.log('🔄 Onboarding reset')
  } catch (error) {
    console.error('❌ Error resetting onboarding:', error)
  }
}

// Afișează manual onboarding
const startOnboarding = () => {
  showOnboarding.value = true
}

export const useOnboarding = () => {
  // Inițializează la prima utilizare
  if (!hasSeenOnboarding.value && !showOnboarding.value) {
    checkOnboardingStatus()
  }

  return {
    showOnboarding,
    hasSeenOnboarding,
    completeOnboarding,
    resetOnboarding,
    startOnboarding,
  }
}
