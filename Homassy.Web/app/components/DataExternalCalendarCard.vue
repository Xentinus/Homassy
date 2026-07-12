<template>
  <div class="relative rounded-2xl overflow-hidden card-animate" style="touch-action: pan-y" data-no-pull-refresh>
    <!-- Swipe action layer -->
    <div
      v-show="swipe.isSwiping.value"
      aria-hidden="true"
      class="absolute inset-0 rounded-2xl flex items-center justify-between px-4"
      :class="swipe.direction.value === 'left' ? 'bg-error-500 dark:bg-error-600' : 'bg-primary-500 dark:bg-primary-600'"
    >
      <UIcon name="i-lucide-pencil" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'right' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
      <UIcon name="i-lucide-trash-2" class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'left' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']" />
    </div>

    <!-- Card surface -->
    <div
      ref="cardEl"
      class="relative h-full bg-default rounded-2xl border-2 border-gray-200 dark:border-gray-700 p-3 cursor-pointer shadow-sm hover:shadow-lg transition-shadow duration-200 flex items-start gap-3 overflow-hidden select-none"
      :class="{ 'opacity-70': !calendar.isEnabled }"
      :style="swipe.cardStyle.value"
      @click="handleCardClick"
    >
      <span class="mt-1 h-3 w-3 rounded-full flex-shrink-0" :style="{ backgroundColor: calendar.color }" />

      <div class="min-w-0 flex-1">
        <div class="flex items-center gap-2">
          <span class="text-sm font-bold text-highlighted truncate">{{ calendar.name }}</span>
          <span v-if="!calendar.isEnabled" class="text-xs px-1.5 py-0.5 rounded-full bg-gray-100 dark:bg-gray-700 text-muted flex-shrink-0">
            {{ $t('profile.family.externalCalendars.disabled') }}
          </span>
        </div>
        <p class="text-xs text-muted truncate mt-0.5 font-mono">{{ calendar.iCalUrl }}</p>
        <div class="flex items-center gap-2 mt-1 text-xs text-toned">
          <UIcon name="i-lucide-refresh-cw" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400 flex-shrink-0" />
          <span v-if="calendar.lastSyncedAt">{{ $t('profile.family.externalCalendars.lastSync') }}: {{ formatTimestamp(calendar.lastSyncedAt) }}</span>
          <span v-else>{{ $t('profile.family.externalCalendars.neverSynced') }}</span>
          <span>·</span>
          <span>{{ $t('profile.family.externalCalendars.eventCount', { count: calendar.eventCount }) }}</span>
        </div>
        <div v-if="calendar.lastSyncError" class="mt-1 text-xs text-red-500 flex items-center gap-1">
          <UIcon name="i-lucide-alert-circle" class="h-3.5 w-3.5 flex-shrink-0" />
          <span class="truncate">{{ calendar.lastSyncError }}</span>
        </div>
      </div>

      <UButton
        size="xs"
        variant="ghost"
        color="neutral"
        icon="i-lucide-refresh-cw"
        class="flex-shrink-0"
        :loading="syncing"
        :disabled="syncing"
        :title="$t('profile.family.externalCalendars.sync')"
        @click.stop="emit('sync', calendar)"
      />
    </div>

    <!-- Delete confirmation -->
    <UModal :open="isDeleteModalOpen" :dismissible="false" @update:open="(v) => { isDeleteModalOpen = v }">
      <template #title>{{ $t('profile.family.externalCalendars.deleteTitle') }}</template>
      <template #description>{{ $t('profile.family.externalCalendars.deleteWarning') }}</template>
      <template #body>
        <div>
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ $t('common.name') }}:</span>
          <span class="text-sm ml-2">{{ calendar.name }}</span>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton :label="$t('common.cancel')" color="neutral" variant="outline" @click="() => { isDeleteModalOpen = false }" />
          <UButton :label="$t('common.delete')" color="error" @click="confirmDelete" />
        </div>
      </template>
    </UModal>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import type { ExternalCalendarResponse } from '~/types/externalCalendar'

const props = withDefaults(defineProps<{
  calendar: ExternalCalendarResponse
  syncing?: boolean
}>(), {
  syncing: false
})

const emit = defineEmits<{
  edit: [calendar: ExternalCalendarResponse]
  sync: [calendar: ExternalCalendarResponse]
  deleted: [publicId: string]
}>()

const { t } = useI18n()

const isDeleteModalOpen = ref(false)

const cardEl = ref<HTMLElement | null>(null)
const swipe = useSwipeActions(cardEl, {
  onSwipeLeft: () => { isDeleteModalOpen.value = true },
  onSwipeRight: () => emit('edit', props.calendar),
  disabled: () => isDeleteModalOpen.value
})

function handleCardClick(event: MouseEvent) {
  if (swipe.suppressClick.value) return
  const target = event.target as HTMLElement
  if (target.closest('a, button')) return
  emit('edit', props.calendar)
}

function confirmDelete() {
  isDeleteModalOpen.value = false
  emit('deleted', props.calendar.publicId)
}

// Relative "time ago" formatter, matching the previous inline row.
const formatTimestamp = (timestamp: string): string => {
  const date = new Date(timestamp)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / 60000)
  const diffHours = Math.floor(diffMs / 3600000)
  const diffDays = Math.floor(diffMs / 86400000)

  if (diffMins < 1) return t('time.justNow')
  if (diffMins < 60) return t('time.minutesAgo', { count: diffMins })
  if (diffHours < 24) return t('time.hoursAgo', { count: diffHours })
  if (diffDays < 7) return t('time.daysAgo', { count: diffDays })
  return date.toLocaleDateString()
}
</script>

<style scoped>
@keyframes slideInUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}
.card-animate {
  animation: slideInUp 0.4s ease-out;
}
</style>
