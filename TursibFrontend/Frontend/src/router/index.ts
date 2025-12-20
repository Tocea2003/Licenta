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
      path: '/about',
      name: 'about',
      component: () => import('../views/AboutView.vue'),
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
  ],
})

// Navigation guard pentru rute protejate
router.beforeEach((to, from, next) => {
  if (to.meta.requiresAuth && !authService.isAuthenticated()) {
    next('/loginadmin')
  } else if (to.path === '/loginadmin' && authService.isAuthenticated()) {
    next('/admin/routes')
  } else {
    next()
  }
})

export default router
