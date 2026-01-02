<template>
  <div class="container mx-auto px-4 py-8">
    <!-- Page Header -->
    <div class="mb-8">
      <h1 class="text-3xl font-bold text-gray-900 dark:text-white mb-2">
        {{ $t('pages.activity.title') }}
      </h1>
      <p class="text-gray-600 dark:text-gray-400">
        {{ $t('pages.activity.description') }}
      </p>
    </div>

    <!-- Loading State (Initial) -->
    <div v-if="isInitialLoading" class="space-y-4">
      <div
        v-for="i in 5"
        :key="i"
        class="p-4 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800"
      >
        <div class="flex items-start gap-3 mb-3">
          <USkeleton class="h-10 w-10 rounded-full" />
          <div class="flex-1 space-y-2">
            <USkeleton class="h-4 w-32" />
            <USkeleton class="h-3 w-24" />
          </div>
        </div>
        <div class="ml-13 space-y-2">
          <USkeleton class="h-4 w-48" />
          <USkeleton class="h-3 w-36" />
        </div>
      </div>
    </div>

    <!-- Activities List -->
    <div v-else-if="activities.length > 0" class="space-y-4">
      <ActivityCard
        v-for="activity in activities"
        :key="activity.publicId"
        :activity="activity"
        :user-info="userCache.get(activity.userPublicId)"
      />

      <!-- Infinite Scroll Sentinel -->
      <div ref="sentinelRef" class="h-10">
        <!-- Loading More State -->
        <div v-if="isLoadingMore" class="space-y-4">
          <div
            v-for="i in 3"
            :key="i"
            class="p-4 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800"
          >
            <div class="flex items-start gap-3 mb-3">
              <USkeleton class="h-10 w-10 rounded-full" />
              <div class="flex-1 space-y-2">
                <USkeleton class="h-4 w-32" />
                <USkeleton class="h-3 w-24" />
              </div>
            </div>
            <div class="ml-13 space-y-2">
              <USkeleton class="h-4 w-48" />
              <USkeleton class="h-3 w-36" />
            </div>
          </div>
        </div>

        <!-- End of Activities -->
        <div
          v-else-if="!hasNextPage"
          class="text-center py-8 text-gray-500 dark:text-gray-400"
        >
          {{ $t('pages.activity.endOfActivities') }}
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div
      v-else
      class="text-center py-16"
    >
      <UIcon name="i-lucide-activity" class="h-16 w-16 mx-auto text-gray-400 mb-4" />
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2">
        {{ $t('pages.activity.noActivities') }}
      </h3>
      <p class="text-gray-600 dark:text-gray-400">
        {{ $t('pages.activity.noActivitiesDescription') }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import type { ActivityInfo } from '~/types/activity'
import type { UserInfo } from '~/types/user'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { t: $t } = useI18n()
const { getActivities, getUsersByPublicIds } = useUserApi()

// State
const activities = ref<ActivityInfo[]>([])
const userCache = ref<Map<string, UserInfo>>(new Map())
const isInitialLoading = ref(true)
const isLoadingMore = ref(false)
const currentPage = ref(1)
const hasNextPage = ref(true)
const sentinelRef = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

const PAGE_SIZE = 20

// Load activities for a specific page
const loadActivities = async (pageNumber: number) => {
  try {
    const response = await getActivities({
      pageNumber,
      pageSize: PAGE_SIZE
    })

    if (response?.data) {
      // Add new activities
      activities.value.push(...response.data.items)

      // Update pagination state
      hasNextPage.value = response.data.hasNextPage

      // Extract unique user publicIds we don't have cached yet
      const newUserPublicIds = new Set<string>()
      for (const activity of response.data.items) {
        if (!userCache.value.has(activity.userPublicId) && activity.userPublicId) {
          newUserPublicIds.add(activity.userPublicId)
        }
      }

      // Fetch user info for new users in bulk
      if (newUserPublicIds.size > 0) {
        await fetchUsers(Array.from(newUserPublicIds))
      }
    }
  } catch (error) {
    console.error('Failed to load activities:', error)
  }
}

// Fetch user information by user publicIds
const fetchUsers = async (userPublicIds: string[]) => {
  try {
    const response = await getUsersByPublicIds(userPublicIds)

    if (response?.data) {
      // Cache each user by their publicId
      for (let i = 0; i < userPublicIds.length; i++) {
        const publicId = userPublicIds[i]
        const userInfo = response.data[i]
        if (userInfo) {
          userCache.value.set(publicId, userInfo)
        }
      }
    }
  } catch (error) {
    console.error('Failed to fetch users:', error)
  }
}

// Initial load
const loadInitialActivities = async () => {
  isInitialLoading.value = true
  try {
    await loadActivities(1)
  } finally {
    isInitialLoading.value = false
  }
}

// Load more activities when sentinel is visible
const loadMoreActivities = async () => {
  if (isLoadingMore.value || !hasNextPage.value) return

  isLoadingMore.value = true
  try {
    currentPage.value++
    await loadActivities(currentPage.value)
  } finally {
    isLoadingMore.value = false
  }
}

// Intersection Observer callback
const handleIntersection = (entries: IntersectionObserverEntry[]) => {
  const entry = entries[0]
  if (entry?.isIntersecting && hasNextPage.value && !isLoadingMore.value) {
    loadMoreActivities()
  }
}

// Setup intersection observer for infinite scroll
const setupObserver = () => {
  if (!sentinelRef.value) return

  observer.value = new IntersectionObserver(handleIntersection, {
    root: null,
    rootMargin: '100px',
    threshold: 0
  })

  observer.value.observe(sentinelRef.value)
}

// Lifecycle hooks
onMounted(async () => {
  await loadInitialActivities()
  setupObserver()
})

onBeforeUnmount(() => {
  if (observer.value) {
    observer.value.disconnect()
  }
})
</script>
