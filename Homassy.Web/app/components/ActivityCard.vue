<template>
  <div
    class="p-4 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 hover:shadow-sm transition-shadow"
  >
    <!-- Header: User info and timestamp -->
    <div class="flex items-start gap-3 mb-3">
      <!-- User Avatar -->
      <div class="flex-shrink-0">
        <img
          v-if="userInfo?.profilePictureBase64"
          :src="`data:image/jpeg;base64,${userInfo.profilePictureBase64}`"
          :alt="userInfo.displayName || userInfo.name"
          class="h-10 w-10 rounded-full object-cover"
        >
        <div
          v-else
          class="h-10 w-10 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center"
        >
          <span class="text-primary-600 dark:text-primary-300 font-semibold text-sm">
            {{ getInitials(userInfo?.displayName || userInfo?.name || '?') }}
          </span>
        </div>
      </div>

      <!-- User name and timestamp -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center justify-between gap-2">
          <p class="font-semibold text-sm text-gray-900 dark:text-white truncate">
            {{ userInfo?.displayName || userInfo?.name || 'Unknown User' }}
          </p>
          <span class="text-xs text-gray-500 dark:text-gray-400 flex-shrink-0">
            {{ formatTimestamp(activity.timestamp) }}
          </span>
        </div>

        <!-- Activity type badge -->
        <div class="flex items-center gap-2 mt-1">
          <UBadge :color="getActivityColor(activity.activityType)" size="xs">
            {{ activity.activityTypeName }}
          </UBadge>
        </div>
      </div>
    </div>

    <!-- Activity details -->
    <div class="ml-13 space-y-1">
      <!-- Record name -->
      <div class="flex items-center gap-2">
        <UIcon :name="getActivityIcon(activity.activityType)" class="h-4 w-4 text-gray-400" />
        <p class="text-sm text-gray-700 dark:text-gray-300">
          {{ activity.recordName }}
        </p>
      </div>

      <!-- Family indicator if applicable -->
      <div v-if="activity.familyId" class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
        <UIcon name="i-lucide-users" class="h-3 w-3" />
        <span>{{ $t('common.familyActivity') }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ActivityInfo } from '~/types/activity'
import type { UserInfo } from '~/types/user'
import { ActivityType } from '~/types/activity'

const props = defineProps<{
  activity: ActivityInfo
  userInfo?: UserInfo
}>()

const { t: $t } = useI18n()

// Get user initials for avatar fallback
const getInitials = (name: string): string => {
  if (!name) return '?'
  const parts = name.trim().split(' ')
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase()
  }
  return name.substring(0, 2).toUpperCase()
}

// Format timestamp to relative time
const formatTimestamp = (timestamp: string): string => {
  const date = new Date(timestamp)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMs / 3600000)
  const diffDays = Math.floor(diffMs / 86400000)

  if (diffMins < 1) return $t('time.justNow')
  if (diffMins < 60) return $t('time.minutesAgo', { count: diffMins })
  if (diffHours < 24) return $t('time.hoursAgo', { count: diffHours })
  if (diffDays < 7) return $t('time.daysAgo', { count: diffDays })

  return date.toLocaleDateString()
}

// Get color based on activity type
const getActivityColor = (activityType: ActivityType): string => {
  if (activityType >= ActivityType.ProductCreate && activityType <= ActivityType.ProductPhotoDownloadFromOpenFoodFacts) {
    return 'blue'
  }
  if (activityType >= ActivityType.ProductInventoryCreate && activityType <= ActivityType.ProductInventoryDelete) {
    return 'green'
  }
  if (activityType >= ActivityType.ShoppingListCreate && activityType <= ActivityType.ShoppingListDelete) {
    return 'purple'
  }
  if (activityType >= ActivityType.ShoppingListItemAdd && activityType <= ActivityType.ShoppingListItemDelete) {
    return 'violet'
  }
  if (activityType >= ActivityType.FamilyCreate && activityType <= ActivityType.FamilyLeave) {
    return 'amber'
  }
  return 'gray'
}

// Get icon based on activity type
const getActivityIcon = (activityType: ActivityType): string => {
  if (activityType >= ActivityType.ProductCreate && activityType <= ActivityType.ProductPhotoDownloadFromOpenFoodFacts) {
    return 'i-lucide-package'
  }
  if (activityType >= ActivityType.ProductInventoryCreate && activityType <= ActivityType.ProductInventoryDelete) {
    return 'i-lucide-archive'
  }
  if (activityType >= ActivityType.ShoppingListCreate && activityType <= ActivityType.ShoppingListDelete) {
    return 'i-lucide-shopping-cart'
  }
  if (activityType >= ActivityType.ShoppingListItemAdd && activityType <= ActivityType.ShoppingListItemDelete) {
    return 'i-lucide-list'
  }
  if (activityType >= ActivityType.FamilyCreate && activityType <= ActivityType.FamilyLeave) {
    return 'i-lucide-users'
  }
  return 'i-lucide-activity'
}
</script>
