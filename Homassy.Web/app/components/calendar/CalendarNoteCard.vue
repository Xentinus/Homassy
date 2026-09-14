<template>
  <div class="rounded-xl border border-l-4 border-gray-200 border-l-violet-500 bg-white px-4 py-3 dark:border-gray-700 dark:border-l-violet-400 dark:bg-gray-900">
    <div class="flex items-start justify-between gap-2">
      <span class="text-sm font-medium leading-snug text-gray-900 dark:text-gray-100">
        {{ note.title }}
      </span>
      <span class="shrink-0 rounded bg-violet-100 px-1.5 py-0.5 text-xs leading-tight text-violet-700 dark:bg-violet-900/40 dark:text-violet-300">
        {{ t('pages.calendar.eventTypes.dayNote') }}
      </span>
    </div>

    <p v-if="note.content" class="mt-1 whitespace-pre-line text-xs text-gray-600 dark:text-gray-300">
      {{ note.content }}
    </p>

    <div class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-gray-500 dark:text-gray-400">
      <span class="flex items-center gap-1">
        <UIcon name="i-lucide-user" class="h-3 w-3" />
        {{ note.lastEditedByName || note.createdByName }}
      </span>
      <span v-if="note.reminderAt" class="flex items-center gap-1">
        <!-- A sent reminder is shown as sent rather than hidden: "did anyone get told?" is the
             question a shared note raises, and an unqualified bell would not answer it. -->
        <UIcon :name="note.reminderSent ? 'i-lucide-bell-ring' : 'i-lucide-bell'" class="h-3 w-3" />
        {{ formatDateTime(note.reminderAt) }}
      </span>
    </div>

    <div class="mt-2 flex items-center gap-1">
      <UButton
        :label="t('common.edit')"
        icon="i-lucide-pencil"
        size="xs"
        color="neutral"
        variant="ghost"
        @click="emit('edit', note)"
      />
      <UButton
        :label="t('common.delete')"
        icon="i-lucide-trash-2"
        size="xs"
        color="error"
        variant="ghost"
        :loading="deleting"
        @click="emit('delete', note)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * One day note in the calendar's day panel (#60).
 *
 * A card of its own rather than a `CalendarEventCard` variant: every other event type on the
 * calendar is derived from something else and read-only, and a note is the one thing on that panel
 * a person writes and can change.
 */
import type { CalendarNoteInfo } from '~/types/calendar'

defineProps<{
  note: CalendarNoteInfo
  deleting?: boolean
}>()

const emit = defineEmits<{
  edit: [note: CalendarNoteInfo]
  delete: [note: CalendarNoteInfo]
}>()

const { t } = useI18n()
const { formatDateTime } = useDateFormat()
</script>
