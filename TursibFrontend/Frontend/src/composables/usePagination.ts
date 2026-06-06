import { ref, computed, type Ref } from 'vue'

export function usePagination<T>(items: Ref<T[]>, defaultPerPage = 10) {
  const currentPage = ref(1)
  const perPage = ref(defaultPerPage)

  const totalPages = computed(() => Math.max(1, Math.ceil(items.value.length / perPage.value)))

  const paginatedItems = computed(() => {
    const start = (currentPage.value - 1) * perPage.value
    return items.value.slice(start, start + perPage.value)
  })

  const goToPage = (page: number) => {
    currentPage.value = Math.max(1, Math.min(page, totalPages.value))
  }

  const nextPage = () => goToPage(currentPage.value + 1)
  const prevPage = () => goToPage(currentPage.value - 1)

  const visiblePages = computed(() => {
    const total = totalPages.value
    const current = currentPage.value
    const pages: number[] = []
    const start = Math.max(1, current - 2)
    const end = Math.min(total, current + 2)
    for (let i = start; i <= end; i++) pages.push(i)
    return pages
  })

  // Reset to page 1 when items change drastically
  const resetPage = () => { currentPage.value = 1 }

  return {
    currentPage,
    perPage,
    totalPages,
    paginatedItems,
    goToPage,
    nextPage,
    prevPage,
    visiblePages,
    resetPage,
  }
}
