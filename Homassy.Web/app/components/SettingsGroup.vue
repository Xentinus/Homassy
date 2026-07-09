<template>
  <section class="space-y-2">
    <p v-if="title" class="px-1 text-xs font-medium uppercase tracking-wide text-muted">
      {{ title }}
    </p>
    <div class="rounded-xl border border-default divide-y divide-default overflow-hidden bg-default">
      <slot />
    </div>
    <p v-if="hint" class="px-1 text-xs text-muted">
      {{ hint }}
    </p>
  </section>
</template>

<script setup lang="ts">
/**
 * iOS-style grouped settings card: an optional muted caption above a rounded
 * bordered container whose rows are separated by `divide-y`. Put SettingsRow
 * (or any row markup) in the default slot.
 */
defineProps<{
  /** Muted uppercase caption shown above the card. */
  title?: string
  /** Muted caption shown below the card. */
  hint?: string
}>()
</script>
