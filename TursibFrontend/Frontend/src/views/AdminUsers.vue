<template>
  <div class="users-management">
    <div class="page-header">
      <h2>{{ t('manageUsers') }}</h2>
      <div class="header-stats">
        <span class="stat-badge">{{ users.length }} {{ t('usersCount') }}</span>
        <span class="stat-badge admin">{{ adminCount }} {{ t('adminsCount') }}</span>
      </div>
    </div>

    <div class="filters-row">
      <input
        v-model="searchQuery"
        class="search-input"
        :placeholder="t('searchByUsername')"
      />
      <select v-model="roleFilter" class="role-filter">
        <option value="">{{ t('allRoles') }}</option>
        <option value="Admin">Admin</option>
        <option value="User">User</option>
      </select>
    </div>

    <div v-if="isLoading" class="loading">{{ t('loading') }}</div>

    <div v-else class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Username</th>
            <th>{{ t('roleCol') }}</th>
            <th>{{ t('registeredAt') }}</th>
            <th>{{ t('actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="user in paginatedUsers" :key="user.id">
            <td class="id-col">{{ user.id }}</td>
            <td>
              <div class="user-cell">
                <div class="user-avatar">{{ user.username.charAt(0).toUpperCase() }}</div>
                <span>{{ user.username }}</span>
              </div>
            </td>
            <td>
              <span :class="['role-badge', user.role.toLowerCase()]">
                {{ user.role }}
              </span>
            </td>
            <td class="date-col">{{ formatDate(user.createdAt) }}</td>
            <td>
              <div class="action-buttons">
                <button
                  @click="toggleRole(user)"
                  :class="['btn-action', user.role === 'Admin' ? 'btn-demote' : 'btn-promote']"
                  :title="user.role === 'Admin' ? t('demoteToUser') : t('promoteToAdmin')"
                >
                  {{ user.role === 'Admin' ? '⬇ User' : '⬆ Admin' }}
                </button>
                <button
                  @click="deleteUser(user)"
                  class="btn-action btn-delete"
                  :title="t('deleteUserLabel')"
                >
                  🗑️
                </button>
              </div>
            </td>
          </tr>
          <tr v-if="filteredUsers.length === 0">
            <td colspan="5" class="empty-row">{{ t('noUsersFound') }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="totalPages > 1" class="pagination">
        <button @click="prevPage" :disabled="currentPage === 1" class="page-btn">&laquo;</button>
        <button v-for="p in visiblePages" :key="p" @click="goToPage(p)" :class="['page-btn', { active: p === currentPage }]">{{ p }}</button>
        <button @click="nextPage" :disabled="currentPage === totalPages" class="page-btn">&raquo;</button>
        <span class="page-info">{{ filteredUsers.length }} / {{ users.length }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { adminUsersService, type AdminUser } from '@/services/adminService'
import { usePagination } from '@/composables/usePagination'
import { useLanguage } from '@/composables/useLanguage'

const { t } = useLanguage()

const users = ref<AdminUser[]>([])
const isLoading = ref(true)
const searchQuery = ref('')
const roleFilter = ref('')

const adminCount = computed(() => users.value.filter(u => u.role === 'Admin').length)

const filteredUsers = computed(() => {
  return users.value.filter(u => {
    const matchesSearch = u.username.toLowerCase().includes(searchQuery.value.toLowerCase())
    const matchesRole = !roleFilter.value || u.role === roleFilter.value
    return matchesSearch && matchesRole
  })
})

const { paginatedItems: paginatedUsers, currentPage, totalPages, visiblePages, goToPage, prevPage, nextPage, resetPage } = usePagination(filteredUsers, 10)
watch([searchQuery, roleFilter], () => resetPage())

const loadUsers = async () => {
  isLoading.value = true
  try {
    users.value = await adminUsersService.getUsers()
  } catch {
    alert(t('usersLoadError'))
  } finally {
    isLoading.value = false
  }
}

const toggleRole = async (user: AdminUser) => {
  const newRole = user.role === 'Admin' ? 'User' : 'Admin'
  const msg = newRole === 'Admin'
    ? t('confirmPromoteUser', '', { name: user.username })
    : t('confirmDemoteUser', '', { name: user.username })
  if (!confirm(msg)) return
  try {
    await adminUsersService.updateRole(user.id, newRole)
    await loadUsers()
  } catch {
    alert(t('roleUpdateError'))
  }
}

const deleteUser = async (user: AdminUser) => {
  if (!confirm(t('confirmDeleteUser', '', { name: user.username }))) return
  try {
    await adminUsersService.deleteUser(user.id)
    await loadUsers()
  } catch (err: any) {
    alert(err.response?.data?.message || t('userDeleteError'))
  }
}

const formatDate = (iso: string) => {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('ro-RO', { day: '2-digit', month: 'short', year: 'numeric' })
}

onMounted(loadUsers)
</script>

<style scoped>
.users-management {
  background: var(--bg-primary);
  border-radius: 12px;
  padding: 24px;
  box-shadow: var(--shadow-sm);
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.page-header h2 {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 0;
}

.header-stats {
  display: flex;
  gap: 8px;
}

.stat-badge {
  padding: 6px 14px;
  background: var(--bg-secondary);
  border-radius: 20px;
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
}

.stat-badge.admin {
  background: rgba(102, 126, 234, 0.15);
  color: #667eea;
}

.filters-row {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

.search-input {
  flex: 1;
  padding: 10px 14px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.search-input:focus {
  outline: none;
  border-color: #667eea;
}

.role-filter {
  padding: 10px 14px;
  border: 2px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  cursor: pointer;
}

.loading {
  text-align: center;
  padding: 40px;
  color: var(--text-secondary);
}

.table-container {
  overflow-x: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table thead {
  background: var(--bg-secondary);
}

.data-table th {
  padding: 12px 16px;
  text-align: left;
  font-weight: 600;
  color: var(--text-primary);
  border-bottom: 2px solid var(--border-color);
  font-size: 13px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.data-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color);
  color: var(--text-primary);
  font-size: 14px;
}

.id-col {
  color: var(--text-secondary);
  font-size: 12px;
  width: 50px;
}

.date-col {
  color: var(--text-secondary);
  font-size: 13px;
}

.user-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.user-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 13px;
  font-weight: 700;
  flex-shrink: 0;
}

.role-badge {
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.role-badge.admin {
  background: rgba(102, 126, 234, 0.15);
  color: #667eea;
}

.role-badge.user {
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
}

.action-buttons {
  display: flex;
  gap: 8px;
}

.btn-action {
  padding: 6px 12px;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 12px;
  font-weight: 600;
  transition: all 0.2s;
}

.btn-promote {
  background: rgba(102, 126, 234, 0.15);
  color: #667eea;
}

.btn-promote:hover {
  background: #667eea;
  color: #fff;
}

.btn-demote {
  background: rgba(251, 191, 36, 0.15);
  color: #d97706;
}

.btn-demote:hover {
  background: #fbbf24;
  color: #fff;
}

.btn-delete {
  background: rgba(239, 68, 68, 0.12);
  color: #ef4444;
}

.btn-delete:hover {
  background: #ef4444;
  color: #fff;
}

.empty-row {
  text-align: center;
  color: var(--text-secondary);
  padding: 40px;
}

.pagination { display: flex; align-items: center; justify-content: center; gap: 4px; margin-top: 16px; padding: 12px 0; }
.page-btn { min-width: 36px; height: 36px; border: 1px solid var(--border-color); background: var(--bg-primary); color: var(--text-primary); border-radius: 8px; cursor: pointer; font-size: 14px; font-weight: 500; transition: all 0.15s; }
.page-btn:hover:not(:disabled) { background: var(--bg-secondary); }
.page-btn.active { background: #3b82f6; color: white; border-color: #3b82f6; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.page-info { margin-left: 12px; font-size: 13px; color: var(--text-secondary); }
</style>
