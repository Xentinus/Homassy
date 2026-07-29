<template>
  <div class="relative rounded-xl overflow-hidden" style="touch-action: pan-y" data-no-pull-refresh>
    <!-- Swipe action layer -->
    <div
      v-show="swipe.isSwiping.value"
      aria-hidden="true"
      class="absolute inset-0 rounded-xl flex items-center justify-between px-4"
      :class="swipe.direction.value === 'left' ? 'bg-error-500 dark:bg-error-600' : 'bg-primary-500 dark:bg-primary-600'"
    >
      <UIcon
        name="i-lucide-pencil"
        class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'right' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']"
      />
      <UIcon
        name="i-lucide-trash-2"
        class="h-5 w-5 text-white transition-transform duration-150"
        :class="[swipe.direction.value === 'left' ? 'opacity-100' : 'opacity-0', swipe.progress.value >= 1 ? 'scale-125' : '']"
      />
    </div>

    <!-- Card surface -->
    <div
      ref="cardEl"
      class="relative rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-4 py-3 border-l-4 border-l-emerald-500 cursor-pointer select-none"
      :style="swipe.cardStyle.value"
      @click="handleCardClick"
    >
      <div class="flex items-start justify-between gap-2">
        <span class="text-sm font-medium text-gray-900 dark:text-gray-100 leading-snug">
          {{ note.title }}
        </span>
        <div class="flex items-center gap-1 shrink-0">
          <span class="text-xs rounded px-1.5 py-0.5 leading-tight bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300">
            {{ t('pages.calendar.eventTypes.dayNote') }}
          </span>
          <!-- An explicit button as well as the swipe: the day panel is also the desktop right column, where
               dragging a card with a mouse is undiscoverable. -->
          <UButton
            size="xs"
            variant="ghost"
            color="neutral"
            icon="i-lucide-trash-2"
            :title="t('pages.calendar.dayNotes.deleteTitle')"
            @click.stop="() => { isDeleteOpen = true }"
          />
        </div>
      </div>

      <p v-if="note.content" class="text-sm text-gray-600 dark:text-gray-300 mt-1 whitespace-pre-line line-clamp-3">
        {{ note.content }}
      </p>

      <div v-if="note.reminderTime" class="flex items-center gap-1.5 mt-1">
        <UIcon name="i-lucide-bell" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400 shrink-0" />
        <span class="text-xs text-gray-500 dark:text-gray-400">{{ note.reminderTime }}</span>
      </div>

      <div class="flex items-center gap-1.5 mt-0.5">
        <span class="text-xs text-gray-500 dark:text-gray-400">{{ note.authorName }}</span>
        <span class="text-xs text-gray-400">·</span>
        <span class="text-xs text-gray-400 dark:text-gray-500">{{ createdTime }}</span>
      </div>

      <!-- Own line rather than appended to the author row: both names plus two times overflow a mobile card. -->
      <div v-if="wasEdited" class="flex items-center gap-1.5 mt-0.5">
        <UIcon name="i-lucide-pencil" class="h-3 w-3 text-gray-400 shrink-0" />
        <span class="text-xs text-gray-400 dark:text-gray-500">
          {{ t('pages.calendar.dayNotes.editedBy', { name: note.lastEditedByName }) }} · {{ editedTime }}
        </span>
      </div>
    </div>

    <!-- Delete confirmation -->
    <AppDrawer
      :open="isDeleteOpen"
      :title="t('pages.calendar.dayNotes.deleteTitle')"
      icon="i-lucide-trash-2"
      fit="content"
      @update:open="(v) => { isDeleteOpen = v }"
    >
      <p class="text-sm text-muted">{{ t('pages.calendar.dayNotes.deleteWarning') }}</p>
      <div>
        <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ t('pages.calendar.dayNotes.noteTitle') }}:</span>
        <span class="text-sm ml-2">{{ note.title }}</span>
      </div>
      <template #footer>
        <UButton :label="t('common.cancel')" color="neutral" variant="outline" @click="() => { isDeleteOpen = false }" />
        <UButton :label="t('common.delete')" color="error" @click="confirmDelete" />
      </template>
    </AppDrawer>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { CalendarNoteResponse } from '~/types/calendarNote'

const props = defineProps<{
  note: CalendarNoteResponse
}>()

const emit = defineEmits<{
  edit: [note: CalendarNoteResponse]
  deleted: [publicId: string]
}>()

const { t, locale } = useI18n()

const isDeleteOpen = ref(false)

const cardEl = ref<HTMLElement | null>(null)
const swipe = useSwipeActions(cardEl, {
  onSwipeLeft: () => { isDeleteOpen.value = true },
  onSwipeRight: () => emit('edit', props.note),
  disabled: () => isDeleteOpen.value
})

const formatTime = (timestamp: string): string =>
  new Date(timestamp).toLocaleTimeString(locale.value, { hour: '2-digit', minute: '2-digit' })

const createdTime = computed(() => formatTime(props.note.createdAt))
const editedTime = computed(() => props.note.lastEditedAt ? formatTime(props.note.lastEditedAt) : '')

// lastEditedAt stays null until the note is actually edited, so this needs no timestamp comparison.
const wasEdited = computed(() => !!props.note.lastEditedAt && !!props.note.lastEditedByName)

function handleCardClick(event: MouseEvent) {
  if (swipe.suppressClick.value) return
  const target = event.target as HTMLElement
  if (target.closest('a, button')) return
  emit('edit', props.note)
}

function confirmDelete() {
  isDeleteOpen.value = false
  emit('deleted', props.note.publicId)
}
</script>
