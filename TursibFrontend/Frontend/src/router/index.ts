import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import { authService } from '@/services/adminService'

// Lazy load pentru rute admin (nu sunt necesare imediat)
const AdminLogin = () => import('../views/AdminLogin.vue')
const AdminDashboard = () => import('../views/AdminDashboard.vue')
const AdminAnalytics = () => import('../views/AdminAnalytics.vue')
const AdminRoutes = () => import('../views/AdminRoutes.vue')
const AdminStations = () => import('../views/AdminStations.vue')

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
    },
    {
      path: '/trip-planner',
      name: 'tripPlanner',
      component: () => import('../views/TripPlannerView.vue'),
    },
    {
      path: '/favorites',
      name: 'favorites',
      component: () => import('../views/FavoritesView.vue'),
    },
    {
      path: '/station/:id',
      name: 'stationDetails',
      component: () => import('../views/StationDetailsView.vue'),
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('../views/SettingsView.vue'),
    },
    {
      path: '/statistics',
      name: 'statistics',
      component: () => import('../views/StatisticsView.vue'),
    },
    {
      path: '/about',
      name: 'about',
      component: () => import('../views/AboutView.vue'),
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/Login.vue'),
    },
    {
      path: '/signup',
      name: 'signup',
      component: () => import('../views/SignUp.vue'),
    },
    {
      path: '/loginadmin',
      name: 'admin-login',
      component: AdminLogin,
    },
    {
      path: '/admin',
      component: AdminDashboard,
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          redirect: '/admin/analytics'
        },
        {
          path: 'analytics',
          name: 'admin-analytics',
          component: AdminAnalytics,
        },
        {
          path: 'routes',
          name: 'admin-routes',
          component: AdminRoutes,
        },
        {
          path: 'stations',
          name: 'admin-stations',
          component: AdminStations,
        },
      ]
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'notFound',
      component: () => import('../views/NotFound404.vue'),
    },
  ],
})

// Navigation guard pentru rute protejate
router.beforeEach((to, from, next) => {
  console.log('🔀 Navigation:', from.path, '→', to.path)
  
  if (to.meta.requiresAuth) {
    const isAuth = authService.isAuthenticated()
    const user = authService.getUser()
    
    console.log('🔐 Auth check:', { isAuth, user })
    
    if (!isAuth) {
      console.warn('⚠️ Not authenticated - redirecting to login')
      next('/loginadmin')
    } else if (user?.role !== 'admin' && user?.role !== 'Admin') {
      console.warn('⚠️ Not admin - access denied')
      alert('Acces interzis. Ai nevoie de rol de administrator.')
      next('/')
    } else {
      console.log('✅ Access granted')
      next()
    }
  } else if (to.path === '/loginadmin' && authService.isAuthenticated()) {
    console.log('🔄 Already authenticated - redirecting to admin')
    next('/admin/routes')
  } else {
    next()
  }
})

export default router
