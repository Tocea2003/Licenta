import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import { authService } from '@/services/adminService'

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
      // route level code-splitting
      // this generates a separate chunk (About.[hash].js) for this route
      // which is lazy-loaded when the route is visited.
      component: () => import('../views/AboutView.vue'),
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
          redirect: '/admin/routes'
        },
        {
          path: 'routes',
          name: 'admin-routes',
          component: () => import('../views/AdminRoutes.vue'),
        },
        {
          path: 'stations',
          name: 'admin-stations',
          component: () => import('../views/AdminRoutes.vue'), // Temporary - folosim AdminRoutes deocamdată
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
