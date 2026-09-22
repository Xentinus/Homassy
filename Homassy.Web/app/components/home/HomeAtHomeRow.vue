<template>
  <div class="flex items-center gap-2.5 py-2.5">
    <span
      class="shrink-0 flex items-center justify-center h-8 w-8 rounded-lg text-[11px] font-bold tabular-nums"
      :class="badgeClass"
      aria-hidden="true"
    >{{ badgeText }}</span>

    <div class="flex-1 min-w-0">
      <p class="text-sm font-semibold truncate">{{ item.productName }}</p>
      <p class="text-xs truncate" :class="tone.text">
        <ExpirationChip :date="item.expirationAt" class="align-middle" />
        <span v-if="item.storageLocationName" class="text-muted">
          · {{ item.storageLocationName }}
        </span>
      </p>
    </div>

    <div class="shrink-0 flex items-center gap-1.5">
      <UButton
        size="xs"
        color="primary"
        :loading="busy === 'discard'"
        :disabled="!!busy"
        @click="emit('discard')"
      >
        {{ $t('pages.household.atHome.discard') }}
      </UButton>
      <UButton
        size="xs"
        color="neutral"
        variant="outline"
        :loading="busy === 'consume'"
        :disabled="!!busy"
        @click="emit('consume')"
      >
        {{ $t('pages.household.atHome.consume') }}
      </UButton>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * One piece of stock that wants a decision tonight, with the two decisions attached.
 *
 * The row states the verdict rather than the data: a leading chip counting days ("-2", "1d") and
 * the relative date, because "expires 2026-09-22" is a fact the reader then has to do arithmetic
 * on while standing in front of an open fridge.
 *
 * Both buttons are destructive-ish in the sense that they remove stock, but only one of them is a
 * loss: "used it" is the good outcome and is therefore the quieter, outlined button, while
 * "threw it out" is the one that needs no hunting for. Neither is a `UButton` with an icon alone
 * — at this size the difference between a bin and a fork is not readable, and the undo toast that
 * follows a wrong tap is a worse apology than a legible label.
 */
import type { AtHomeItem } from '~/utils/homeFocus'

const props = defineProps<{
  item: AtHomeItem
  /** Which action is mid-flight, so the row can spin the right button and lock the other. */
  busy?: 'consume' | 'discard' | null
}>()

const emit = defineEmits<{
  consume: []
  discard: []
}>()

const { t } = useI18n()

const tone = computed(() => toneForLevel(props.item.level))

const days = computed(() => daysUntilExpiration(props.item.expirationAt))

/** Days as a chip: negative for what is already gone off, "0" for today, "Nd" for what is left. */
const badgeText = computed(() => {
  const value = days.value
  if (value === null) return '—'
  return value < 0 ? String(value) : t('pages.household.atHome.daysLeft', { days: value })
})

const badgeClass = computed(() => props.item.level === 'expired'
  ? 'bg-error/10 text-error'
  : 'bg-warning/10 text-warning')
</script>
