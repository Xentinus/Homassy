<template>
  <div
    class="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-4 py-3 border-l-4 border-l-gray-400 dark:border-l-gray-500"
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
      <span class="text-xs text-gray-400 dark:text-gray-500">{{ formattedTime }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ActivityType } from '~/types/activity'

const props = defineProps<{
  activityType: ActivityType
  userName: string
  recordName: string
  timestamp: string
}>()

const { t, locale } = useI18n()

const formattedTime = computed(() =>
  new Date(props.timestamp).toLocaleTimeString(locale.value, {
    hour: '2-digit',
    minute: '2-digit'
  })
)
</script>
