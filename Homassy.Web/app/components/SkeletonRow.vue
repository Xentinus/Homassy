<template>
  <div class="flex items-start gap-3 rounded-xl border border-default bg-default p-3">
    <USkeleton v-if="icon" class="mt-0.5 h-5 w-5 shrink-0 rounded-full" />

    <div class="min-w-0 flex-1 space-y-2">
      <div class="flex items-center justify-between gap-2">
        <USkeleton class="h-3.5 w-1/3 rounded" />
        <USkeleton class="h-3 w-14 shrink-0 rounded" />
      </div>
      <USkeleton
        v-for="line in lines"
        :key="line"
        class="h-3 rounded"
        :class="line === lines ? 'w-1/3' : 'w-2/3'"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * The loading placeholder for one row in a stacked list — the item lists inside
 * the overview drawers and the product history timeline.
 *
 * Same idea as SkeletonCard, for the other shape the app uses: `rounded-xl
 * border p-3`, an optional leading icon, a title with a right-aligned meta
 * value, and a line or two under it. Render it in the same `space-y-*`
 * container as the real rows.
 */
withDefaults(defineProps<{
  /** Leading icon bubble, for the rows that have one. */
  icon?: boolean
  /** Lines below the title row. The last one is rendered narrow. */
  lines?: number
}>(), {
  icon: false,
  lines: 1
})
</script>
