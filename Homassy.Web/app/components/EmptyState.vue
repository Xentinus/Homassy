<template>
  <div class="flex flex-col items-center gap-4 px-6 py-12 text-center">
    <EmptyStateIllustration :name="illustration" class="w-24 h-24 sm:w-28 sm:h-28 text-muted" />

    <div class="space-y-1.5">
      <p class="text-lg font-semibold text-highlighted">{{ title }}</p>
      <p v-if="description" class="mx-auto max-w-sm text-sm text-muted">{{ description }}</p>
    </div>

    <div v-if="actionLabel || secondaryLabel" class="flex flex-col items-center gap-1 pt-1">
      <UButton
        v-if="actionLabel"
        :label="actionLabel"
        :icon="actionIcon"
        color="primary"
        size="lg"
        @click="emit('action')"
      />
      <UButton
        v-if="secondaryLabel"
        :label="secondaryLabel"
        color="neutral"
        variant="link"
        size="sm"
        @click="emit('secondary')"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The one empty state in the app: illustration, title, a one-line explanation
 * and a call to action. Before this, every empty list rendered its own line of
 * grey text, so a first-run user met a sequence of blank screens with nothing
 * to press.
 *
 * Two situations, and they are not the same — keep them apart at the call site:
 *
 * - *Nothing here yet.* Use the entity's own illustration and make the primary
 *   action the one that creates the first item ("Add your first product").
 * - *No results for this filter.* Use `search`, and make the primary action
 *   "Clear filters" — offering "Add your first product" to someone who has
 *   twenty products and a typo in the search box is wrong.
 *
 * Render it *next to* the (then empty) grid rather than in place of it: the
 * lists animate through AnimatedList, and unmounting the grid replays the enter
 * animation on the way back and swallows the leave animation of the last card
 * removed. Every adopting page already follows that rule.
 */
import type { EmptyStateIllustrationName } from '~/types/emptyState'

defineProps<{
  /** Which drawing from the set. `search` for the filtered-out case. */
  illustration: EmptyStateIllustrationName
  /** Localized, one line, states the situation. */
  title: string
  /** Localized, one line, says what to do about it. */
  description?: string
  /** Localized label of the primary button. Omit for a state with no action. */
  actionLabel?: string
  /** Icon name for the primary button, e.g. `i-lucide-plus`. */
  actionIcon?: string
  /** Localized label of an optional secondary link below the button. */
  secondaryLabel?: string
}>()

const emit = defineEmits<{
  action: []
  secondary: []
}>()
</script>
