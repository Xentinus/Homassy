<template>
  <AppDrawer
    :open="open"
    :title="title"
    icon="i-lucide-notebook-pen"
    :loading="saving"
    @update:open="(v) => emit('update:open', v)"
  >
    <UForm ref="formRef" :schema="schema" :state="form" class="space-y-4" @submit="onSubmit">
      <UFormField :label="t('pages.calendar.dayNotes.date')" name="date" required>
        <UInputDate v-model="datePicker" :locale="inputDateLocale" :disabled="saving" class="w-full">
          <template #trailing>
            <UPopover>
              <UButton icon="i-lucide-calendar" color="neutral" variant="ghost" size="xs" :disabled="saving" />
              <template #content>
                <UCalendar v-model="datePicker" :locale="inputDateLocale" />
              </template>
            </UPopover>
          </template>
        </UInputDate>
      </UFormField>

      <UFormField :label="t('pages.calendar.dayNotes.noteTitle')" name="title" required>
        <UInput
          v-model="form.title"
          :placeholder="t('pages.calendar.dayNotes.titlePlaceholder')"
          :disabled="saving"
          class="w-full"
        />
      </UFormField>

      <UFormField :label="t('pages.calendar.dayNotes.content')" name="content">
        <UTextarea
          v-model="form.content"
          :placeholder="t('pages.calendar.dayNotes.contentPlaceholder')"
          :rows="5"
          :disabled="saving"
          class="w-full"
        />
      </UFormField>

      <UFormField name="reminderEnabled">
        <UCheckbox
          v-model="form.reminderEnabled"
          :label="t('pages.calendar.dayNotes.reminderEnabled')"
          :disabled="saving"
        />
      </UFormField>

      <!-- Only shown once the reminder is on: an empty time input beside an off checkbox reads as broken. -->
      <UFormField v-if="form.reminderEnabled" :label="t('pages.calendar.dayNotes.reminderTime')" name="reminderTime">
        <UInput v-model="form.reminderTime" type="time" :disabled="saving" class="w-32" />
        <p class="text-xs text-muted mt-1">{{ t('pages.calendar.dayNotes.reminderHint') }}</p>
      </UFormField>
    </UForm>

    <template #footer>
      <UButton :label="t('common.cancel')" color="neutral" variant="ghost" @click="emit('update:open', false)" />
      <UButton
        :label="t('common.save')"
        color="primary"
        icon="i-lucide-save"
        :loading="saving"
        @click="formRef?.submit()"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { z } from 'zod'
import type { CalendarDate } from '@internationalized/date'
import { CalendarDate as CalendarDateClass } from '@internationalized/date'
import type { FormSubmitEvent } from '@nuxt/ui'
import { useCalendarNoteApi } from '~/composables/api/useCalendarNoteApi'
import type { CalendarNoteResponse } from '~/types/calendarNote'

/**
 * Create/edit a family day note. Owns the API call and emits `saved` with the resulting DTO so the calendar can
 * patch locally; the master-data socket delivers the same change to other members.
 */
const props = withDefaults(defineProps<{
  open: boolean
  note?: CalendarNoteResponse | null
  /** `YYYY-MM-DD` seed for a new note — normally the day selected on the calendar. */
  date: string
}>(), {
  note: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [note: CalendarNoteResponse]
  /** Another member changed the note first; the page should reload rather than retry. */
  conflict: []
}>()

const { t } = useI18n()
const toast = useToast()
const { inputDateLocale } = useInputDateLocale()
const { createCalendarNote, updateCalendarNote } = useCalendarNoteApi()

const isEdit = computed(() => !!props.note)
const title = computed(() => isEdit.value
  ? t('pages.calendar.dayNotes.edit')
  : t('pages.calendar.dayNotes.create'))

const schema = z.object({
  // A plain `YYYY-MM-DD` string rather than a CalendarDate: the picker's class type does not survive a `ref`
  // deep-unwrap or a Zod inference round-trip, and the string is already the shape the API wants.
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, t('pages.calendar.dayNotes.dateRequired')),
  title: z.string({ required_error: t('pages.calendar.dayNotes.titleRequired') })
    .min(1, t('pages.calendar.dayNotes.titleRequired'))
    .max(128),
  content: z.string().max(2000).optional(),
  reminderEnabled: z.boolean().optional().default(false),
  reminderTime: z.string()
    .regex(NOTE_REMINDER_TIME_PATTERN, t('pages.calendar.dayNotes.invalidTime'))
    .optional()
    .or(z.literal(''))
}).refine(d => !d.reminderEnabled || !!d.reminderTime, {
  path: ['reminderTime'],
  message: t('pages.calendar.dayNotes.invalidTime')
})
type Schema = z.output<typeof schema>

// `YYYY-MM-DD` <-> CalendarDate, never via Date/toISOString() which would shift the day off UTC.
const toCalendarDate = (key: string): CalendarDate => {
  const [y, m, d] = noteDayKey(key).split('-').map(Number)
  return new CalendarDateClass(y ?? 1970, m ?? 1, d ?? 1)
}

const emptyForm = (dateKey: string) => ({
  date: noteDayKey(dateKey),
  title: '',
  content: '',
  reminderEnabled: false,
  reminderTime: DEFAULT_NOTE_REMINDER_TIME
})

const form = ref(emptyForm(props.date))
const saving = ref(false)
const formRef = ref()

/** Bridges the pickers (which speak `CalendarDate`) to the string the form state holds. */
const datePicker = computed<CalendarDate>({
  get: () => toCalendarDate(form.value.date),
  set: (value) => {
    if (value) form.value.date = toDateKey(value.year, value.month, value.day)
  }
})

// Hydrate on open, not on prop change: the parent keeps `note` set while the drawer animates closed.
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  if (props.note) {
    form.value = {
      date: noteDayKey(props.note.date),
      title: props.note.title,
      content: props.note.content || '',
      reminderEnabled: !!props.note.reminderTime,
      reminderTime: props.note.reminderTime || DEFAULT_NOTE_REMINDER_TIME
    }
  } else {
    form.value = emptyForm(props.date)
  }
})

async function onSubmit(event: FormSubmitEvent<Schema>) {
  const data = event.data
  saving.value = true
  try {
    const dateKey = data.date
    const reminderTime = data.reminderEnabled ? (data.reminderTime || null) : null

    const res = props.note
      ? await updateCalendarNote(props.note.publicId, {
        date: dateKey,
        title: data.title.trim(),
        // '' erases the body; omitting the key would mean "unchanged", so always send a value.
        content: data.content?.trim() ?? '',
        // An omitted key reads as "unchanged" server-side, which would make the reminder impossible to
        // remove — the explicit flag is what clears it.
        reminderTime: reminderTime ?? undefined,
        clearReminder: reminderTime === null,
        version: props.note.version
      })
      : await createCalendarNote({
        date: dateKey,
        title: data.title.trim(),
        content: data.content?.trim() || null,
        reminderTime
      })

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
      return
    }

    // useApiClient has already toasted the translated code; a conflict additionally needs a reload, because
    // retrying with the same stale version would fail again.
    if (res.errorCodes?.includes('CALNOTE-0006')) {
      emit('conflict')
      emit('update:open', false)
      return
    }

    toast.add({
      title: t('common.error'),
      description: t('pages.calendar.dayNotes.saveFailed'),
      color: 'error',
      icon: 'i-lucide-alert-circle'
    })
  } catch (error) {
    console.error('Failed to save calendar note:', error)
    toast.add({
      title: t('common.error'),
      description: t('pages.calendar.dayNotes.saveFailed'),
      color: 'error',
      icon: 'i-lucide-alert-circle'
    })
  } finally {
    saving.value = false
  }
}
</script>
