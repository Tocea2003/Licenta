<template>
  <div class="statistics-page">
    <div class="header">
      <button @click="goBack" class="back-btn">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
          <path d="M19 12H5M5 12L12 19M5 12L12 5" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      <div class="header-info">
        <h1>📊 Statisticile Mele</h1>
        <p>Utilizarea aplicației</p>
      </div>
    </div>

    <div class="content">
      <!-- Overview Cards -->
      <div class="stats-grid">
        <div class="stat-card primary">
          <div class="stat-icon">🔍</div>
          <div class="stat-value">{{ statistics.totalSearches }}</div>
          <div class="stat-label">Căutări Total</div>
        </div>
        
        <div class="stat-card success">
          <div class="stat-icon">🚌</div>
          <div class="stat-value">{{ statistics.totalTrips }}</div>
          <div class="stat-label">Călătorii</div>
        </div>
        
        <div class="stat-card warning">
          <div class="stat-icon">⏱️</div>
          <div class="stat-value">{{ formatTime(statistics.totalTimeUsed) }}</div>
          <div class="stat-label">Timp Folosit</div>
        </div>
        
        <div class="stat-card info">
          <div class="stat-icon">📈</div>
          <div class="stat-value">{{ averageSearchesPerDay }}</div>
          <div class="stat-label">Medie/Zi</div>
        </div>
      </div>

      <!-- Search Activity Chart -->
      <div class="section">
        <h2>📅 Activitate Ultimele 7 Zile</h2>
        <div class="activity-chart">
          <div 
            v-for="(entry, index) in last7Days" 
            :key="entry.date || index"
            class="chart-bar"
            :style="{ height: getBarHeight(entry.count) + 'px' }"
            :title="`${entry.count} căutări`"
          >
            <div class="bar-value">{{ entry.count }}</div>
            <div class="bar-label">{{ formatDay(entry.date, index) }}</div>
          </div>
        </div>
      </div>

      <!-- Top Routes -->
      <div class="section">
        <h2>🚌 Trasee Preferate</h2>
        <div v-if="statistics.favoriteRoutes.length === 0" class="empty-state">
          <p>Nu ai folosit încă niciun traseu</p>
        </div>
        <div v-else class="list-items">
          <div 
            v-for="(route, index) in statistics.favoriteRoutes.slice(0, 5)" 
            :key="route.routeId"
            class="list-item"
          >
            <div class="rank">{{ index + 1 }}</div>
            <div class="item-info">
              <h3>Linia {{ route.routeId }}</h3>
              <p>{{ route.count }} utilizări</p>
            </div>
            <div class="progress-bar">
              <div 
                class="progress-fill" 
                :style="{ width: getPercentage(route.count, statistics.favoriteRoutes[0]?.count) + '%' }"
              ></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Top Stations -->
      <div class="section">
        <h2>🚏 Stații Preferate</h2>
        <div v-if="statistics.favoriteStations.length === 0" class="empty-state">
          <p>Nu ai căutat încă nicio stație</p>
        </div>
        <div v-else class="list-items">
          <div 
            v-for="(station, index) in statistics.favoriteStations.slice(0, 5)" 
            :key="station.stationId"
            class="list-item"
          >
            <div class="rank">{{ index + 1 }}</div>
            <div class="item-info">
              <h3>Stația #{{ station.stationId }}</h3>
              <p>{{ station.count }} accesări</p>
            </div>
            <div class="progress-bar">
              <div 
                class="progress-fill" 
                :style="{ width: getPercentage(station.count, statistics.favoriteStations[0]?.count) + '%' }"
              ></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Reset Button -->
      <div class="section">
        <button @click="confirmReset" class="reset-btn">
          🗑️ Resetează Statisticile
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useStatistics } from '@/composables/useStatistics'

const router = useRouter()
const { statistics, averageSearchesPerDay, resetStatistics } = useStatistics()

const last7Days = computed(() => {
  const result = []
  const today = new Date()
  
  for (let i = 6; i >= 0; i--) {
    const date = new Date(today)
    date.setDate(date.getDate() - i)
    const dateString = date.toISOString().split('T')[0]
    
    const existingEntry = statistics.value.searchHistory.find(entry => entry.date === dateString)
    result.push({
      date: dateString,
      count: existingEntry ? existingEntry.count : 0
    })
  }
  
  return result
})

const getBarHeight = (count: number): number => {
  const maxCount = Math.max(...last7Days.value.map(d => d.count), 1)
  return Math.max((count / maxCount) * 120, 20)
}

const formatDay = (dateString: string | undefined, index: number): string => {
  if (!dateString) return '?'
  
  const date = new Date(dateString)
  const days = ['Dum', 'Lun', 'Mar', 'Mie', 'Joi', 'Vin', 'Sâm']
  
  if (index === 6) return 'Azi'
  if (index === 5) return 'Ieri'
  
  return days[date.getDay()] || '?'
}

const formatTime = (minutes: number): string => {
  if (minutes < 60) return `${minutes}m`
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`
}

const getPercentage = (value: number, max: number | undefined): number => {
  if (!max || max === 0) return 0
  return Math.round((value / max) * 100)
}

const goBack = () => {
  router.push('/settings')
}

const confirmReset = () => {
  if (confirm('Ești sigur că vrei să resetezi toate statisticile? Această acțiune nu poate fi anulată.')) {
    resetStatistics()
    alert('✅ Statisticile au fost resetate!')
  }
}
</script>

<style scoped>
.statistics-page {
  min-height: 100vh;
  background: var(--gradient-bg);
  padding-bottom: 100px;
}

.header {
  background: var(--gradient-primary);
  padding: 24px;
  color: white;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: var(--shadow-md);
}

.back-btn {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  border-radius: 10px;
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  flex-shrink: 0;
}

.back-btn:hover {
  background: rgba(255, 255, 255, 0.3);
  transform: scale(1.05);
}

.header-info h1 {
  margin: 0 0 4px 0;
  font-size: 1.5rem;
  font-weight: 800;
}

.header-info p {
  margin: 0;
  font-size: 0.85rem;
  opacity: 0.9;
}

.content {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.stat-card {
  background: var(--bg-primary);
  border-radius: 16px;
  padding: 20px;
  text-align: center;
  box-shadow: var(--shadow-sm);
  transition: all 0.2s;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-md);
}

.stat-card.primary {
  border-top: 4px solid #3b82f6;
}

.stat-card.success {
  border-top: 4px solid #10b981;
}

.stat-card.warning {
  border-top: 4px solid #f59e0b;
}

.stat-card.info {
  border-top: 4px solid #8b5cf6;
}

.stat-icon {
  font-size: 2rem;
  margin-bottom: 8px;
}

.stat-value {
  font-size: 1.75rem;
  font-weight: 800;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.stat-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.section {
  background: var(--bg-primary);
  border-radius: 16px;
  padding: 20px;
  box-shadow: var(--shadow-sm);
}

.section h2 {
  margin: 0 0 16px 0;
  font-size: 1.1rem;
  font-weight: 700;
  color: var(--text-primary);
}

.activity-chart {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 8px;
  height: 160px;
  padding-top: 20px;
}

.chart-bar {
  flex: 1;
  background: linear-gradient(180deg, #3b82f6 0%, #8b5cf6 100%);
  border-radius: 8px 8px 0 0;
  display: flex;
  flex-direction: column;
  justify-content: flex-start;
  align-items: center;
  padding: 4px;
  position: relative;
  transition: all 0.3s;
  cursor: pointer;
}

.chart-bar:hover {
  opacity: 0.8;
  transform: scaleY(1.05);
}

.bar-value {
  font-size: 0.75rem;
  font-weight: 700;
  color: white;
  margin-bottom: 4px;
}

.bar-label {
  position: absolute;
  bottom: -24px;
  font-size: 0.7rem;
  color: var(--text-secondary);
  font-weight: 600;
}

.list-items {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.list-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.rank {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: linear-gradient(135deg, #3b82f6 0%, #8b5cf6 100%);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 800;
  font-size: 0.9rem;
  flex-shrink: 0;
}

.item-info {
  flex: 1;
  min-width: 0;
}

.item-info h3 {
  margin: 0 0 2px 0;
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-primary);
}

.item-info p {
  margin: 0;
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.progress-bar {
  width: 100px;
  height: 8px;
  background: var(--bg-tertiary);
  border-radius: 4px;
  overflow: hidden;
  flex-shrink: 0;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #3b82f6 0%, #8b5cf6 100%);
  border-radius: 4px;
  transition: width 0.5s;
}

.empty-state {
  text-align: center;
  padding: 40px 20px;
  color: var(--text-tertiary);
  font-size: 0.9rem;
}

.reset-btn {
  width: 100%;
  padding: 14px;
  background: #ef4444;
  color: white;
  border: none;
  border-radius: 12px;
  font-weight: 600;
  font-size: 0.95rem;
  cursor: pointer;
  transition: all 0.2s;
}

.reset-btn:hover {
  background: #dc2626;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(239, 68, 68, 0.3);
}

@media (max-width: 640px) {
  .stats-grid {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
