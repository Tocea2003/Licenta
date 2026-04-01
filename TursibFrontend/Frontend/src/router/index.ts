import { createRouter, createWebHistory } from 'vue-router'
import { authService } from '@/services/adminService'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('../views/HomeView.vue'),
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
      component: () => import('../views/AdminLogin.vue'),
    },
    {
      path: '/admin',
      component: () => import('../views/AdminDashboard.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          redirect: '/admin/analytics'
        },
        {
          path: 'analytics',
          name: 'admin-analytics',
          component: () => import('../views/AdminAnalytics.vue'),
        },
        {
          path: 'routes',
          name: 'admin-routes',
          component: () => import('../views/AdminRoutes.vue'),
        },
        {
          path: 'stations',
          name: 'admin-stations',
          component: () => import('../views/AdminStations.vue'),
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

router.beforeEach((to, from, next) => {
  if (to.meta.requiresAuth) {
    const isAuth = authService.isAuthenticated()
    const user = authService.getUser()
    if (!isAuth) {
      next('/loginadmin')
    } else if (user?.role !== 'admin' && user?.role !== 'Admin') {
      alert('Acces interzis. Ai nevoie de rol de administrator.')
      next('/')
    } else {
      next()
    }
  } else if (to.path === '/loginadmin' && authService.isAuthenticated()) {
    next('/admin/routes')
  } else {
    next()
  }
})

export default router
