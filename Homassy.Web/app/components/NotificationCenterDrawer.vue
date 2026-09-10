<template>
  <AppDrawer
    :open="isOpen"
    :title="t('notifications.title')"
    icon="i-lucide-bell"
    @update:open="(value) => { if (!value) close() }"
  >
    <template #header-extra>
      <div class="flex items-center justify-between gap-2">
        <p class="text-xs text-muted">
          {{ unreadCount > 0 ? t('notifications.unreadCount', { count: unreadCount }, unreadCount) : t('notifications.allRead') }}
        </p>
        <UButton
          v-if="unreadCount > 0"
          :label="t('notifications.markAllRead')"
          icon="i-lucide-check-check"
          color="primary"
          variant="ghost"
          size="xs"
          @click="markEverythingRead"
        />
      </div>
    </template>

    <div class="space-y-4">
      <template v-if="isLoading && items.length === 0">
        <SkeletonRow v-for="i in 5" :key="i" />
      </template>

      <EmptyState
        v-else-if="items.length === 0"
        illustration="notifications"
        :title="t('notifications.empty.title')"
        :description="t('notifications.empty.description')"
      />

      <template v-else>
        <section v-for="group in dayGroups" :key="group.key" :aria-labelledby="`notif-day-${group.key}`">
          <h2
            :id="`notif-day-${group.key}`"
            class="mb-2 flex items-baseline gap-2 px-1"
          >
            <span class="text-xs font-bold uppercase tracking-wide text-highlighted">{{ dayHeading(group.key) }}</span>
            <span class="text-xs text-muted tabular-nums">{{ group.entries.length }}</span>
          </h2>

          <!-- One AnimatedList per day section, not one for the whole list: a day header inside a
               TransitionGroup would join the rows' FLIP animation. Same reason the inventory grid
               and the shopping list split their sections. -->
          <AnimatedList class="space-y-2">
            <NotificationRow
              v-for="entry in group.entries"
              :key="entry.publicId"
              :notification="entry"
              @activate="onActivate"
              @dismiss="onDismiss"
            />
          </AnimatedList>
        </section>

        <div v-if="hasMore" class="pt-1">
          <UButton
            :label="t('notifications.loadMore')"
            color="neutral"
            variant="soft"
            block
            :loading="isLoadingMore"
            @click="loadMore"
          />
        </div>
      </template>
    </div>
  </AppDrawer>
</template>

<script setup lang="ts">
/**
 * The notification centre (#116) — the inbox behind the header bell.
 *
 * It replaces nothing: `NotificationsDrawer` (renamed to `NotificationSettingsDrawer` in the same
 * change) is the *preferences* panel and always was, despite the name. This is the list of what
 * was actually sent.
 *
 * Grouped by day using the same `groupByDay` the activity timeline uses — two time-ordered feeds
 * with two implementations of "is this today" is one implementation too many, and the day-boundary
 * reasoning in that util is not worth repeating.
 */
import { computed } from 'vue'
import { groupByDay, type DayBucketKey } from '~/utils/activityTimeline'
import { formatDayBucketDate } from '~/utils/activityTimeline'
import type { NotificationInfo } from '~/types/notification'

const { t, locale } = useI18n()

const {
  items,
  unreadCount,
  isLoading,
  isLoadingMore,
  isOpen,
  hasMore,
  close,
  loadMore,
  markOneRead,
  markEverythingRead,
  dismissOne
} = useNotificationCenter()

/**
 * `groupByDay` buckets on a `timestamp` field, which is what the activity timeline's entries call
 * theirs. Mapping rather than renaming the DTO field: `createdAt` is the honest name for a
 * notification's own timestamp, and adapting one field at one call site is cheaper than a
 * generic-key parameter on a tested util.
 */
const dayGroups = computed(() =>
  groupByDay(items.value.map(item => ({ ...item, timestamp: item.createdAt })), new Date())
)

const dayHeading = (key: DayBucketKey): string => {
  if (key === 'today') return t('activity.today')
  if (key === 'yesterday') return t('activity.yesterday')
  return formatDayBucketDate(key, locale.value)
}

/**
 * Tapping a row marks it read and, when it has one, goes to what it is about. The drawer closes
 * first: leaving a full-height sheet open over the page it just navigated to would hide the thing
 * the user tapped through to see.
 */
async function onActivate(notification: NotificationInfo) {
  await markOneRead(notification.publicId)

  if (notification.targetUrl) {
    close()
    await navigateTo(notification.targetUrl)
  }
}

async function onDismiss(notification: NotificationInfo) {
  await dismissOne(notification.publicId)
}
</script>
