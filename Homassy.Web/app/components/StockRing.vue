<template>
  <span
    class="relative inline-flex shrink-0 items-center justify-center"
    :style="{ width: `${size}px`, height: `${size}px` }"
  >
    <!-- With a reference amount: a real gauge. -->
    <svg
      v-if="hasReference"
      class="absolute inset-0 -rotate-90"
      :width="size"
      :height="size"
      viewBox="0 0 100 100"
      aria-hidden="true"
    >
      <circle
        cx="50"
        cy="50"
        :r="RADIUS"
        fill="none"
        stroke="currentColor"
        :stroke-width="STROKE"
        class="opacity-15"
      />
      <circle
        cx="50"
        cy="50"
        :r="RADIUS"
        fill="none"
        stroke="currentColor"
        :stroke-width="STROKE"
        stroke-linecap="round"
        :stroke-dasharray="CIRCUMFERENCE"
        :stroke-dashoffset="dashOffset"
        :style="{ transition: animate ? 'stroke-dashoffset 700ms cubic-bezier(0.22, 1, 0.36, 1)' : 'none' }"
      />
    </svg>

    <!-- Without one: a flat badge. Deliberately not a full or empty ring — either would be a
         claim about a proportion nobody knows. -->
    <span
      v-else
      class="absolute inset-0 rounded-full border-2 border-current opacity-25"
      aria-hidden="true"
    />

    <span class="relative flex items-center justify-center">
      <slot />
    </span>
  </span>
</template>

<script setup lang="ts">
/**
 * A circular stock gauge drawn around whatever is slotted into it — the quantity icon on an
 * inventory row, the amount on a grid card.
 *
 * It shows how much of the item is left **against what was bought** (`PurchaseInfo.OriginalQuantity`).
 * An item with no purchase info has no denominator, and rather than picking one it falls back to a
 * flat ring: the slot still reads the same, but nothing on screen claims a proportion.
 *
 * The colour is inherited (`currentColor`), so the caller sets it from the expiration ramp and the
 * gauge follows without knowing anything about expiry.
 */
const props = withDefaults(defineProps<{
  /** What is left. */
  value: number
  /** What there was to begin with. Null / zero means "unknown", which draws the flat badge. */
  reference?: number | null
  /** Outer diameter in CSS pixels. */
  size?: number
}>(), {
  reference: null,
  size: 44
})

const RADIUS = 44
const STROKE = 8
const CIRCUMFERENCE = 2 * Math.PI * RADIUS

const hasReference = computed(() => props.reference != null && props.reference > 0)

const fraction = computed(() => {
  if (!hasReference.value) return 0
  // Clamped: consuming can leave a quantity above the original (an edit raises it), and an arc
  // past 100% would just overdraw itself.
  return Math.min(Math.max(props.value / (props.reference as number), 0), 1)
})

/**
 * Starts empty and fills in on mount. Under `prefers-reduced-motion` the fill is simply there —
 * `animate` stays false, so there is no transition for the value change to run through.
 */
const animate = ref(false)
const settled = ref(false)

const dashOffset = computed(() =>
  CIRCUMFERENCE * (1 - (settled.value ? fraction.value : 0))
)

onMounted(() => {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    settled.value = true
    return
  }

  animate.value = true
  // Next frame, so the browser has painted the empty ring for the transition to start from.
  requestAnimationFrame(() => { settled.value = true })
  // rAF does not fire in a hidden tab; without this the ring would stay empty until the tab came
  // back into view.
  setTimeout(() => { settled.value = true }, 120)
})
</script>
