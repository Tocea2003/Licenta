<template>
  <div class="admin-dashboard">
    <aside class="admin-sidebar">
      <div class="sidebar-header">
        <span class="logo-mark" aria-hidden="true"><BusFront :size="18" /></span>
        <span class="logo-text">Tursib</span>
        <span class="logo-sub">/ Admin</span>
      </div>

      <nav class="sidebar-nav" :aria-label="t('adminPanel')">
        <router-link v-for="item in navItems" :key="item.to" :to="item.to" class="nav-item">
          <component :is="item.icon" :size="18" aria-hidden="true" />
          <span class="nav-text">{{ t(item.labelKey) }}</span>
        </router-link>
      </nav>

      <div class="sidebar-footer">
        <div class="user-profile">
          <span class="user-avatar" aria-hidden="true">{{ user?.username?.charAt(0).toUpperCase() }}</span>
          <span class="user-details">
            <span class="user-name">{{ user?.username }}</span>
            <span class="user-role">{{ user?.role }}</span>
          </span>
        </div>
        <button type="button" @click="handleLogout" class="btn-logout">
          <LogOut :size="16" aria-hidden="true" />{{ t('logout') }}
        </button>
      </div>
    </aside>

    <div class="admin-main">
      <header class="admin-header">
        <h1 class="page-title">{{ t('adminPanel') }}</h1>
        <p class="page-subtitle">{{ t('adminPanelSubtitle') }}</p>
      </header>

      <div class="admin-content">
        <router-view />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { BarChart3, BusFront, LogOut, MapPin, Route, Ticket, Users } from 'lucide-vue-next'

const navItems = [
  { to: '/admin/analytics', icon: BarChart3, labelKey: 'analytics' },
  { to: '/admin/routes', icon: Route, labelKey: 'routes' },
  { to: '/admin/stations', icon: MapPin, labelKey: 'stationsSection' },
  { to: '/admin/buses', icon: BusFront, labelKey: 'busFleet' },
  { to: '/admin/users', icon: Users, labelKey: 'users' },
  { to: '/admin/tickets', icon: Ticket, labelKey: 'ticketsAndRevenue' }
]
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { authService } from '@/services/adminService'
import { useLanguage } from '@/composables/useLanguage'

const router = useRouter()
const { t } = useLanguage()
const user = computed(() => authService.getUser())

const handleLogout = () => {
  authService.logout()
  router.push('/loginadmin')
}
</script>

<style scoped>
.admin-dashboard {
  display: flex;
  min-height: 100dvh;
  height: 100dvh;
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-family: var(--font-sans);
}

.admin-sidebar {
  width: 248px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: #16181d;
  color: #fff;
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 64px;
  padding: 0 20px;
}

.logo-mark {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: #fff;
  color: #16181d;
  display: flex;
  align-items: center;
  justify-content: center;
}

.logo-text {
  font-family: var(--font-display);
  font-weight: var(--fw-heavy);
  font-size: 20px;
  letter-spacing: -0.02em;
}

.logo-sub {
  font-size: 14px;
  color: #b9bcc4;
}

.sidebar-nav {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 12px;
  overflow-y: auto;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 44px;
  padding: 0 12px;
  border-radius: var(--radius-md);
  color: #d5d7dc;
  font-size: 14px;
  font-weight: var(--fw-semibold);
  text-decoration: none;
}

.nav-item:hover {
  background: #2b2e36;
  color: #fff;
  text-decoration: none;
}

.nav-item.router-link-active {
  background: #fff;
  color: #16181d;
}

.nav-item:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px rgba(138, 162, 245, 0.5);
}

.sidebar-footer {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 16px;
  border-top: 1px solid #2b2e36;
}

.user-profile {
  display: flex;
  align-items: center;
  gap: 10px;
}

.user-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #1e3fae;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: var(--fw-bold);
}

.user-details {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.user-name {
  font-weight: var(--fw-bold);
  font-size: 14px;
}

.user-role {
  font-size: 12px;
  color: #b9bcc4;
}

.btn-logout {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 40px;
  border: 1px solid #3a3e48;
  border-radius: var(--radius-md);
  background: transparent;
  color: #fff;
  font: inherit;
  font-size: 14px;
  font-weight: var(--fw-semibold);
  cursor: pointer;
}

.btn-logout:hover {
  background: #2b2e36;
}

.admin-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
}

.admin-header {
  padding: 24px 32px 8px;
}

.admin-header .page-subtitle {
  margin-top: 4px;
}

.admin-content {
  flex: 1;
  padding: 16px 32px 32px;
}

@media (max-width: 900px) {
  .admin-dashboard {
    flex-direction: column;
    height: auto;
  }

  .admin-sidebar {
    width: auto;
  }

  .sidebar-nav {
    flex-direction: row;
    overflow-x: auto;
    padding: 0 12px 12px;
  }

  .nav-item {
    flex-shrink: 0;
  }

  .sidebar-footer {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    padding: 10px 16px;
  }

  .admin-header {
    padding: 20px 16px 4px;
  }

  .admin-content {
    padding: 12px 16px 24px;
  }
}
</style>
