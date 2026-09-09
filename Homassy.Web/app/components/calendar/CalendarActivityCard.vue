<template>
  <ActivityCardShell
    :user-public-id="userPublicId"
    :identity-color="identityColor"
    :activity-type="activityType"
    :record-name="recordName"
    :timestamp="timestamp"
    density="compact"
  >
    <template #meta>
      <span class="text-xs text-gray-500 dark:text-gray-400">{{ userName }}</span>
    </template>
  </ActivityCardShell>
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
</script>
