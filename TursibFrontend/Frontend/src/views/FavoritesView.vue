<template>
  <div class="favorites-view">
    <div class="header">
      <h1>Locații Favorite</h1>
      <p class="subtitle">Salvează locațiile tale frecvente pentru acces rapid</p>
    </div>

    <!-- Quick Add Buttons -->
    <div class="quick-add">
      <button 
        v-if="!homeFavorite" 
        @click="openAddDialog('home')"
        class="quick-btn home"
      >
        <span class="icon">🏠</span>
        <span class="text">Adaugă Casă</span>
      </button>
      <button 
        v-if="!workFavorite" 
        @click="openAddDialog('work')"
        class="quick-btn work"
      >
        <span class="icon">💼</span>
        <span class="text">Adaugă Serviciu</span>
      </button>
      <button 
        @click="openAddDialog('custom')"
        class="quick-btn custom"
      >
        <span class="icon">➕</span>
        <span class="text">Locație Nouă</span>
      </button>
    </div>

    <!-- Favorites List -->
    <div class="favorites-list">
      <!-- Home -->
      <div v-if="homeFavorite" class="favorite-item home">
        <div class="favorite-icon">🏠</div>
        <div class="favorite-info">
          <h3>{{ homeFavorite.name }}</h3>
          <p>{{ homeFavorite.address }}</p>
        </div>
        <div class="favorite-actions">
          <button @click="editFavorite(homeFavorite)" class="action-btn edit">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M11 4H4C3.46957 4 2.96086 4.21071 2.58579 4.58579C2.21071 4.96086 2 5.46957 2 6V20C2 20.5304 2.21071 21.0391 2.58579 21.4142C2.96086 21.7893 3.46957 22 4 22H18C18.5304 22 19.0391 21.7893 19.4142 21.4142C19.7893 21.0391 20 20.5304 20 20V13" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M18.5 2.50023C18.8978 2.1024 19.4374 1.87891 20 1.87891C20.5626 1.87891 21.1022 2.1024 21.5 2.50023C21.8978 2.89805 22.1213 3.43762 22.1213 4.00023C22.1213 4.56284 21.8978 5.1024 21.5 5.50023L12 15.0002L8 16.0002L9 12.0002L18.5 2.50023Z" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <button @click="confirmDelete(homeFavorite)" class="action-btn delete">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M3 6H5H21M8 6V4C8 3.46957 8.21071 2.96086 8.58579 2.58579C8.96086 2.21071 9.46957 2 10 2H14C14.5304 2 15.0391 2.21071 15.4142 2.58579C15.7893 2.96086 16 3.46957 16 4V6M19 6V20C19 20.5304 18.7893 21.0391 18.4142 21.4142C18.0391 21.7893 17.5304 22 17 22H7C6.46957 22 5.96086 21.7893 5.58579 21.4142C5.21071 21.0391 5 20.5304 5 20V6H19Z" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Work -->
      <div v-if="workFavorite" class="favorite-item work">
        <div class="favorite-icon">💼</div>
        <div class="favorite-info">
          <h3>{{ workFavorite.name }}</h3>
          <p>{{ workFavorite.address }}</p>
        </div>
        <div class="favorite-actions">
          <button @click="editFavorite(workFavorite)" class="action-btn edit">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M11 4H4C3.46957 4 2.96086 4.21071 2.58579 4.58579C2.21071 4.96086 2 5.46957 2 6V20C2 20.5304 2.21071 21.0391 2.58579 21.4142C2.96086 21.7893 3.46957 22 4 22H18C18.5304 22 19.0391 21.7893 19.4142 21.4142C19.7893 21.0391 20 20.5304 20 20V13" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M18.5 2.50023C18.8978 2.1024 19.4374 1.87891 20 1.87891C20.5626 1.87891 21.1022 2.1024 21.5 2.50023C21.8978 2.89805 22.1213 3.43762 22.1213 4.00023C22.1213 4.56284 21.8978 5.1024 21.5 5.50023L12 15.0002L8 16.0002L9 12.0002L18.5 2.50023Z" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <button @click="confirmDelete(workFavorite)" class="action-btn delete">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M3 6H5H21M8 6V4C8 3.46957 8.21071 2.96086 8.58579 2.58579C8.96086 2.21071 9.46957 2 10 2H14C14.5304 2 15.0391 2.21071 15.4142 2.58579C15.7893 2.96086 16 3.46957 16 4V6M19 6V20C19 20.5304 18.7893 21.0391 18.4142 21.4142C18.0391 21.7893 17.5304 22 17 22H7C6.46957 22 5.96086 21.7893 5.58579 21.4142C5.21071 21.0391 5 20.5304 5 20V6H19Z" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
        </div>
      </div>

      <!-- Custom Favorites -->
      <div 
        v-for="favorite in customFavorites" 
        :key="favorite.id"
        class="favorite-item custom"
      >
        <div class="favorite-icon">{{ favorite.icon }}</div>
        <div class="favorite-info">
          <h3>{{ favorite.name }}</h3>
          <p>{{ favorite.address }}</p>
        </div>
        <div class="favorite-actions">
          <button @click="editFavorite(favorite)" class="action-btn edit">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M11 4H4C3.46957 4 2.96086 4.21071 2.58579 4.58579C2.21071 4.96086 2 5.46957 2 6V20C2 20.5304 2.21071 21.0391 2.58579 21.4142C2.96086 21.7893 3.46957 22 4 22H18C18.5304 22 19.0391 21.7893 19.4142 21.4142C19.7893 21.0391 20 20.5304 20 20V13" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M18.5 2.50023C18.8978 2.1024 19.4374 1.87891 20 1.87891C20.5626 1.87891 21.1022 2.1024 21.5 2.50023C21.8978 2.89805 22.1213 3.43762 22.1213 4.00023C22.1213 4.56284 21.8978 5.1024 21.5 5.50023L12 15.0002L8 16.0002L9 12.0002L18.5 2.50023Z" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <button @click="confirmDelete(favorite)" class="action-btn delete">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
              <path d="M3 6H5H21M8 6V4C8 3.46957 8.21071 2.96086 8.58579 2.58579C8.96086 2.21071 9.46957 2 10 2H14C14.5304 2 15.0391 2.21071 15.4142 2.58579C15.7893 2.96086 16 3.46957 16 4V6M19 6V20C19 20.5304 18.7893 21.0391 18.4142 21.4142C18.0391 21.7893 17.5304 22 17 22H7C6.46957 22 5.96086 21.7893 5.58579 21.4142C5.21071 21.0391 5 20.5304 5 20V6H19Z" 
                stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-if="favorites.length === 0" class="empty-state">
      <div class="empty-icon">📍</div>
      <h3>Nicio locație salvată</h3>
      <p>Adaugă locațiile tale frecvente pentru a le accesa rapid</p>
    </div>

    <!-- Add/Edit Dialog -->
    <div v-if="showDialog" class="dialog-overlay" @click="closeDialog">
      <div class="dialog" @click.stop>
        <h2>{{ editingFavorite ? 'Editează Locație' : 'Adaugă Locație' }}</h2>
        
        <div class="form-group">
          <label>Nume locație</label>
          <input 
            v-model="form.name" 
            type="text" 
            placeholder="ex: Casă, Serviciu, Sală sport"
            maxlength="50"
          />
        </div>

        <div class="form-group">
          <label>Adresă</label>
          <input 
            v-model="form.address" 
            type="text" 
            placeholder="Caută adresa..."
            @input="searchAddress"
          />
          
          <!-- Address suggestions -->
          <div v-if="addressSuggestions.length > 0" class="suggestions">
            <div 
              v-for="suggestion in addressSuggestions" 
              :key="suggestion.place_id"
              @click="selectAddress(suggestion)"
              class="suggestion-item"
            >
              <span class="suggestion-icon">📍</span>
              <span class="suggestion-text">{{ suggestion.display_name }}</span>
            </div>
          </div>
        </div>

        <div class="form-group">
          <label>Icon</label>
          <div class="icon-picker">
            <button 
              v-for="icon in availableIcons" 
              :key="icon"
              @click="form.icon = icon"
              :class="{ selected: form.icon === icon }"
              class="icon-option"
            >
              {{ icon }}
            </button>
          </div>
        </div>

        <div class="dialog-actions">
          <button @click="closeDialog" class="btn-cancel">Anulează</button>
          <button @click="saveFavorite" class="btn-save" :disabled="!canSave">
            {{ editingFavorite ? 'Salvează' : 'Adaugă' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFavorites, type FavoriteLocation } from '@/composables/useFavorites'

const {
  favorites,
  homeFavorite,
  workFavorite,
  customFavorites,
  addFavorite,
  removeFavorite,
  updateFavorite
} = useFavorites()

const showDialog = ref(false)
const editingFavorite = ref<FavoriteLocation | null>(null)
const form = ref({
  name: '',
  address: '',
  lat: 0,
  lon: 0,
  type: 'custom' as 'home' | 'work' | 'custom',
  icon: '📍'
})

const addressSuggestions = ref<any[]>([])
let searchTimeout: number | null = null

const availableIcons = ['🏠', '💼', '🏫', '🏥', '🛒', '🎭', '⛪', '🏋️', '🍽️', '☕', '📚', '🎵', '🌳', '🏖️', '⛰️', '📍']

const canSave = computed(() => {
  return form.value.name.trim() !== '' && 
         form.value.address.trim() !== '' &&
         form.value.lat !== 0 &&
         form.value.lon !== 0
})

const openAddDialog = (type: 'home' | 'work' | 'custom') => {
  editingFavorite.value = null
  form.value = {
    name: type === 'home' ? 'Casă' : type === 'work' ? 'Serviciu' : '',
    address: '',
    lat: 0,
    lon: 0,
    type,
    icon: type === 'home' ? '🏠' : type === 'work' ? '💼' : '📍'
  }
  showDialog.value = true
}

const editFavorite = (favorite: FavoriteLocation) => {
  editingFavorite.value = favorite
  form.value = {
    name: favorite.name,
    address: favorite.address,
    lat: favorite.lat,
    lon: favorite.lon,
    type: favorite.type,
    icon: favorite.icon
  }
  showDialog.value = true
}

const closeDialog = () => {
  showDialog.value = false
  editingFavorite.value = null
  addressSuggestions.value = []
}

const searchAddress = () => {
  if (searchTimeout) clearTimeout(searchTimeout)
  
  const query = form.value.address.trim()
  if (query.length < 3) {
    addressSuggestions.value = []
    return
  }
  
  searchTimeout = setTimeout(async () => {
    try {
      const response = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}, Sibiu&limit=5`,
        {
          headers: {
            'Accept': 'application/json'
          }
        }
      )
      const data = await response.json()
      addressSuggestions.value = data
    } catch (error) {
      console.error('❌ Error searching address:', error)
      addressSuggestions.value = []
    }
  }, 300)
}

const selectAddress = (suggestion: any) => {
  form.value.address = suggestion.display_name
  form.value.lat = parseFloat(suggestion.lat)
  form.value.lon = parseFloat(suggestion.lon)
  addressSuggestions.value = []
}

const saveFavorite = async () => {
  if (!canSave.value) return
  
  if (editingFavorite.value) {
    // Update existing
    await updateFavorite(editingFavorite.value.id, {
      name: form.value.name,
      address: form.value.address,
      lat: form.value.lat,
      lon: form.value.lon,
      icon: form.value.icon
    })
  } else {
    // Add new
    await addFavorite({
      name: form.value.name,
      address: form.value.address,
      lat: form.value.lat,
      lon: form.value.lon,
      type: form.value.type,
      icon: form.value.icon
    })
  }
  
  closeDialog()
}

const confirmDelete = async (favorite: FavoriteLocation) => {
  if (confirm(`Ștergi locația "${favorite.name}"?`)) {
    await removeFavorite(favorite.id)
  }
}
</script>

<style scoped>
.favorites-view {
  padding: 32px 24px;
  padding-bottom: 100px; /* Space for bottom nav */
  min-height: 100vh;
  background: var(--gradient-bg);
}

.header {
  margin-bottom: 32px;
  text-align: center;
}

.header h1 {
  font-size: 2rem;
  font-weight: 800;
  margin: 0 0 8px 0;
  color: var(--text-primary);
}

.subtitle {
  color: var(--text-secondary);
  font-size: 0.95rem;
  margin: 0;
  font-weight: 500;
}

.quick-add {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 12px;
  margin-bottom: 32px;
}

.quick-btn {
  background: var(--bg-primary);
  border: 1.5px dashed var(--border-secondary);
  border-radius: var(--radius-lg, 14px);
  padding: 20px 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: var(--shadow-xs);
  -webkit-tap-highlight-color: transparent;
}

.quick-btn:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
  border-style: solid;
}

.quick-btn.home:hover {
  border-color: var(--color-danger);
  background: var(--color-danger-soft);
}

.quick-btn.work:hover {
  border-color: var(--accent-primary);
  background: var(--accent-primary-soft);
}

.quick-btn.custom:hover {
  border-color: var(--accent-secondary);
  background: var(--accent-secondary-soft);
}

.quick-btn .icon {
  font-size: 2rem;
}

.quick-btn .text {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--text-primary);
}

.favorites-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.favorite-item {
  background: var(--bg-primary);
  border-radius: 14px;
  padding: 16px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: var(--shadow-sm);
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  border: 1px solid var(--border-color);
  position: relative;
  overflow: hidden;
}

.favorite-item::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
  background: var(--border-secondary);
  border-radius: 4px 0 0 4px;
}

.favorite-item.home::before { background: var(--color-danger); }
.favorite-item.work::before { background: var(--accent-primary); }
.favorite-item.custom::before { background: var(--accent-secondary); }

.favorite-item:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-2px);
}

.favorite-icon {
  font-size: 2rem;
  width: 56px;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-secondary);
  border-radius: 12px;
  flex-shrink: 0;
}

.favorite-item.home .favorite-icon { background: var(--color-danger-soft); }
.favorite-item.work .favorite-icon { background: var(--accent-primary-soft); }
.favorite-item.custom .favorite-icon { background: var(--accent-secondary-soft); }

.favorite-info {
  flex: 1;
  min-width: 0;
}

.favorite-info h3 {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.favorite-info p {
  font-size: 13px;
  color: var(--text-secondary);
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.favorite-actions {
  display: flex;
  gap: 8px;
}

.action-btn {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  border: none;
  background: var(--bg-secondary);
  color: var(--text-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  flex-shrink: 0;
}

.action-btn:hover {
  transform: scale(1.05);
}

.action-btn.edit:hover {
  background: var(--accent-primary-soft);
  color: var(--accent-primary);
}

.action-btn.delete:hover {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.empty-state {
  text-align: center;
  padding: 80px 24px;
  max-width: 500px;
  margin: 0 auto;
}

.empty-icon {
  font-size: 5rem;
  margin-bottom: 24px;
  opacity: 0.8;
}

.empty-state h3 {
  font-size: 1.35rem;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.empty-state p {
  color: var(--text-secondary);
  font-size: 0.95rem;
  margin: 0;
  line-height: 1.5;
}

/* Dialog */
.dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
  padding: 24px;
  animation: fadeIn 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.dialog {
  background: var(--bg-primary);
  border-radius: 20px;
  padding: 28px;
  max-width: 520px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  animation: slideUp 0.35s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(30px) scale(0.95);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

.dialog h2 {
  font-size: 1.35rem;
  font-weight: 700;
  background: var(--gradient-primary);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  margin: 0 0 24px 0;
  letter-spacing: -0.02em;
}

.form-group {
  margin-bottom: 20px;
  position: relative;
}

.form-group label {
  display: block;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 8px;
}

.form-group input {
  width: 100%;
  padding: 12px 14px;
  border: 2px solid var(--border-color);
  border-radius: 10px;
  font-size: 0.95rem;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  font-family: inherit;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.form-group input:focus {
  outline: none;
  border-color: var(--accent-primary);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.12);
  background: var(--bg-primary);
}

.suggestions {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  margin-top: 4px;
  max-height: 200px;
  overflow-y: auto;
  box-shadow: var(--shadow-md);
  z-index: 10;
}

.suggestion-item {
  padding: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  transition: background 0.2s;
}

.suggestion-item:hover {
  background: var(--bg-tertiary);
}

.suggestion-icon {
  font-size: 18px;
}

.suggestion-text {
  font-size: 14px;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.icon-picker {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 8px;
}

.icon-option {
  width: 44px;
  height: 44px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-secondary);
  font-size: 24px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-option:hover {
  border-color: #3b82f6;
  background: #eff6ff;
}

.icon-option.selected {
  border-color: #3b82f6;
  background: #dbeafe;
  transform: scale(1.1);
}

.dialog-actions {
  display: flex;
  gap: 12px;
  margin-top: 24px;
}

.dialog-actions button {
  flex: 1;
  padding: 13px;
  border-radius: 10px;
  font-size: 0.95rem;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.btn-cancel {
  background: var(--bg-secondary);
  border: 2px solid var(--border-color);
  color: var(--text-primary);
}

.btn-cancel:hover {
  background: var(--bg-tertiary);
  border-color: var(--border-color);
}

.btn-save {
  background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
  border: none;
  color: white;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.25);
}

.btn-save:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(59, 130, 246, 0.35);
}

.btn-save:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

@media (max-width: 768px) {
  .favorites-view {
    padding: 16px;
  }
  
  .icon-picker {
    grid-template-columns: repeat(6, 1fr);
  }
}
</style>
