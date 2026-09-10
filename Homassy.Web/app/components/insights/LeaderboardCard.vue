<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-users" class="h-5 w-5 shrink-0 text-primary" />
        <div class="min-w-0 flex-1">
          <h2 class="truncate text-base font-semibold text-highlighted leading-tight">
            {{ $t('insights.leaderboard.title') }}
          </h2>
          <p v-if="periodLabel" class="truncate text-sm text-muted">{{ periodLabel }}</p>
        </div>
      </div>
    </template>

    <div v-if="loading" class="space-y-2">
      <USkeleton v-for="row in 3" :key="row" class="h-10 w-full" />
    </div>

    <p v-else-if="members.length === 0" class="text-sm text-muted">
      {{ $t('insights.leaderboard.empty') }}
    </p>

    <ul v-else class="space-y-2.5">
      <li v-for="row in rows" :key="row.publicId" class="space-y-1">
        <div class="flex items-center gap-2">
          <!-- Identity colour as an accent only: a dot, never text on an arbitrary background. -->
          <span
            class="h-2.5 w-2.5 shrink-0 rounded-full bg-[var(--member-color)]"
            :style="row.accent"
          />
          <span v-if="row.rank" class="w-4 shrink-0 text-xs font-medium text-dimmed tabular-nums">{{ row.rank }}</span>
          <!-- A member with nothing this period gets no rank number - and no gap where one would
               be, so the absence does not read as a missing position. -->
          <span class="min-w-0 flex-1 truncate text-sm font-medium text-highlighted">{{ row.displayName }}</span>
          <span class="shrink-0 text-sm font-semibold text-highlighted tabular-nums">{{ row.total }}</span>
          <span class="flex w-14 shrink-0 items-center justify-end gap-1 text-xs" :class="row.deltaClass">
            <UIcon :name="row.deltaIcon" class="h-3.5 w-3.5 shrink-0" :aria-label="row.deltaLabel" />
            <span class="tabular-nums">{{ row.deltaMagnitude }}</span>
          </span>
        </div>

        <!-- The bar is the ranking, drawn as a fill in the member's own colour - the second of
             R4's two permitted accent treatments (ring, dot or bar fill). Width is relative to the
             busiest member, so the leader's bar is always full and the rest read against it. -->
        <div class="h-1.5 w-full overflow-hidden rounded-full bg-elevated">
          <div
            class="h-full rounded-full bg-[var(--member-color)]"
            :style="{ ...row.accent, width: row.barWidth }"
          />
        </div>
      </li>
    </ul>
  </UCard>
</template>

<script setup lang="ts">
/**
 * The family leaderboard (#109): members ranked by what they did in the window, each row painted
 * with that member's own identity colour.
 *
 * <b>The copy rule applies to the iconography, not just the words.</b> A member who did less than
 * last period gets a dimmed arrow and the magnitude - a neutral marker, never an error colour and
 * never a red down-arrow. A member who did nothing gets their name and a zero, with no rank number
 * and no empty state of their own: they are part of the household, and a leaderboard that scolds
 * the quiet ones is a leaderboard nobody wants on their fridge.
 *
 * <b>Not built on `ChartBar`</b>, though the task brief lists it. `ChartBar` colours its bars from
 * the positional series palette (`seriesColors`), which cannot paint a bar in a specific member's
 * identity colour - and that colour is the whole point of these rows. The bars here are plain divs
 * with `--member-color` set per row, which is exactly the "bar fill" accent R4's rule permits.
 * `ChartBar` remains the right component for the spend chart, where the categories are shops and
 * have no identity of their own.
 */
import { computed } from 'vue'
import type { CSSProperties } from 'vue'
import { formatAxisDate } from '~/utils/chart/format'
import type { MemberScore } from '~/types/insights'

const props = withDefaults(defineProps<{
  members: MemberScore[]
  loading?: boolean
  /** The window the counters cover, as the API reported it ("yyyy-MM-dd"). */
  periodStart?: string
  periodEnd?: string
}>(), {
  loading: false,
  periodStart: undefined,
  periodEnd: undefined
})

const { t, locale } = useI18n()
const { accentStyle } = useMemberColor()

/**
 * The bounds are plain calendar dates the server already resolved in the viewer's own zone, so they
 * are parsed as UTC and formatted from that instant - never through the local `Date` constructor,
 * which would reinterpret the calendar date in the viewer's offset and can name the day before.
 *
 * Formatted with the chart layer's own `formatAxisDate` (its 'week' shape is the date without a
 * weekday, which is what a range label wants) rather than vue-i18n's `d()`: this project declares
 * no `datetimeFormats`, so `d(date, 'short')` returns an empty string - which is exactly what the
 * first version of this card rendered.
 */
const periodLabel = computed(() => {
  if (!props.periodStart || !props.periodEnd) return ''
  const parse = (value: string): number => Date.parse(`${value}T00:00:00Z`)
  return t('insights.leaderboard.period', {
    from: formatAxisDate(parse(props.periodStart), locale.value, 'week'),
    to: formatAxisDate(parse(props.periodEnd), locale.value, 'week')
  })
})

/** The busiest member's total, as the bar widths' denominator. Never zero (guarded below). */
const leaderTotal = computed(() => props.members.reduce((max, member) => Math.max(max, member.currentPeriodTotal), 0))

interface LeaderboardRow {
  publicId: string
  displayName: string
  total: number
  /** Absent for a member with nothing this period - see the module comment. */
  rank: number | null
  barWidth: string
  accent: CSSProperties
  deltaIcon: string
  deltaClass: string
  deltaLabel: string
  deltaMagnitude: string
}

const rows = computed<LeaderboardRow[]>(() => {
  let rank = 0

  return props.members.map((member) => {
    const delta = member.currentPeriodTotal - member.previousPeriodTotal
    if (member.currentPeriodTotal > 0) rank++

    return {
      publicId: member.publicId,
      displayName: member.displayName,
      total: member.currentPeriodTotal,
      rank: member.currentPeriodTotal > 0 ? rank : null,
      barWidth: leaderTotal.value > 0
        ? `${Math.round((member.currentPeriodTotal / leaderTotal.value) * 100)}%`
        : '0%',
      accent: accentStyle(member.publicId, member.identityColor),
      // Up: worth celebrating, so it gets the primary accent. Down: a dimmed arrow, never an error
      // colour. Level: a neutral dash, so "same as last time" is not styled as a failure either.
      deltaIcon: delta > 0 ? 'i-lucide-trending-up' : delta < 0 ? 'i-lucide-arrow-down' : 'i-lucide-minus',
      deltaClass: delta > 0 ? 'text-primary' : 'text-dimmed',
      deltaLabel: delta > 0
        ? t('insights.leaderboard.deltaUp')
        : delta < 0
          ? t('insights.leaderboard.deltaDown')
          : t('insights.leaderboard.deltaLevel'),
      deltaMagnitude: delta === 0 ? '' : String(Math.abs(delta))
    }
  })
})
</script>
