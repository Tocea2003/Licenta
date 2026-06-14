<template>
  <div class="analytics-dashboard">
    <div class="page-header">
      <div>
        <h2>📊 {{ t('analyticsDashboard') }}</h2>
        <p class="subtitle">{{ t('analyticsSubtitle') }}</p>
      </div>
      <div class="header-live" v-if="!isLoading">
        <span class="live-dot"></span>
        <span>{{ liveBuses.length }} {{ t('liveBuses') }}</span>
      </div>
    </div>

    <div v-if="isLoading" class="loading-state">
      <div class="loading-spinner"></div>
      <p>{{ t('loadingData') }}</p>
    </div>

    <div v-else class="analytics-content">
      <!-- Statistici rapide -->
      <div class="stats-grid">
        <div class="stat-card stat-buses">
          <div class="stat-glow"></div>
          <div class="stat-icon-wrap"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="14" rx="2"/><path d="M3 10h18M7 21v-4M17 21v-4"/></svg></div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.activeBuses }}</div>
            <div class="stat-label">{{ t('activeBusesLabel') }}</div>
          </div>
        </div>

        <div class="stat-card stat-occupancy">
          <div class="stat-glow"></div>
          <div class="stat-icon-wrap"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75"/></svg></div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.avgOccupancy }}<span class="stat-unit">%</span></div>
            <div class="stat-label">{{ t('avgOccupancyLabel') }}</div>
          </div>
        </div>

        <div class="stat-card stat-routes">
          <div class="stat-glow"></div>
          <div class="stat-icon-wrap"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg></div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.totalRoutes }}</div>
            <div class="stat-label">{{ t('totalRoutesLabel') }}</div>
          </div>
        </div>

        <div class="stat-card stat-stations">
          <div class="stat-glow"></div>
          <div class="stat-icon-wrap"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0118 0z"/><circle cx="12" cy="10" r="3"/></svg></div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.totalStations }}</div>
            <div class="stat-label">{{ t('totalStationsLabel') }}</div>
          </div>
        </div>
      </div>

      <!-- Grafice -->
      <div class="charts-grid">
        <div class="chart-card">
          <div class="chart-header">
            <h3>{{ t('avgOccupancyByRoute') }}</h3>
            <span class="chart-badge">Bar</span>
          </div>
          <div class="chart-body"><canvas ref="occupancyChartCanvas"></canvas></div>
        </div>

        <div class="chart-card">
          <div class="chart-header">
            <h3>{{ t('activeBusesRealtime') }}</h3>
            <span class="chart-badge">Line</span>
          </div>
          <div class="chart-body"><canvas ref="busesChartCanvas"></canvas></div>
        </div>

        <div class="chart-card">
          <div class="chart-header">
            <h3>{{ t('occupancyDistribution') }}</h3>
            <span class="chart-badge">Donut</span>
          </div>
          <div class="chart-body"><canvas ref="distributionChartCanvas"></canvas></div>
        </div>

        <div class="chart-card">
          <div class="chart-header">
            <h3>{{ t('topStationsTraffic') }}</h3>
            <span class="chart-badge">Horizontal</span>
          </div>
          <div class="chart-body"><canvas ref="stationsChartCanvas"></canvas></div>
        </div>
      </div>

      <!-- Detalii autobuze live -->
      <div class="live-buses-section">
        <div class="section-header">
          <h3>🔴 {{ t('liveBuses') }}</h3>
          <span class="bus-count" v-if="liveBuses.length">{{ liveBuses.length }} active</span>
        </div>
        <div v-if="liveBuses.length === 0" class="empty-live">
          <div class="empty-icon">🚌</div>
          <p>Nu sunt autobuze active momentan</p>
        </div>
        <div v-else class="live-buses-grid">
          <div
            v-for="bus in liveBuses"
            :key="bus.id"
            class="live-bus-card"
            :class="getOccupancyClass(bus.occupancy)"
          >
            <div class="bus-header">
              <span class="bus-route">{{ bus.route }}</span>
              <span class="bus-status">● {{ t('live') }}</span>
            </div>
            <div class="bus-details">
              <div class="bus-stat">
                <span class="label">ID:</span>
                <span class="value">{{ bus.id }}</span>
              </div>
              <div class="bus-stat">
                <span class="label">{{ t('speed') }}:</span>
                <span class="value">{{ bus.speed }} km/h</span>
              </div>
              <div class="bus-stat">
                <span class="label">{{ t('occupancy') }}:</span>
                <span class="value occupancy-value">{{ bus.occupancy }}%</span>
              </div>
              <div class="bus-stat">
                <span class="label">{{ t('position') }}:</span>
                <span class="value coords">
                  {{ bus.latitude.toFixed(4) }}, {{ bus.longitude.toFixed(4) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Chart, registerables } from 'chart.js'
import { database } from '@/main'
import { ref as dbRef, onValue, off } from 'firebase/database'
import { useLanguage } from '@/composables/useLanguage'

Chart.register(...registerables)
const { t } = useLanguage()

interface Bus {
  id: string
  route: string
  latitude: number
  longitude: number
  speed: number
  heading: number
  occupancy: number
}

interface Statistics {
  activeBuses: number
  avgOccupancy: number
  totalRoutes: number
  totalStations: number
}

const isLoading = ref(true)
const stats = ref<Statistics>({
  activeBuses: 0,
  avgOccupancy: 0,
  totalRoutes: 0,
  totalStations: 0
})

const liveBuses = ref<Bus[]>([])

// Canvas refs pentru grafice
const occupancyChartCanvas = ref<HTMLCanvasElement>()
const busesChartCanvas = ref<HTMLCanvasElement>()
const distributionChartCanvas = ref<HTMLCanvasElement>()
const stationsChartCanvas = ref<HTMLCanvasElement>()

// Chart instances
let occupancyChart: Chart | null = null
let busesChart: Chart | null = null
let distributionChart: Chart | null = null
let stationsChart: Chart | null = null

const getOccupancyClass = (occupancy: number) => {
  if (occupancy < 40) return 'low'
  if (occupancy < 70) return 'medium'
  return 'high'
}

const loadStatistics = async () => {
  try {
    // Încarcă date din Firebase pentru autobuze active
    const busesRef = dbRef(database, 'buses')
    
    // Optimizare: actualizează doar o dată la 5 secunde
    let lastUpdate = 0
    onValue(busesRef, (snapshot) => {
      const now = Date.now()
      if (now - lastUpdate < 5000) return // Skip dacă < 5 secunde
      lastUpdate = now
      
      const data = snapshot.val()
      if (data) {
        const buses: Bus[] = Object.entries(data).map(([id, busData]: [string, any]) => ({
          id,
          route: busData.route || 'N/A',
          latitude: busData.latitude || 0,
          longitude: busData.longitude || 0,
          speed: busData.speed || 0,
          heading: busData.heading || 0,
          occupancy: busData.occupancy || 0
        }))

        liveBuses.value = buses
        stats.value.activeBuses = buses.length
        
        // Calculează ocupare medie
        const totalOccupancy = buses.reduce((sum, bus) => sum + bus.occupancy, 0)
        stats.value.avgOccupancy = buses.length > 0 
          ? Math.round(totalOccupancy / buses.length) 
          : 0

        // Update grafice doar dacă sunt diferențe semnificative
        updateCharts(buses)
      }
    })

    // Încarcă date statice din API
    const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
    console.log('📊 Loading routes and stations from API:', API_BASE_URL)
    
    const [routesRes, stationsRes] = await Promise.all([
      fetch(`${API_BASE_URL}/Routes`),
      fetch(`${API_BASE_URL}/Stations`)
    ])

    const routes = await routesRes.json()
    const stations = await stationsRes.json()

    stats.value.totalRoutes = routes.length
    stats.value.totalStations = stations.length
    
    console.log('✅ Statistics loaded:', {
      routes: routes.length,
      stations: stations.length,
      activeBuses: stats.value.activeBuses
    })

    isLoading.value = false
  } catch (error) {
    console.error('❌ Error loading statistics:', error)
    isLoading.value = false
  }
}

const updateCharts = (buses: Bus[]) => {
  if (!occupancyChartCanvas.value || !busesChartCanvas.value || 
      !distributionChartCanvas.value || !stationsChartCanvas.value) {
    return
  }

  // Grupează ocupare pe traseu
  const routeOccupancy = buses.reduce((acc, bus) => {
    if (!acc[bus.route]) {
      acc[bus.route] = { total: 0, count: 0 }
    }
    acc[bus.route]!.total += bus.occupancy
    acc[bus.route]!.count += 1
    return acc
  }, {} as Record<string, { total: number; count: number }>)

  const routes = Object.keys(routeOccupancy)
  const avgOccupancies = routes.map(route => 
    Math.round(routeOccupancy[route]!.total / routeOccupancy[route]!.count)
  )

  // Chart 1: Ocupare medie pe traseu
  if (occupancyChart) occupancyChart.destroy()
  occupancyChart = new Chart(occupancyChartCanvas.value, {
    type: 'bar',
    data: {
      labels: routes,
      datasets: [{
        label: t('occupancyAvgPercent'),
        data: avgOccupancies,
        backgroundColor: avgOccupancies.map(occ => {
          if (occ < 40) return 'rgba(34, 197, 94, 0.7)'
          if (occ < 70) return 'rgba(251, 191, 36, 0.7)'
          return 'rgba(239, 68, 68, 0.7)'
        }),
        borderColor: avgOccupancies.map(occ => {
          if (occ < 40) return 'rgb(34, 197, 94)'
          if (occ < 70) return 'rgb(251, 191, 36)'
          return 'rgb(239, 68, 68)'
        }),
        borderWidth: 2
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        y: {
          beginAtZero: true,
          max: 100,
          ticks: {
            callback: (value) => value + '%'
          }
        }
      }
    }
  })

  // Chart 2: Autobuze active pe traseu
  const busesPerRoute = buses.reduce((acc, bus) => {
    acc[bus.route] = (acc[bus.route] || 0) + 1
    return acc
  }, {} as Record<string, number>)

  if (busesChart) busesChart.destroy()
  busesChart = new Chart(busesChartCanvas.value, {
    type: 'line',
    data: {
      labels: Object.keys(busesPerRoute),
      datasets: [{
        label: t('activeBusesLabel'),
        data: Object.values(busesPerRoute),
        borderColor: 'rgb(59, 130, 246)',
        backgroundColor: 'rgba(59, 130, 246, 0.1)',
        tension: 0.4,
        fill: true
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        y: {
          beginAtZero: true,
          ticks: {
            stepSize: 1
          }
        }
      }
    }
  })

  // Chart 3: Distribuție nivel ocupare
  const lowOccupancy = buses.filter(b => b.occupancy < 40).length
  const mediumOccupancy = buses.filter(b => b.occupancy >= 40 && b.occupancy < 70).length
  const highOccupancy = buses.filter(b => b.occupancy >= 70).length

  if (distributionChart) distributionChart.destroy()
  distributionChart = new Chart(distributionChartCanvas.value, {
    type: 'doughnut',
    data: {
      labels: [t('occupancyLow'), t('occupancyMedium'), t('occupancyHigh')],
      datasets: [{
        data: [lowOccupancy, mediumOccupancy, highOccupancy],
        backgroundColor: [
          'rgba(34, 197, 94, 0.7)',
          'rgba(251, 191, 36, 0.7)',
          'rgba(239, 68, 68, 0.7)'
        ],
        borderColor: [
          'rgb(34, 197, 94)',
          'rgb(251, 191, 36)',
          'rgb(239, 68, 68)'
        ],
        borderWidth: 2
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false
    }
  })

  // Chart 4: Mock data pentru stații tranzitate
  if (stationsChart) stationsChart.destroy()
  stationsChart = new Chart(stationsChartCanvas.value, {
    type: 'horizontalBar' as any,
    data: {
      labels: ['Piața Mare', 'Gara CFR', 'Autogării', 'Strand', 'Kaufland'],
      datasets: [{
        label: t('passCount'),
        data: [156, 142, 128, 98, 87],
        backgroundColor: 'rgba(139, 92, 246, 0.7)',
        borderColor: 'rgb(139, 92, 246)',
        borderWidth: 2
      }]
    },
    options: {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          beginAtZero: true
        }
      }
    }
  })
}

onMounted(() => {
  loadStatistics()
})

onUnmounted(() => {
  // Cleanup Firebase listeners
  const busesRef = dbRef(database, 'buses')
  off(busesRef)

  // Destroy charts
  if (occupancyChart) occupancyChart.destroy()
  if (busesChart) busesChart.destroy()
  if (distributionChart) distributionChart.destroy()
  if (stationsChart) stationsChart.destroy()
})
</script>

<style scoped>
.analytics-dashboard {
  padding: 0 24px 24px;
  max-width: 1600px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 28px;
}

.page-header h2 {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0 0 4px 0;
}

.subtitle {
  font-size: 14px;
  color: var(--text-secondary);
  margin: 0;
}

.header-live {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: rgba(16, 185, 129, 0.1);
  border: 1px solid rgba(16, 185, 129, 0.2);
  border-radius: 20px;
  font-size: 13px;
  font-weight: 600;
  color: #10b981;
}

.live-dot {
  width: 8px;
  height: 8px;
  background: #10b981;
  border-radius: 50%;
  animation: pulse 2s infinite;
}

/* Loading */
.loading-state {
  text-align: center;
  padding: 80px 24px;
  color: var(--text-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--border-primary);
  border-top-color: var(--accent-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 16px;
}

@keyframes spin { to { transform: rotate(360deg); } }

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  position: relative;
  border-radius: 16px;
  padding: 24px 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  overflow: hidden;
  transition: transform 0.25s ease, box-shadow 0.25s ease;
  cursor: default;
}

.stat-card:hover {
  transform: translateY(-4px);
}

.stat-glow {
  position: absolute;
  inset: 0;
  opacity: 0.12;
  transition: opacity 0.3s;
}

.stat-card:hover .stat-glow { opacity: 0.2; }

.stat-buses { background: linear-gradient(135deg, rgba(59,130,246,0.15), rgba(59,130,246,0.05)); border: 1px solid rgba(59,130,246,0.2); }
.stat-buses .stat-glow { background: linear-gradient(135deg, #3b82f6, #60a5fa); }
.stat-buses .stat-icon-wrap { color: #3b82f6; background: rgba(59,130,246,0.15); }
.stat-buses:hover { box-shadow: 0 8px 32px rgba(59,130,246,0.2); }

.stat-occupancy { background: linear-gradient(135deg, rgba(139,92,246,0.15), rgba(139,92,246,0.05)); border: 1px solid rgba(139,92,246,0.2); }
.stat-occupancy .stat-glow { background: linear-gradient(135deg, #8b5cf6, #a78bfa); }
.stat-occupancy .stat-icon-wrap { color: #8b5cf6; background: rgba(139,92,246,0.15); }
.stat-occupancy:hover { box-shadow: 0 8px 32px rgba(139,92,246,0.2); }

.stat-routes { background: linear-gradient(135deg, rgba(16,185,129,0.15), rgba(16,185,129,0.05)); border: 1px solid rgba(16,185,129,0.2); }
.stat-routes .stat-glow { background: linear-gradient(135deg, #10b981, #34d399); }
.stat-routes .stat-icon-wrap { color: #10b981; background: rgba(16,185,129,0.15); }
.stat-routes:hover { box-shadow: 0 8px 32px rgba(16,185,129,0.2); }

.stat-stations { background: linear-gradient(135deg, rgba(245,158,11,0.15), rgba(245,158,11,0.05)); border: 1px solid rgba(245,158,11,0.2); }
.stat-stations .stat-glow { background: linear-gradient(135deg, #f59e0b, #fbbf24); }
.stat-stations .stat-icon-wrap { color: #f59e0b; background: rgba(245,158,11,0.15); }
.stat-stations:hover { box-shadow: 0 8px 32px rgba(245,158,11,0.2); }

.stat-icon-wrap {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.stat-icon-wrap svg {
  width: 24px;
  height: 24px;
}

.stat-content { flex: 1; position: relative; }

.stat-value {
  font-size: 32px;
  font-weight: 800;
  color: var(--text-primary);
  line-height: 1;
  margin-bottom: 4px;
  letter-spacing: -0.5px;
}

.stat-unit {
  font-size: 20px;
  font-weight: 600;
  opacity: 0.7;
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
  font-weight: 500;
}

/* Charts Grid */
.charts-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.chart-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 16px;
  overflow: hidden;
  transition: border-color 0.2s;
}

.chart-card:hover { border-color: var(--accent-primary); }

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 18px 20px 0;
}

.chart-header h3 {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
}

.chart-badge {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-secondary);
  background: var(--bg-secondary);
  padding: 3px 8px;
  border-radius: 6px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.chart-body {
  padding: 12px 16px 16px;
}

.chart-body canvas {
  max-height: 260px;
}

/* Live Buses */
.live-buses-section {
  background: var(--bg-primary);
  border: 1px solid var(--border-primary);
  border-radius: 16px;
  padding: 20px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-header h3 {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
}

.bus-count {
  font-size: 12px;
  font-weight: 600;
  color: #10b981;
  background: rgba(16,185,129,0.1);
  padding: 4px 10px;
  border-radius: 8px;
}

.empty-live {
  text-align: center;
  padding: 48px 20px;
  color: var(--text-secondary);
}

.empty-icon { font-size: 3rem; margin-bottom: 12px; opacity: 0.4; }
.empty-live p { font-size: 14px; margin: 0; }

.live-buses-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px;
}

.live-bus-card {
  background: var(--bg-secondary);
  border: 1px solid var(--border-primary);
  border-radius: 12px;
  padding: 14px;
  border-left: 3px solid var(--accent-primary);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.live-bus-card:hover {
  transform: translateX(2px);
  box-shadow: 0 4px 16px rgba(0,0,0,0.1);
}

.live-bus-card.low    { border-left-color: #10b981; }
.live-bus-card.medium { border-left-color: #f59e0b; }
.live-bus-card.high   { border-left-color: #ef4444; }

.bus-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.bus-route { font-size: 16px; font-weight: 700; color: var(--text-primary); }

.bus-status {
  font-size: 11px;
  color: #ef4444;
  font-weight: 600;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.bus-details { display: flex; flex-direction: column; gap: 6px; }

.bus-stat { display: flex; justify-content: space-between; font-size: 13px; }
.bus-stat .label { color: var(--text-secondary); font-weight: 500; }
.bus-stat .value { color: var(--text-primary); font-weight: 600; }
.occupancy-value { color: #8b5cf6; }
.coords { font-family: 'Courier New', monospace; font-size: 11px; }

@media (max-width: 1200px) {
  .stats-grid { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 768px) {
  .stats-grid { grid-template-columns: 1fr; }
  .charts-grid { grid-template-columns: 1fr; }
  .page-header { flex-direction: column; align-items: flex-start; gap: 12px; }
}
</style>
