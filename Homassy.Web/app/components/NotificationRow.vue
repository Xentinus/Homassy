<template>
  <div class="relative rounded-xl overflow-hidden" style="touch-action: pan-y" data-no-pull-refresh>
    <!-- Swipe action layer, revealed behind the row while dragging. One action in both
         directions: there is only one thing to do to a notification you do not want. -->
    <div
      v-show="swipe.isSwiping.value"
      aria-hidden="true"
      class="absolute inset-0 rounded-xl flex items-center justify-between px-4 bg-error-500 dark:bg-error-600"
    >
      <UIcon
        name="i-lucide-trash-2"
        class="h-5 w-5 text-white transition-transform duration-150"
        :class="swipe.progress.value >= 1 ? 'scale-125' : ''"
      />
      <UIcon
        name="i-lucide-trash-2"
        class="h-5 w-5 text-white transition-transform duration-150"
        :class="swipe.progress.value >= 1 ? 'scale-125' : ''"
      />
    </div>

    <div
      ref="rowEl"
      class="relative flex w-full items-start gap-3 rounded-xl border border-default bg-default p-3 text-left select-none"
      :class="notification.isRead ? '' : 'bg-primary-50/60 dark:bg-primary-950/30'"
      :style="swipe.cardStyle.value"
      role="button"
      tabindex="0"
      @click="onActivate"
      @keydown.enter.prevent="onActivate"
      @keydown.space.prevent="onActivate"
    >
      <UIcon :name="template.icon" class="mt-0.5 h-5 w-5 shrink-0 text-primary-500" />

      <div class="min-w-0 flex-1">
        <div class="flex items-start gap-2">
          <p class="min-w-0 flex-1 text-sm font-semibold text-highlighted">
            {{ t(template.titleKey) }}
          </p>
          <!-- The unread marker is a dot, not a bold row: an inbox where the unread rows are
               heavier is an inbox where the read ones look broken. -->
          <span
            v-if="!notification.isRead"
            class="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-primary-500"
            :aria-label="t('notifications.unread')"
          />
        </div>
        <p class="mt-0.5 text-sm text-muted break-words">
          {{ body }}
        </p>
        <RelativeTime :date="notification.createdAt" class="mt-1 block text-xs text-dimmed" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * One row of the notification centre (#116).
 *
 * The row's wording comes from `notificationTemplate` — the stored row carries a type and
 * parameters, and the text is composed here, in the reader's current language. Nothing is
 * rendered from stored prose.
 */
import { computed, ref } from 'vue'
import { notificationTemplate } from '~/utils/notificationTemplate'
import { reminderLeadTimeLabel } from '~/utils/calendarReminders'
import type { NotificationInfo } from '~/types/notification'

const props = defineProps<{ notification: NotificationInfo }>()

const emit = defineEmits<{
  /** Tapped — the drawer marks it read and navigates to its target. */
  activate: [notification: NotificationInfo]
  /** Swiped past the threshold. */
  dismiss: [notification: NotificationInfo]
}>()

const { t } = useI18n()

const rowEl = ref<HTMLElement | null>(null)

const swipe = useSwipeActions(rowEl, {
  // The same action either way. A notification has one destructive verb and no constructive
  // one, so binding only one direction would make half the gesture do nothing.
  onSwipeLeft: () => emit('dismiss', props.notification),
  onSwipeRight: () => emit('dismiss', props.notification)
})

const template = computed(() => notificationTemplate(props.notification))

/**
 * The template's own parameters, plus the one value that has to be localized rather than
 * interpolated raw: the calendar reminder's lead time. `reminderLeadTimeLabel` is the same helper
 * the external-calendar settings form uses, so "15 minutes" is worded identically in both places
 * and in all three languages.
 */
const bodyParams = computed<Record<string, string>>(() => {
  const params = { ...template.value.params }

  if (props.notification.type === 'CalendarEventReminder') {
    const minutes = Number.parseInt(params.leadMinutes ?? '0', 10)
    const label = reminderLeadTimeLabel(Number.isFinite(minutes) ? minutes : 0)
    params.lead = t(label.key, label.params)
  }

  return params
})

/**
 * The rendered body.
 *
 * The third `t()` argument is vue-i18n's plural selector — the same pattern
 * `AggregatedActivityCard` uses. It is passed whenever the row carries a `count`, because the
 * count-bearing templates carry the singular/plural (and, for the weekly summary, the
 * nothing-at-all) forms English and German need. Hungarian's count noun does not change, so its
 * translations simply have fewer forms and the selector is a no-op there.
 */
const body = computed(() => {
  const count = Number.parseInt(bodyParams.value.count ?? '', 10)

  return Number.isFinite(count)
    ? t(template.value.bodyKey, bodyParams.value, count)
    : t(template.value.bodyKey, bodyParams.value)
})

function onActivate() {
  // A swipe that ended in a tap-sized movement still fires a click; `suppressClick` is how the
  // gesture tells us to ignore it.
  if (swipe.suppressClick.value) return
  emit('activate', props.notification)
}
</script>
