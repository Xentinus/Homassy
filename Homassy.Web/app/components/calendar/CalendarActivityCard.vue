<template>
  <div
    class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-4 py-3 border-l-4"
    :style="{ ...accentStyle(userPublicId, identityColor), borderLeftColor: 'var(--member-color)' }"
  >
    <div class="flex items-start justify-between gap-2">
      <span class="text-sm font-medium text-gray-900 dark:text-gray-100 leading-snug">
        {{ recordName }}
      </span>
      <span class="shrink-0 text-xs rounded px-1.5 py-0.5 leading-tight bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300">
        {{ t('enums.activityType.' + activityType) }}
      </span>
    </div>
    <div class="flex items-center gap-1.5 mt-0.5">
      <span class="text-xs text-gray-500 dark:text-gray-400">{{ userName }}</span>
      <span class="text-xs text-gray-400">·</span>
      <!-- Today's entries read as "5 minutes ago" and keep counting; older days
           fall back to the clock time, since the panel header already names the
           day and "3 days ago" would say less than "14:32". -->
      <RelativeTime
        :date="timestamp"
        absolute-format="time"
        :threshold="DAY_MS"
        class="text-xs text-gray-400 dark:text-gray-500"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ActivityType } from '~/types/activity'

withDefaults(defineProps<{
  activityType: ActivityType
  userName: string
  userPublicId: string
  /** Chosen palette key override for this member, or null/absent for the deterministic pick. */
  identityColor?: string | null
  recordName: string
  timestamp: string
}>(), {
  identityColor: null
})

const { t } = useI18n()
const { accentStyle } = useMemberColor()

const DAY_MS = 24 * 60 * 60 * 1000
</script>
