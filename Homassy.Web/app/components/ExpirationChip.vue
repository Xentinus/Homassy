<template>
  <span
    v-if="tone.level !== 'none'"
    class="inline-flex items-center gap-1.5 text-sm"
    :class="tone.text"
    :title="absolute"
  >
    <UIcon :name="tone.icon" class="h-4 w-4 shrink-0" />
    <span class="font-medium">{{ label }}</span>
  </span>
</template>

<script setup lang="ts">
/**
 * "in 3 days" / "expired 2 days ago", coloured by the shared expiration ramp.
 *
 * The phrasing comes from `useRelativeTime`, so all three locales get grammatical output — and
 * "tomorrow" / "yesterday" rather than "in 1 day" — from `Intl.RelativeTimeFormat`. The threshold
 * is the ramp's own window: inside it a relative phrase is the useful thing to read, outside it the
 * date is, and either way the exact date and time is on the `title`.
 */
const props = defineProps<{
  date?: string | null
}>()

const { t } = useI18n()
const { expirationTone } = useExpirationStatus()
const { relative, absolute, absoluteDate, isAbsolute } = useRelativeTime(
  () => props.date,
  { thresholdMs: EXPIRATION_SOON_DAYS * 86_400_000 }
)

const tone = computed(() => expirationTone(props.date))

const label = computed(() => {
  if (isAbsolute.value) return absoluteDate.value
  // "expired 2 days ago" reads as one phrase; "in 3 days" already does on its own.
  return tone.value.level === 'expired'
    ? t('expiration.expiredRelative', { relative: relative.value })
    : relative.value
})
</script>
