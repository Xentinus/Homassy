<template>
  <div class="flex h-full flex-col overflow-hidden rounded-2xl border-2 border-default bg-default p-3 shadow-sm">
    <div class="min-w-0 space-y-1">
      <USkeleton class="h-4 w-3/4 rounded" />
      <USkeleton
        v-for="line in lines"
        :key="line"
        class="h-3 rounded"
        :class="line === lines ? 'w-1/2' : 'w-full'"
      />
    </div>

    <div v-if="footerLines > 0" class="mt-auto space-y-2 pt-4">
      <div v-for="line in footerLines" :key="line" class="flex items-center gap-2">
        <USkeleton class="h-3.5 w-3.5 shrink-0 rounded-full" />
        <USkeleton class="h-3 rounded" :class="FOOTER_WIDTHS[(line - 1) % FOOTER_WIDTHS.length]" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The loading placeholder for one card in a grid.
 *
 * Every card grid in the app is built the same way — `rounded-2xl border-2 p-3`,
 * a `space-y-1` header with a bold title and a muted line, then a `mt-auto pt-4
 * space-y-2` block of icon-plus-text attributes pinned to the bottom — so the
 * placeholder is built from those same pieces at those same sizes. Each call
 * site used to invent its own `h-36` / `h-48` / `h-32` box instead, and wherever
 * the guess was wrong the content jumped when the data arrived.
 *
 * Height comes from the content, exactly as it does in the real card (`h-full`
 * in a grid cell), rather than from a fixed `h-*`: put this in the *same* grid
 * classes as the real list and the two agree by construction.
 *
 * The shimmer is `USkeleton`'s, the one Nuxt UI already animates, and
 * app/assets/css/main.css switches it off under `prefers-reduced-motion`.
 */

/** Attribute rows are short and uneven in the real cards; mirror that. */
const FOOTER_WIDTHS = ['w-2/3', 'w-1/2', 'w-3/5']

withDefaults(defineProps<{
  /** Muted lines below the title. The last one is rendered half-width. */
  lines?: number
  /** Icon-plus-text rows in the block pinned to the bottom. 0 removes it. */
  footerLines?: number
}>(), {
  lines: 2,
  footerLines: 2
})
</script>
