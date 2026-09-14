<template>
  <AppDrawer
    :open="open"
    :title="isEdit ? t('pages.calendar.notes.editTitle') : t('pages.calendar.notes.createTitle')"
    :icon="isEdit ? 'i-lucide-pencil' : 'i-lucide-sticky-note'"
    :loading="saving"
    fit="content"
    @update:open="(v) => emit('update:open', v)"
  >
    <p class="mb-4 text-sm text-muted">{{ t('pages.calendar.notes.description') }}</p>

    <UForm ref="formRef" :schema="schema" :state="form" class="space-y-4" @submit="onSubmit">
      <UFormField :label="t('pages.calendar.notes.titleLabel')" name="title" required>
        <UInput
          v-model="form.title"
          :placeholder="t('pages.calendar.notes.titlePlaceholder')"
          :disabled="saving"
          class="w-full"
        />
      </UFormField>

      <UFormField :label="t('pages.calendar.notes.contentLabel')" name="content">
        <UTextarea
          v-model="form.content"
          :placeholder="t('pages.calendar.notes.contentPlaceholder')"
          :disabled="saving"
          :rows="4"
          class="w-full"
        />
      </UFormField>

      <UFormField name="hasReminder">
        <UCheckbox
          v-model="form.hasReminder"
          :label="t('pages.calendar.notes.reminderLabel')"
          :disabled="saving"
        />
      </UFormField>

      <!-- The reminder is an instant, and the family only sees one of them, so it is deliberately
           a single date+time rather than the lead-time list an external calendar carries: a note
           has no start time to lead from. -->
      <div v-if="form.hasReminder" class="grid grid-cols-2 gap-4">
        <UFormField :label="t('pages.calendar.notes.reminderDate')" name="reminderDate">
          <UInputDate v-model="form.reminderDate" :locale="inputDateLocale" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('pages.calendar.notes.reminderTime')" name="reminderTime">
          <UInput v-model="form.reminderTime" type="time" :disabled="saving" class="w-full" />
        </UFormField>
      </div>

      <p v-if="form.hasReminder" class="text-xs text-muted">
        {{ t('pages.calendar.notes.reminderHint') }}
      </p>
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
/**
 * Create or edit a day note on the family calendar (#60).
 *
 * The reminder is entered as a local date and time and sent as a UTC instant, because that is what
 * the worker compares against: a wall-clock time with no zone would fire at the container's offset
 * rather than the household's.
 */
import { computed, ref, watch } from 'vue'
import type { Ref } from 'vue'
import { z } from 'zod'
import { CalendarDate, type DateValue } from '@internationalized/date'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { CalendarNoteInfo } from '~/types/calendar'

const props = withDefaults(defineProps<{
  open: boolean
  /** The day the note belongs to, `YYYY-MM-DD`. Required for a new note. */
  date: string
  note?: CalendarNoteInfo | null
}>(), {
  note: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [note: CalendarNoteInfo]
}>()

const { t } = useI18n()
const toast = useToast()
const { inputDateLocale } = useInputDateLocale()
const { createCalendarNote, updateCalendarNote } = useCalendarApi()
const { toFormErrors } = useApiFormErrors()

/** The default reminder: the morning of the day the note is about. */
const DEFAULT_REMINDER_TIME = '08:00'

const isEdit = computed(() => !!props.note)

const schema = z.object({
  title: z.string({ required_error: t('pages.calendar.notes.titleRequired') })
    .min(1, t('pages.calendar.notes.titleRequired'))
    .max(120),
  content: z.string().max(2000).optional(),
  hasReminder: z.boolean().optional().default(false),
  reminderTime: z.string().optional()
})
type Schema = z.output<typeof schema>

interface NoteForm {
  title: string
  content: string
  hasReminder: boolean
  reminderDate: DateValue | null
  reminderTime: string
}

const parseDateKey = (key: string): CalendarDate => {
  const [y, m, d] = key.split('-').map(Number)
  const today = new Date()
  if (!y || !m || !d) return new CalendarDate(today.getFullYear(), today.getMonth() + 1, today.getDate())
  return new CalendarDate(y, m, d)
}

const emptyForm = (): NoteForm => ({
  title: '',
  content: '',
  hasReminder: false,
  reminderDate: parseDateKey(props.date),
  reminderTime: DEFAULT_REMINDER_TIME
})

// Cast back to the declared shape — see the same note in LoadInventoryFromListsDrawer: `UnwrapRef`
// strips the private brands off `DateValue`, and UInputDate's v-model needs them.
const form = ref<NoteForm>(emptyForm()) as Ref<NoteForm>
const saving = ref(false)
const formRef = ref()

watch(() => props.open, (isOpen) => {
  if (!isOpen) return

  if (!props.note) {
    form.value = emptyForm()
    return
  }

  const reminder = props.note.reminderAt ? new Date(props.note.reminderAt) : null
  form.value = {
    title: props.note.title,
    content: props.note.content ?? '',
    hasReminder: !!reminder,
    reminderDate: reminder
      ? new CalendarDate(reminder.getFullYear(), reminder.getMonth() + 1, reminder.getDate())
      : parseDateKey(props.note.date),
    reminderTime: reminder
      ? `${String(reminder.getHours()).padStart(2, '0')}:${String(reminder.getMinutes()).padStart(2, '0')}`
      : DEFAULT_REMINDER_TIME
  }
})

/** Local date + `HH:mm` → the UTC instant the worker fires on. */
const toReminderIso = (): string | null => {
  if (!form.value.hasReminder || !form.value.reminderDate) return null

  const [hours, minutes] = (form.value.reminderTime || DEFAULT_REMINDER_TIME).split(':').map(Number)
  const local = new Date(
    form.value.reminderDate.year,
    form.value.reminderDate.month - 1,
    form.value.reminderDate.day,
    hours ?? 8,
    minutes ?? 0,
    0
  )
  return local.toISOString()
}

async function onSubmit(event: FormSubmitEvent<Schema>) {
  const data = event.data
  saving.value = true

  try {
    const payload = {
      date: props.note?.date ?? props.date,
      title: data.title.trim(),
      content: data.content?.trim() || null,
      reminderAt: toReminderIso()
    }

    const failureMessage = { errorMessage: t('pages.calendar.notes.saveFailed') }
    const res = props.note
      ? await updateCalendarNote(props.note.publicId, payload, failureMessage)
      : await createCalendarNote(payload, failureMessage)

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
    } else {
      formRef.value?.setErrors(toFormErrors(res.validationErrors, ['title', 'content']))
    }
  } catch (error) {
    // Only a request that never reached the API lands here; useApiClient stays silent for those.
    console.error('Failed to save the calendar note:', error)
    toast.add({
      title: t('common.error'),
      description: t('pages.calendar.notes.saveFailed'),
      color: 'error',
      icon: 'i-lucide-alert-circle'
    })
  } finally {
    saving.value = false
  }
}
</script>
