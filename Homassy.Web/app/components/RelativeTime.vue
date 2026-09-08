<template>
  <span :title="absolute">
    <!-- The relative phrase depends on the moment it is rendered, so a
         server-rendered one would disagree with the client's the instant a
         minute ticked over between the two. The server (and the first client
         render) shows the plain date; the live label takes over on mount. -->
    <ClientOnly>
      {{ label }}
      <template #fallback>{{ fallbackLabel }}</template>
    </ClientOnly>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue'

/**
 * A timestamp that keeps itself current: "2 minutes ago" turns into "3 minutes
 * ago" on its own, driven by the app's single shared ticker (see
 * `useRelativeTime`), never by a per-component interval.
 *
 * Renders a plain `<span>`, so the caller keeps full control of typography —
 * put the text classes on the component itself. The full date and time is
 * always on the `title` attribute, whatever the visible label says.
 *
 * A relative phrase stops carrying information once a value is old enough, so
 * past `threshold` the label falls back to the `absolute` form.
 */
const props = withDefaults(defineProps<{
  date: string | Date | null | undefined
  /** Which absolute form takes over past the threshold. */
  absoluteFormat?: 'date' | 'datetime' | 'time'
  /** Age (ms) at which the label stops being relative. Defaults to 7 days. */
  threshold?: number
}>(), {
  absoluteFormat: 'date',
  threshold: undefined
})

const { relative, absolute, absoluteDate, absoluteTime, isAbsolute } = useRelativeTime(
  () => props.date,
  { thresholdMs: () => props.threshold }
)

const fallbackLabel = computed(() => {
  switch (props.absoluteFormat) {
    case 'datetime': return absolute.value
    case 'time': return absoluteTime.value
    default: return absoluteDate.value
  }
})

const label = computed(() => (isAbsolute.value ? fallbackLabel.value : relative.value))
</script>
