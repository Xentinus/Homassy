<template>
  <span ref="el" class="tabular-nums">{{ displayed }}</span>
</template>

<script setup lang="ts">
/**
 * A number that counts up to its value the first time it scrolls into view.
 *
 * Before this, every count in the app appeared the instant its fetch resolved.
 * Counting up is only worth anything if the reader is looking at it, hence the
 * IntersectionObserver: the animation waits for the element, runs once, and
 * later updates tween from the value already on screen (see useCountUp).
 *
 * `tabular-nums` is not decoration. Proportional digits each have their own
 * advance width, so a counter running through 60 frames of changing digits
 * visibly wobbles — and with locale grouping (1,234 / 1 234 / 1.234) the
 * separator moves too.
 *
 * Under `prefers-reduced-motion: reduce` there is no observer and no animation:
 * the final value is rendered immediately, on the first frame after mount. SSR
 * still renders the pre-animation zero — the media query is a client fact, and
 * branching on it during render would be a hydration mismatch. That is invisible
 * where this is used today, because the number's fetch resolves on the client
 * and the server renders a skeleton in its place.
 */
const props = defineProps<{
  /** The target. Updates are tweened from whatever is currently displayed. */
  value: number
  /** Milliseconds for a full run; the composable's default is usually right. */
  duration?: number
}>()

const el = ref<HTMLElement | null>(null)
const { displayed, animate, settle } = useCountUp({ duration: props.duration })

const prefersReducedMotion = () =>
  import.meta.client && window.matchMedia('(prefers-reduced-motion: reduce)').matches

/**
 * Whether the element has been seen. Until it has, the value is held at zero:
 * the fetch that supplies it usually resolves while the section is still below
 * the fold, and the count has to start from zero when the reader gets there.
 */
const seen = ref(false)

watch(() => props.value, (to) => {
  if (!seen.value) return
  if (prefersReducedMotion()) settle(to)
  else animate(to)
})

onMounted(() => {
  if (prefersReducedMotion()) {
    seen.value = true
    settle(props.value)
    return
  }

  const target = el.value
  if (!target) return

  const observer = new IntersectionObserver((entries) => {
    if (!entries.some(entry => entry.isIntersecting)) return
    // Once only — this is an entrance, not a scroll effect.
    observer.disconnect()
    seen.value = true
    animate(props.value)
  }, { threshold: 0.5 })

  observer.observe(target)
  onBeforeUnmount(() => observer.disconnect())
})
</script>
