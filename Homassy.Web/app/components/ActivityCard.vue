<template>
  <ActivityCardShell
    :user-public-id="activity.userPublicId"
    :activity-type="activity.activityType"
    :record-name="activity.recordName"
    :timestamp="activity.timestamp"
  >
    <!-- User Avatar: publicId switches on its own member ring, replacing the old flat
         ring-2 ring-gray-200 that used to sit here regardless of who the activity belonged to. -->
    <template #leading>
      <UserAvatar
        :src="userInfo?.profilePictureUrl"
        :name="userInfo?.displayName || userInfo?.name"
        :public-id="activity.userPublicId"
        :size="40"
      />
    </template>

    <template #meta>
      <p class="font-semibold text-gray-900 dark:text-white truncate">
        {{ userInfo?.displayName || userInfo?.name || 'Unknown User' }}
      </p>
    </template>

    <template #details>
      <!-- Quantity and unit (if available) -->
      <div v-if="activity.quantity != null && activity.unit != null" class="flex items-center gap-2">
        <UIcon name="i-lucide-scale" class="h-4 w-4 text-amber-600 dark:text-amber-400" />
        <p class="text-sm text-gray-700 dark:text-gray-300">
          {{ formatQuantity(activity.quantity) }} {{ $t(`enums.unit.${activity.unit}`) }}
        </p>
      </div>
    </template>
  </ActivityCardShell>
</template>

<script setup lang="ts">
import type { ActivityInfo } from '~/types/activity'
import type { UserInfo } from '~/types/user'

defineProps<{
  activity: ActivityInfo
  userInfo?: UserInfo
}>()

const { t: $t } = useI18n()

// Format quantity: show integers without decimals, otherwise max 2 decimal places
const formatQuantity = (quantity: number): string => {
  return Number.isInteger(quantity) ? quantity.toString() : quantity.toFixed(2)
}
</script>
