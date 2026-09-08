<template>
  <div
    :class="frameClasses"
    :style="{ ...accentStyle(userPublicId, identityColor), borderLeftColor: 'var(--member-color)' }"
  >
    <template v-if="density === 'compact'">
      <!-- Reproduces CalendarActivityCard.vue's layout exactly: record name + type chip on the
           first row, actor + relative time on the second. No icon, no leading slot content, no
           details today — none of that was there before this shell existed. -->
      <div class="flex items-start justify-between gap-2">
        <span class="text-sm font-medium text-gray-900 dark:text-gray-100 leading-snug">
          {{ recordName }}
        </span>
        <span class="shrink-0 text-xs rounded px-1.5 py-0.5 leading-tight bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300">
          {{ t(`enums.activityType.${activityType}`) }}
        </span>
      </div>
      <div class="flex items-center gap-1.5 mt-0.5">
        <slot name="meta" />
        <span class="text-xs text-gray-400">·</span>
        <!-- Today's entries read as "5 minutes ago" and keep counting; older days fall back to
             the clock time, since the panel header already names the day and "3 days ago" would
             say less than "14:32". -->
        <RelativeTime
          :date="timestamp"
          absolute-format="time"
          :threshold="DAY_MS"
          class="text-xs text-gray-400 dark:text-gray-500"
        />
      </div>
      <slot name="details" />
    </template>

    <template v-else>
      <!-- Reproduces ActivityCard.vue's layout exactly: leading avatar, name + time, activity
           type as a plain (non-chip) line, then an icon + record name row, then details. -->
      <div class="flex items-start gap-3 mb-3">
        <div class="flex-shrink-0">
          <slot name="leading" />
        </div>

        <div class="flex-1 min-w-0">
          <div class="flex items-center justify-between gap-2">
            <slot name="meta" />
            <RelativeTime
              :date="timestamp"
              class="text-xs text-gray-500 dark:text-gray-500 flex-shrink-0"
            />
          </div>

          <div class="flex items-center gap-2 mt-1">
            <span class="text-sm text-gray-600 dark:text-gray-400">
              {{ t(`enums.activityType.${activityType}`) }}
            </span>
          </div>
        </div>
      </div>

      <div class="ml-13 space-y-2">
        <div class="flex items-center gap-2">
          <UIcon :name="getActivityIcon(activityType)" class="h-4 w-4" :class="getActivityIconColor(activityType)" />
          <p class="text-sm font-medium text-gray-900 dark:text-white">
            {{ recordName }}
          </p>
        </div>

        <slot name="details" />
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
/**
 * Shared frame behind every activity card: the card shell, the member accent border, the
 * activity-type badge, and the `getActivityIcon` / `getActivityIconColor` mapping — one copy
 * instead of the one `ActivityCard.vue` used to keep to itself.
 *
 * `density` reproduces two pre-existing, pixel-specific layouts rather than blending them into a
 * new third look: `'comfortable'` is `ActivityCard.vue`'s current markup, `'compact'` is
 * `CalendarActivityCard.vue`'s (tighter padding, no avatar, no icon, and the
 * `absolute-format="time"` / `:threshold="DAY_MS"` `RelativeTime` behaviour that reads as a clock
 * time instead of a relative phrase once an entry is a day old — the calendar's own day panel
 * header already names the day, so "3 days ago" would say less there than "14:32").
 *
 * The shell owns the accent border, the activity-type line and (comfortable only) the icon +
 * record-name row, plus the `RelativeTime` instance for either density. It does not know the
 * actor's display name (`#meta`) or their avatar (`#leading`, comfortable only) — callers differ
 * too much in how they resolve those (a `UserInfo` lookup vs. a plain `userName` string) for the
 * shell to own that resolution. `#details` is for whatever renders below the record name: a
 * quantity row today, an aggregated card's expand affordance in Task 13.
 */
import { computed } from 'vue'
import type { ActivityType } from '~/types/activity'
import { getActivityIcon, getActivityIconColor } from '~/utils/activityIcons'

const props = withDefaults(defineProps<{
  userPublicId: string
  /** Chosen palette key override for this member, or null/absent for the deterministic pick. */
  identityColor?: string | null
  activityType: ActivityType
  recordName: string
  timestamp: string
  density?: 'comfortable' | 'compact'
}>(), {
  identityColor: null,
  density: 'comfortable'
})

const { t } = useI18n()
const { accentStyle } = useMemberColor()

const DAY_MS = 24 * 60 * 60 * 1000

const frameClasses = computed(() => props.density === 'compact'
  ? 'rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-4 py-3 border-l-4'
  : 'p-4 rounded-xl shadow-sm bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 hover:shadow-lg hover:-translate-y-0.5 transition-all duration-200 border-l-[3px]')
</script>
