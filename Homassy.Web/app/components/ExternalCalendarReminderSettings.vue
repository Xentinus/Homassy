<template>
  <div class="space-y-2">
    <label class="block text-sm font-medium">{{ $t('profile.family.externalCalendars.reminders') }}</label>
    <div class="flex flex-wrap gap-2">
      <UButton
        v-for="minutes in REMINDER_LEAD_TIME_PRESETS"
        :key="minutes"
        size="xs"
        :color="leadTimes.includes(minutes) ? 'primary' : 'neutral'"
        :variant="leadTimes.includes(minutes) ? 'solid' : 'outline'"
        :aria-pressed="leadTimes.includes(minutes)"
        @click="toggle(minutes)"
      >
        {{ labelFor(minutes) }}
      </UButton>
    </div>
    <p class="text-xs text-muted">{{ $t('profile.family.externalCalendars.remindersHint') }}</p>

    <!-- An all-day event has no time of day of its own, so it only needs an anchor once reminders are on. -->
    <div v-if="leadTimes.length > 0" class="flex items-center gap-3 flex-wrap pt-1">
      <label class="text-sm font-medium">{{ $t('profile.family.externalCalendars.allDayNotifyTime') }}</label>
      <UInput
        :model-value="allDayNotifyTime"
        type="time"
        class="w-32"
        @update:model-value="(v) => emit('update:allDayNotifyTime', String(v))"
      />
      <p class="text-xs text-muted basis-full">{{ $t('profile.family.externalCalendars.allDayNotifyTimeHint') }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  leadTimes: number[]
  allDayNotifyTime: string
}>()

const emit = defineEmits<{
  'update:leadTimes': [value: number[]]
  'update:allDayNotifyTime': [value: string]
}>()

const { t: $t } = useI18n()

function labelFor(minutes: number): string {
  const { key, params } = reminderLeadTimeLabel(minutes)
  return $t(key, params)
}

// Reassigned rather than spliced so the parent's v-model always sees a new array.
function toggle(minutes: number) {
  const next = props.leadTimes.includes(minutes)
    ? props.leadTimes.filter(m => m !== minutes)
    : [...props.leadTimes, minutes].sort((a, b) => b - a)

  emit('update:leadTimes', next)
}
</script>
