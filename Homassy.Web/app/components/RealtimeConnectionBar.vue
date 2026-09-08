<template>
  <Transition name="bubble">
    <div
      v-if="visible"
      role="status"
      aria-live="polite"
      class="flex items-center gap-1.5 font-medium"
      :class="[variantClass, toneClass]"
    >
      <template v-if="status === 'reconnecting'">
        <UIcon name="i-lucide-loader-circle" class="realtime-spin h-4 w-4 shrink-0" />
        <span>{{ $t('realtime.reconnecting') }}</span>
      </template>
      <template v-else-if="status === 'offline'">
        <UIcon name="i-lucide-cloud-off" class="h-4 w-4 shrink-0" />
        <span>{{ $t('realtime.socketOffline') }}</span>
      </template>
      <template v-else-if="status === 'device-offline'">
        <UIcon name="i-lucide-wifi-off" class="h-4 w-4 shrink-0" />
        <span>{{ $t('realtime.deviceOffline') }}</span>
      </template>
      <template v-else>
        <!-- Only reachable when justReconnected is true — see `visible` below. -->
        <UIcon name="i-lucide-circle-check" class="h-4 w-4 shrink-0" />
        <span>{{ $t('realtime.backOnline') }}</span>
      </template>
    </div>
  </Transition>
</template>

<script setup lang="ts">
/**
 * The one realtime connection indicator — a `useRealtimeStatus()` reading rendered as either a
 * full-width bar (`products/index.vue`, under its header) or a compact chip (`shopping-lists/
 * index.vue`, next to `PresenceAvatars`). One component, both places: the whole point of
 * aggregating hub states into a single status is that a page with more than one hub open can
 * never show two contradicting indicators, which a forked bar/chip pair would risk re-introducing.
 *
 * Renders nothing at all — not even an empty wrapper — while `status === 'idle'` (this page never
 * opened a hub) or while `status === 'connected'` with no `justReconnected` confirmation to show:
 * there is nothing to tell the user in either case. Mounting/unmounting through the existing
 * `bubble` transition (rather than a bespoke fade) means the CSS timing and its
 * `prefers-reduced-motion` handling are already accounted for.
 */
import { computed } from 'vue'
import { useRealtimeStatus } from '~/composables/useRealtimeStatus'

const props = withDefaults(defineProps<{
  /** 'bar': full-width block under a page header. 'chip': compact pill next to PresenceAvatars. */
  variant?: 'bar' | 'chip'
}>(), {
  variant: 'bar'
})

const { status, justReconnected } = useRealtimeStatus()

const visible = computed(() => {
  if (status.value === 'idle') return false
  if (status.value === 'connected') return justReconnected.value
  return true
})

const variantClass = computed(() => props.variant === 'chip'
  ? 'rounded-full px-3 py-1 text-xs'
  : 'rounded-2xl px-4 py-2.5 text-sm w-full')

// Semantic Nuxt UI tokens, never a literal hex — see CLAUDE.md's theming rule. 'reconnecting' and
// 'back online' (the only states reachable when status is 'connected') get their own tone even
// though neither is an error, so the bar/chip never reads as an alarm for a transient retry or a
// pure good-news confirmation.
const toneClass = computed(() => {
  if (status.value === 'reconnecting') return 'bg-warning/10 text-warning'
  if (status.value === 'offline' || status.value === 'device-offline') return 'bg-error/10 text-error'
  return 'bg-success/10 text-success' // justReconnected, status === 'connected'
})
</script>

<style scoped>
/* Tailwind's animate-spin utility (used by the reconnecting icon above) has no reduced-motion
   guard of its own — see main.css's own note on .animate-pulse, which has the same gap. Scoped
   here rather than patched globally: this component is the only new spin this task adds, and a
   scoped rule can't affect the pre-existing PullToRefreshIndicator spinner elsewhere. */
.realtime-spin {
  animation: spin 1s linear infinite;
}

@media (prefers-reduced-motion: reduce) {
  .realtime-spin {
    animation: none;
  }
}
</style>
