<template>
  <div class="analytics-dashboard">
    <div class="page-header">
      <h2>📊 Dashboard Analytics</h2>
      <p class="subtitle">Statistici în timp real pentru sistemul de transport public</p>
    </div>

    <div v-if="isLoading" class="loading">Se încarcă datele...</div>

    <div v-else class="analytics-content">
      <!-- Statistici rapide -->
      <div class="stats-grid">
        <div class="stat-card active-buses">
          <div class="stat-icon">🚌</div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.activeBuses }}</div>
            <div class="stat-label">Autobuze Active</div>
          </div>
        </div>

        <div class="stat-card avg-occupancy">
          <div class="stat-icon">👥</div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.avgOccupancy }}%</div>
            <div class="stat-label">Ocupare Medie</div>
          </div>
        </div>

        <div class="stat-card total-routes">
          <div class="stat-icon">🗺️</div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.totalRoutes }}</div>
            <div class="stat-label">Trasee Totale</div>
          </div>
        </div>

        <div class="stat-card total-stations">
          <div class="stat-icon">📍</div>
          <div class="stat-content">
            <div class="stat-value">{{ stats.totalStations }}</div>
            <div class="stat-label">Stații Totale</div>
          </div>
        </div>
      </div>

      <!-- Grafice -->
      <div class="charts-grid">
        <!-- Ocupare pe traseu -->
        <div class="chart-card">
          <h3>Ocupare Medie pe Traseu</h3>
          <canvas ref="occupancyChartCanvas"></canvas>
        </div>

        <!-- Autobuze active pe oră -->
        <div class="chart-card">
          <h3>Autobuze Active în Timp Real</h3>
          <canvas ref="busesChartCanvas"></canvas>
        </div>

        <!-- Distribuție ocupare -->
        <div class="chart-card">
          <h3>Distribuție Nivel Ocupare</h3>
          <canvas ref="distributionChartCanvas"></canvas>
        </div>

        <!-- Stații cu trafic mare -->
        <div class="chart-card">
          <h3>Top Stații Tranzitate</h3>
          <canvas ref="stationsChartCanvas"></canvas>
        </div>
      </div>

      <!-- Detalii autobuze live -->
      <div class="live-buses-section">
        <h3>🔴 Autobuze Live</h3>
        <div class="live-buses-grid">
          <div 
            v-for="bus in liveBuses" 
            :key="bus.id"
            class="live-bus-card"
            :class="getOccupancyClass(bus.occupancy)"
          >
            <div class="bus-header">
              <span class="bus-route">{{ bus.route }}</span>
              <span class="bus-status">● LIVE</span>
            </div>
            <div class="bus-details">
              <div class="bus-stat">
                <span class="label">ID:</span>
                <span class="value">{{ bus.id }}</span>
              </div>
              <div class="bus-stat">
                <span class="label">Viteză:</span>
                <span class="value">{{ bus.speed }} km/h</span>
              </div>
              <div class="bus-stat">
                <span class="label">Ocupare:</span>
                <span class="value occupancy-value">{{ bus.occupancy }}%</span>
              </div>
              <div class="bus-stat">
                <span class="label">Poziție:</span>
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

Chart.register(...registerables)

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
    const [routesRes, stationsRes] = await Promise.all([
      fetch('http://localhost:5022/api/Routes'),
      fetch('http://localhost:5022/api/Stations')
    ])

    const routes = await routesRes.json()
    const stations = await stationsRes.json()

    stats.value.totalRoutes = routes.length
    stats.value.totalStations = stations.length

    isLoading.value = false
  } catch (error) {
    console.error('Error loading statistics:', error)
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
        label: 'Ocupare Medie (%)',
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
        label: 'Autobuze Active',
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
      labels: ['Scăzută (<40%)', 'Medie (40-70%)', 'Ridicată (>70%)'],
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
        label: 'Număr treceri',
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
  padding: 24px;
  max-width: 1600px;
  margin: 0 auto;
}

.page-header {
  margin-bottom: 32px;
}

.page-header h2 {
  font-size: 32px;
  font-weight: 700;
  color: #1e293b;
  margin-bottom: 8px;
}

.subtitle {
  font-size: 16px;
  color: #64748b;
}

.loading {
  text-align: center;
  padding: 48px;
  font-size: 18px;
  color: #64748b;
}

/* Stats Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 20px;
  margin-bottom: 32px;
}

.stat-card {
  background: white;
  border-radius: 16px;
  padding: 24px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  transition: transform 0.2s, box-shadow 0.2s;
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}

.stat-icon {
  font-size: 48px;
  line-height: 1;
}

.stat-content {
  flex: 1;
}

.stat-value {
  font-size: 36px;
  font-weight: 700;
  color: #1e293b;
  line-height: 1;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 14px;
  color: #64748b;
  font-weight: 500;
}

.active-buses {
  border-left: 4px solid #3b82f6;
}

.avg-occupancy {
  border-left: 4px solid #8b5cf6;
}

.total-routes {
  border-left: 4px solid #10b981;
}

.total-stations {
  border-left: 4px solid #f59e0b;
}

/* Charts Grid */
.charts-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(450px, 1fr));
  gap: 24px;
  margin-bottom: 32px;
}

.chart-card {
  background: white;
  border-radius: 16px;
  padding: 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.chart-card h3 {
  font-size: 18px;
  font-weight: 600;
  color: #1e293b;
  margin-bottom: 20px;
}

.chart-card canvas {
  max-height: 300px;
}

/* Live Buses */
.live-buses-section {
  background: white;
  border-radius: 16px;
  padding: 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.live-buses-section h3 {
  font-size: 20px;
  font-weight: 600;
  color: #1e293b;
  margin-bottom: 20px;
}

.live-buses-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.live-bus-card {
  background: #f8fafc;
  border-radius: 12px;
  padding: 16px;
  border-left: 4px solid #3b82f6;
  transition: transform 0.2s;
}

.live-bus-card:hover {
  transform: translateX(4px);
}

.live-bus-card.low {
  border-left-color: #22c55e;
}

.live-bus-card.medium {
  border-left-color: #fbbf24;
}

.live-bus-card.high {
  border-left-color: #ef4444;
}

.bus-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.bus-route {
  font-size: 18px;
  font-weight: 700;
  color: #1e293b;
}

.bus-status {
  font-size: 12px;
  color: #ef4444;
  font-weight: 600;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.bus-details {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.bus-stat {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
}

.bus-stat .label {
  color: #64748b;
  font-weight: 500;
}

.bus-stat .value {
  color: #1e293b;
  font-weight: 600;
}

.occupancy-value {
  color: #8b5cf6;
}

.coords {
  font-family: 'Courier New', monospace;
  font-size: 12px;
}

@media (max-width: 768px) {
  .stats-grid,
  .charts-grid {
    grid-template-columns: 1fr;
  }

  .live-buses-grid {
    grid-template-columns: 1fr;
  }
}
</style>
