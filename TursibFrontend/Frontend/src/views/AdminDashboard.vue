<template>
  <div class="admin-dashboard">
    <!-- Sidebar Navigation -->
    <aside class="admin-sidebar">
      <div class="sidebar-header">
        <div class="logo">
          <span class="logo-icon">🚌</span>
          <span class="logo-text">Tursib Admin</span>
        </div>
      </div>
      
      <nav class="sidebar-nav">
        <router-link to="/admin/routes" class="nav-item">
          <span class="nav-icon">🗺️</span>
          <span class="nav-text">Trasee</span>
        </router-link>
        <router-link to="/admin/stations" class="nav-item">
          <span class="nav-icon">📍</span>
          <span class="nav-text">Stații</span>
        </router-link>
      </nav>

      <div class="sidebar-footer">
        <div class="user-profile">
          <div class="user-avatar">
            {{ user?.username?.charAt(0).toUpperCase() }}
          </div>
          <div class="user-details">
            <div class="user-name">{{ user?.username }}</div>
            <div class="user-role">{{ user?.role }}</div>
          </div>
        </div>
        <button @click="handleLogout" class="btn-logout">
          <span>🚪</span> Logout
        </button>
      </div>
    </aside>

    <!-- Main Content -->
    <div class="admin-main">
      <header class="admin-header">
        <h1 class="page-title">Panou de Administrare</h1>
        <p class="page-subtitle">Gestionați traseele și stațiile de transport public Sibiu</p>
      </header>

      <div class="admin-content">
        <router-view />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { authService } from '@/services/adminService'

const router = useRouter()
const user = computed(() => authService.getUser())

const handleLogout = () => {
  authService.logout()
  router.push('/loginadmin')
}
</script>

<style scoped>
.admin-dashboard {
  display: flex;
  min-height: 100vh;
  background: #f8fafc;
}

/* Sidebar */
.admin-sidebar {
  width: 280px;
  background: linear-gradient(180deg, #1e293b 0%, #0f172a 100%);
  display: flex;
  flex-direction: column;
  box-shadow: 4px 0 24px rgba(0, 0, 0, 0.12);
  position: fixed;
  height: 100vh;
  left: 0;
  top: 0;
  z-index: 100;
}

.sidebar-header {
  padding: 32px 24px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.logo {
  display: flex;
  align-items: center;
  gap: 12px;
}

.logo-icon {
  font-size: 32px;
  filter: drop-shadow(0 2px 8px rgba(255, 255, 255, 0.2));
}

.logo-text {
  font-size: 20px;
  font-weight: 700;
  color: #fff;
  letter-spacing: -0.5px;
}

.sidebar-nav {
  flex: 1;
  padding: 24px 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px;
  color: #cbd5e1;
  text-decoration: none;
  border-radius: 12px;
  font-size: 15px;
  font-weight: 500;
  transition: all 0.2s;
  position: relative;
}

.nav-item:hover {
  background: rgba(255, 255, 255, 0.08);
  color: #fff;
  transform: translateX(4px);
}

.nav-item.router-link-active {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.nav-icon {
  font-size: 20px;
}

.nav-text {
  font-weight: 600;
}

.sidebar-footer {
  padding: 24px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(0, 0, 0, 0.2);
}

.user-profile {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 12px;
  margin-bottom: 16px;
}

.user-avatar {
  width: 44px;
  height: 44px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 18px;
  font-weight: 700;
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.3);
}

.user-details {
  flex: 1;
}

.user-name {
  font-size: 15px;
  font-weight: 600;
  color: #fff;
  margin-bottom: 2px;
}

.user-role {
  font-size: 12px;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.btn-logout {
  width: 100%;
  padding: 12px;
  background: rgba(239, 68, 68, 0.15);
  color: #fca5a5;
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.btn-logout:hover {
  background: rgba(239, 68, 68, 0.25);
  border-color: rgba(239, 68, 68, 0.5);
  color: #fff;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(239, 68, 68, 0.3);
}

/* Main Content */
.admin-main {
  flex: 1;
  margin-left: 280px;
  display: flex;
  flex-direction: column;
}

.admin-header {
  background: linear-gradient(135deg, #f8fafc 0%, #e2e8f0 100%);
  padding: 48px 48px 64px;
  border-bottom: 3px solid #667eea;
  box-shadow: 0 4px 24px rgba(102, 126, 234, 0.15);
  position: relative;
  overflow: hidden;
}

.admin-header::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 6px;
  background: linear-gradient(90deg, #667eea 0%, #764ba2 50%, #667eea 100%);
  background-size: 200% 100%;
  animation: shimmer 3s linear infinite;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.page-title {
  font-size: 36px;
  font-weight: 800;
  color: #1e293b;
  margin: 0 0 8px 0;
  letter-spacing: -1px;
  text-shadow: none;
}

.page-subtitle {
  font-size: 16px;
  color: #475569;
  margin: 0;
  font-weight: 500;
  text-shadow: none;
}

.admin-content {
  flex: 1;
  padding: 32px 48px;
  margin-top: -32px;
}

/* Responsive */
@media (max-width: 1024px) {
  .admin-sidebar {
    width: 240px;
  }
  
  .admin-main {
    margin-left: 240px;
  }
}

@media (max-width: 768px) {
  .admin-sidebar {
    width: 80px;
  }
  
  .sidebar-header,
  .user-details,
  .nav-text {
    display: none;
  }
  
  .logo-icon {
    font-size: 28px;
  }
  
  .nav-item {
    justify-content: center;
  }
  
  .user-profile {
    justify-content: center;
  }
  
  .btn-logout span:last-child {
    display: none;
  }
  
  .admin-main {
    margin-left: 80px;
  }
}
</style>
