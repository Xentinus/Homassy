<template>
  <div>
    <ActivityCardShell
      :user-public-id="entry.userPublicId"
      :identity-color="entry.userIdentityColor"
      :activity-type="entry.activityType"
      :record-name="aggregatedLabel"
      :timestamp="entry.timestamp"
    >
      <template #leading>
        <UserAvatar
          :src="entry.userProfilePictureUrl"
          :name="entry.userName"
          :public-id="entry.userPublicId"
          :identity-color="entry.userIdentityColor"
          :size="40"
        />
      </template>

      <template #meta>
        <p class="font-semibold text-gray-900 dark:text-white truncate">
          {{ entry.userName }}
        </p>
      </template>

      <template #details>
        <!-- A real button (not a clickable div) so the expand/collapse state is announced and
             keyboard-reachable like any other control. -->
        <button
          type="button"
          class="flex items-center gap-1 text-sm font-medium text-primary-600 dark:text-primary-400"
          :aria-expanded="expanded"
          @click="expanded = !expanded"
        >
          <UIcon
            name="i-lucide-chevron-down"
            class="expand-chevron h-4 w-4"
            :class="{ 'rotate-180': expanded }"
          />
          {{ expanded ? t('activity.collapse') : t('activity.expand') }}
        </button>
      </template>
    </ActivityCardShell>

    <!-- The individual runs, revealed with the bubble motion — same tokens as every other list in
         the app, via AnimatedList. A separate list from the summary card above on purpose: an
         AnimatedList only animates its own direct children, and the summary card must never be
         part of that FLIP batch. -->
    <AnimatedList v-if="expanded" class="mt-2 ml-4 space-y-2 border-l-2 border-gray-100 pl-3 dark:border-gray-800">
      <ActivityCardShell
        v-for="item in entry.items ?? []"
        :key="item.publicId"
        density="compact"
        :user-public-id="item.userPublicId"
        :identity-color="item.identityColor"
        :activity-type="item.activityType"
        :record-name="item.recordName"
        :timestamp="item.timestamp"
      >
        <template #meta>
          <span class="text-xs text-gray-500 dark:text-gray-400">{{ item.userName }}</span>
        </template>
      </ActivityCardShell>
    </AnimatedList>
  </div>
</template>

<script setup lang="ts">
/**
 * A collapsed run of same-actor, same-type activities (`ActivityTimelineEntry.count > 1`) — the
 * bulk-import case #108 exists for. Collapsed by default: the record line reads as one summarised
 * sentence (`activity.aggregated`) instead of one card per near-identical entry, and tapping the
 * button reveals the individual `ActivityInfo` rows at `density="compact"` inside an
 * `<AnimatedList>`.
 *
 * Only ever rendered for `count > 1` — a `count === 1` entry is a plain `ActivityCardShell` the
 * timeline page renders directly, since a single entry has nothing to expand.
 */
import { computed, ref } from 'vue'
import type { ActivityTimelineEntry } from '~/types/activity'

const props = defineProps<{
  entry: ActivityTimelineEntry
}>()

const { t } = useI18n()

const expanded = ref(false)

/**
 * `count` is passed through as the run's literal total (not "count minus the one already named"),
 * so the message states the aggregate directly ("12 activities (Inventory Created), most
 * recently: Milk") rather than an "and N more" phrasing, which would either double-count the
 * named record or need the caller to do the off-by-one subtraction itself. `t`'s third positional
 * argument is vue-i18n's plural selector — see `UndoToast.vue`'s `pluralAwareT` for the same
 * pattern; German and English both need the singular | plural split, Hungarian does not (its
 * count noun does not change) and so has no `|` in its translation at all.
 */
const aggregatedLabel = computed(() => t(
  'activity.aggregated',
  {
    name: props.entry.recordName,
    count: props.entry.count,
    type: t(`enums.activityType.${props.entry.activityType}`)
  },
  props.entry.count
))
</script>

<style scoped>
/* The chevron's direction is the information (expanded vs. collapsed); the smooth rotation is
   decoration on top of it. A named class rather than Tailwind's transition-transform utility so
   the reduced-motion override below can target it specifically -- same approach as
   RealtimeConnectionBar.vue's .realtime-spin, and for the same reason: a blanket rule on the
   utility class would also strip transitions from unrelated call sites that use it. */
.expand-chevron {
  transition: transform 200ms;
}

@media (prefers-reduced-motion: reduce) {
  .expand-chevron {
    transition: none;
  }
}
</style>
