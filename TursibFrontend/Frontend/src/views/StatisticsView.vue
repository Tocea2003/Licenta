<template>
  <div class="statistics-page">
    <div class="header">
      <button type="button" @click="goBack" class="back-btn" :aria-label="t('back')">
        <ArrowLeft :size="20" aria-hidden="true" />
      </button>
      <div class="header-info">
        <h1 class="page-title">{{ t('statsMyTitle') }}</h1>
        <p>{{ t('appUsage') }}</p>
      </div>
    </div>

    <div class="content">
      <!-- Overview Cards -->
      <div class="stats-grid">
        <div class="stat-card primary">
          <span class="stat-icon" aria-hidden="true"><Search :size="18" /></span>
          <div class="stat-value">{{ statistics.totalSearches }}</div>
          <div class="stat-label">{{ t('totalSearches') }}</div>
        </div>
        
        <div class="stat-card success">
          <span class="stat-icon" aria-hidden="true"><BusFront :size="18" /></span>
          <div class="stat-value">{{ statistics.totalTrips }}</div>
          <div class="stat-label">{{ t('totalTrips') }}</div>
        </div>
        
        <div class="stat-card warning">
          <span class="stat-icon" aria-hidden="true"><Clock :size="18" /></span>
          <div class="stat-value">{{ formatTime(statistics.totalTimeUsed) }}</div>
          <div class="stat-label">{{ t('timeUsed') }}</div>
        </div>
        
        <div class="stat-card info">
          <span class="stat-icon" aria-hidden="true"><TrendingUp :size="18" /></span>
          <div class="stat-value">{{ averageSearchesPerDay }}</div>
          <div class="stat-label">{{ t('avgPerDay') }}</div>
        </div>
      </div>

      <!-- Search Activity Chart -->
      <div class="section">
        <h2>{{ t('last7Days') }}</h2>
        <div class="activity-chart">
          <div 
            v-for="(entry, index) in last7Days" 
            :key="entry.date || index"
            class="chart-bar"
            :class="{ empty: entry.count === 0 }"
            :style="{ height: getBarHeight(entry.count) + 'px' }"
            :title="`${entry.count} ${t('searches')}`"
          >
            <div class="bar-value">{{ entry.count }}</div>
            <div class="bar-label">{{ formatDay(entry.date, index) }}</div>
          </div>
        </div>
      </div>

      <!-- Top Routes -->
      <div class="section">
        <h2>{{ t('topRoutes') }}</h2>
        <div v-if="statistics.favoriteRoutes.length === 0" class="empty-state">
          <p>{{ t('noRouteUsed') }}</p>
        </div>
        <div v-else class="list-items">
          <div 
            v-for="(route, index) in statistics.favoriteRoutes.slice(0, 5)" 
            :key="route.routeId"
            class="list-item"
          >
            <div class="rank">{{ index + 1 }}</div>
            <span class="line-pill" :style="{ background: lineColor(route.routeId) }">{{ route.routeId }}</span>
            <div class="item-info">
              <h3>{{ t('lineLabel') }} {{ route.routeId }}</h3>
              <p>{{ route.count }} {{ t('usages') }}</p>
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
        <h2>{{ t('topStations') }}</h2>
        <div v-if="statistics.favoriteStations.length === 0" class="empty-state">
          <p>{{ t('noStationSearched') }}</p>
        </div>
        <div v-else class="list-items">
          <div 
            v-for="(station, index) in statistics.favoriteStations.slice(0, 5)" 
            :key="station.stationId"
            class="list-item"
          >
            <div class="rank">{{ index + 1 }}</div>
            <div class="item-info">
              <h3>{{ t('stationLabel') }} #{{ station.stationId }}</h3>
              <p>{{ station.count }} {{ t('accesses') }}</p>
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
        <button type="button" @click="confirmReset" class="reset-btn">
          <Trash2 :size="18" aria-hidden="true" />{{ t('resetStatistics') }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, BusFront, Clock, Search, TrendingUp, Trash2 } from 'lucide-vue-next'
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useStatistics } from '@/composables/useStatistics'
import { useLanguage } from '@/composables/useLanguage'

const router = useRouter()
const { statistics, averageSearchesPerDay, resetStatistics } = useStatistics()
const { t, currentLanguage } = useLanguage()

const last7Days = computed(() => {
  const result = []
  const today = new Date()
  const activity = statistics.value.dailyActivity || []

  for (let i = 6; i >= 0; i--) {
    const date = new Date(today)
    date.setDate(date.getDate() - i)
    const dateString = date.toISOString().split('T')[0]

    const existingEntry = activity.find(entry => entry.date === dateString)
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
  const daysRo = ['Dum', 'Lun', 'Mar', 'Mie', 'Joi', 'Vin', 'Sam']
  const daysEn = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
  const days = currentLanguage.value === 'ro' ? daysRo : daysEn
  
  if (index === 6) return t('today')
  if (index === 5) return t('yesterday')
  
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
  if (confirm(t('confirmResetStats'))) {
    resetStatistics()
    alert(`✅ ${t('statsResetDone')}`)
  }
}

// Culorile liniilor din lista de rute salvată de planificator (dacă există)
const cachedRouteColors: Record<number, string> = (() => {
  try {
    const list = JSON.parse(localStorage.getItem('routesList.v2') || '[]') as Array<{ id: number; color?: string }>
    return Object.fromEntries(list.filter(r => r.color).map(r => [r.id, r.color as string]))
  } catch {
    return {}
  }
})()

const lineColor = (routeId: number) => cachedRouteColors[routeId] ?? '#5f6672'
</script>

<style scoped>
.statistics-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 16px 16px 32px;
  font-variant-numeric: tabular-nums;
}

.header {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}

.back-btn {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.back-btn:hover {
  background: var(--bg-tertiary);
}

.header-info p {
  margin: 4px 0 0;
  color: var(--text-secondary);
  font-size: 14px;
}

.content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.stat-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 14px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
}

.stat-icon {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-md);
  background: var(--bg-tertiary);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 4px;
}

.stat-value {
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: var(--fw-heavy);
  letter-spacing: -0.02em;
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
}

.section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section h2 {
  margin: 0;
  font-family: var(--font-sans);
  font-size: 13px;
  font-weight: var(--fw-bold);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-secondary);
}

.activity-chart {
  display: flex;
  align-items: flex-end;
  gap: 8px;
  height: 170px;
  padding: 28px 12px 34px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
}

.chart-bar {
  position: relative;
  flex: 1;
  min-height: 4px;
  border-radius: 6px 6px 2px 2px;
  background: var(--accent-primary);
}

.chart-bar.empty {
  background: var(--bg-tertiary);
}

.bar-value {
  position: absolute;
  top: -22px;
  left: 0;
  right: 0;
  text-align: center;
  font-size: 12px;
  font-weight: var(--fw-bold);
}

.bar-label {
  position: absolute;
  bottom: -26px;
  left: 0;
  right: 0;
  text-align: center;
  font-size: 12px;
  color: var(--text-secondary);
}

.empty-state {
  padding: 16px;
  border-radius: var(--radius-lg);
  background: var(--bg-primary);
  color: var(--text-secondary);
  font-size: 14px;
}

.empty-state p {
  margin: 0;
}

.list-items {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.list-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-radius: var(--radius-md);
  background: var(--bg-primary);
}

.rank {
  width: 20px;
  font-weight: var(--fw-bold);
  color: var(--text-secondary);
}

.item-info {
  flex: 1;
  min-width: 0;
}

.item-info h3 {
  margin: 0;
  font-family: var(--font-sans);
  font-size: 15px;
  font-weight: var(--fw-semibold);
}

.item-info p {
  margin: 2px 0 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.progress-bar {
  width: 80px;
  height: 6px;
  border-radius: 3px;
  background: var(--bg-tertiary);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  border-radius: 3px;
  background: var(--text-primary);
}

.reset-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 48px;
  border: 1px solid var(--border-primary);
  border-radius: var(--radius-md);
  background: var(--bg-primary);
  color: var(--color-danger);
  font: inherit;
  font-weight: var(--fw-bold);
  cursor: pointer;
}

.reset-btn:hover {
  background: var(--color-danger-soft);
}
</style>
