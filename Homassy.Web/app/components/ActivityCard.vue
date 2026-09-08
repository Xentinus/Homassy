<template>
  <div
    class="p-4 rounded-xl shadow-sm bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 hover:shadow-lg hover:-translate-y-0.5 transition-all duration-200"
  >
    <!-- Header: User info and timestamp -->
    <div class="flex items-start gap-3 mb-3">
      <!-- User Avatar -->
      <div class="flex-shrink-0">
        <UserAvatar
          :src="userInfo?.profilePictureUrl"
          :name="userInfo?.displayName || userInfo?.name"
          :size="40"
          class="ring-2 ring-gray-200 dark:ring-gray-700"
        />
      </div>

      <!-- User name and timestamp -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center justify-between gap-2">
          <p class="font-semibold text-gray-900 dark:text-white truncate">
            {{ userInfo?.displayName || userInfo?.name || 'Unknown User' }}
          </p>
          <RelativeTime
            :date="activity.timestamp"
            class="text-xs text-gray-500 dark:text-gray-500 flex-shrink-0"
          />
        </div>

        <!-- Activity type badge -->
        <div class="flex items-center gap-2 mt-1">
          <span class="text-sm text-gray-600 dark:text-gray-400">
            {{ $t(`enums.activityType.${activity.activityType}`) }}
          </span>
        </div>
      </div>
    </div>

    <!-- Activity details -->
    <div class="ml-13 space-y-2">
      <!-- Record name -->
      <div class="flex items-center gap-2">
        <UIcon :name="getActivityIcon(activity.activityType)" class="h-4 w-4" :class="getActivityIconColor(activity.activityType)" />
        <p class="text-sm font-medium text-gray-900 dark:text-white">
          {{ activity.recordName }}
        </p>
      </div>

      <!-- Quantity and unit (if available) -->
      <div v-if="activity.quantity != null && activity.unit != null" class="flex items-center gap-2">
        <UIcon name="i-lucide-scale" class="h-4 w-4 text-amber-600 dark:text-amber-400" />
        <p class="text-sm text-gray-700 dark:text-gray-300">
          {{ formatQuantity(activity.quantity) }} {{ $t(`enums.unit.${activity.unit}`) }}
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ActivityInfo } from '~/types/activity'
import type { UserInfo } from '~/types/user'
import { ActivityType } from '~/types/activity'

defineProps<{
  activity: ActivityInfo
  userInfo?: UserInfo
}>()

const { t: $t } = useI18n()

// Format quantity: show integers without decimals, otherwise max 2 decimal places
const formatQuantity = (quantity: number): string => {
  return Number.isInteger(quantity) ? quantity.toString() : quantity.toFixed(2)
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
  if ((activityType >= ActivityType.ShoppingListItemAdd && activityType <= ActivityType.ShoppingListItemDelete) ||
      activityType === ActivityType.ShoppingListItemQuickPurchase ||
      activityType === ActivityType.ShoppingListItemRestorePurchase) {
    return 'i-lucide-list'
  }
  if ((activityType >= ActivityType.FamilyCreate && activityType <= ActivityType.FamilyLeave) ||
      (activityType >= ActivityType.FamilyJoinRequestCreate && activityType <= ActivityType.FamilyJoinRequestDecline)) {
    return 'i-lucide-users'
  }
  return 'i-lucide-activity'
}

// Get semantic icon color for activity type
const getActivityIconColor = (activityType: ActivityType): string => {
  if (activityType >= ActivityType.ProductCreate && activityType <= ActivityType.ProductPhotoDownloadFromOpenFoodFacts) {
    return 'text-primary-600 dark:text-primary-400'
  }
  if (activityType >= ActivityType.ProductInventoryCreate && activityType <= ActivityType.ProductInventoryDelete) {
    return 'text-amber-600 dark:text-amber-400'
  }
  if (activityType >= ActivityType.ShoppingListCreate && activityType <= ActivityType.ShoppingListDelete) {
    return 'text-pink-600 dark:text-pink-400'
  }
  if ((activityType >= ActivityType.ShoppingListItemAdd && activityType <= ActivityType.ShoppingListItemDelete) ||
      activityType === ActivityType.ShoppingListItemQuickPurchase ||
      activityType === ActivityType.ShoppingListItemRestorePurchase) {
    return 'text-pink-600 dark:text-pink-400'
  }
  if ((activityType >= ActivityType.FamilyCreate && activityType <= ActivityType.FamilyLeave) ||
      (activityType >= ActivityType.FamilyJoinRequestCreate && activityType <= ActivityType.FamilyJoinRequestDecline)) {
    return 'text-primary-600 dark:text-primary-400'
  }
  return 'text-gray-600 dark:text-gray-400'
}
</script>
