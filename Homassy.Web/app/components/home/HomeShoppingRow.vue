<template>
  <div class="flex items-center gap-2.5 py-2.5">
    <span
      class="shrink-0 flex items-center justify-center h-8 w-8 rounded-lg"
      :class="row.overdue ? 'bg-error/10 text-error' : 'bg-elevated text-muted'"
      aria-hidden="true"
    >
      <UIcon :name="row.overdue ? 'i-lucide-alarm-clock' : 'i-lucide-clock'" class="h-4 w-4" />
    </span>

    <div class="flex-1 min-w-0">
      <p class="text-sm font-semibold truncate flex items-center gap-1.5">
        <span
          v-if="row.color"
          class="inline-block h-2 w-2 rounded-full shrink-0"
          :style="{ backgroundColor: row.color }"
          aria-hidden="true"
        />
        {{ row.name }}
      </p>
      <p class="text-xs truncate" :class="row.overdue ? 'text-error font-semibold' : 'text-muted'">
        {{ deadlineLabel }} · {{ $t('pages.household.shopping.pending', { count: row.pendingItemCount }) }}
      </p>
    </div>

    <UButton size="xs" color="primary" class="shrink-0" @click="emit('shop')">
      {{ $t('pages.household.shopping.go') }}
    </UButton>
  </div>
</template>

<script setup lang="ts">
/**
 * One shopping list with a deadline close enough to act on, and the one action that matters:
 * walk into the shop with it.
 *
 * "Go" does not open the list for editing — it opens in-store shopping mode on it, which is what
 * someone reading this row on their way out of the door is about to want. Planning happens on the
 * lists page; this row is for leaving the house.
 *
 * The relative deadline ("today 18:00", "in 2 days") is the label, with the absolute date kept as
 * the tooltip: a deadline is read as "how long have I got", and `useRelativeTime` already falls
 * back to the date itself once that stops being a useful answer.
 */
import type { ShoppingRow } from '~/utils/homeFocus'

const props = defineProps<{ row: ShoppingRow }>()

const emit = defineEmits<{ shop: [] }>()

const { relative } = useRelativeTime(() => props.row.deadlineAt)

const deadlineLabel = computed(() => relative.value)
</script>
