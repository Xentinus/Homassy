<template>
  <div
    class="relative mx-auto w-full"
    :style="{ maxWidth: `${width}px` }"
  >
    <!-- A soft pool of light under the device, so the frame reads as an object on the page
         rather than a rectangle stuck to it. Purely decorative and behind everything. -->
    <div
      aria-hidden="true"
      class="pointer-events-none absolute -inset-6 -z-10 rounded-[3rem] bg-primary-400/20 blur-3xl dark:bg-primary-500/10"
    />

    <div class="rounded-[2.2rem] border border-default bg-elevated p-2 shadow-xl ring-1 ring-black/5 dark:ring-white/10">
      <div class="relative overflow-hidden rounded-[1.7rem] bg-default" :style="{ aspectRatio: ASPECT_RATIO }">
        <!-- The notch. Drawn, not decorative filler: without it the frame reads as a tablet. -->
        <div
          aria-hidden="true"
          class="absolute left-1/2 top-0 z-10 h-4 w-24 -translate-x-1/2 rounded-b-2xl bg-elevated"
        />

        <!-- A real capture when one is given, the rendered stand-in otherwise. Both fill the
             same fixed-ratio box, so which one is shown cannot change the layout. -->
        <NuxtImg
          v-if="resolvedSrc"
          :src="resolvedSrc"
          :alt="alt"
          :width="width"
          :height="Math.round(width * (19.5 / 9))"
          sizes="(max-width: 640px) 80vw, 320px"
          format="webp"
          class="h-full w-full object-cover object-top"
          :loading="priority ? 'eager' : 'lazy'"
          :fetchpriority="priority ? 'high' : 'auto'"
        />
        <slot v-else />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

/**
 * A phone-shaped frame around one app screen, for the landing page (#123).
 *
 * The frame owns the aspect ratio, so whatever is inside it — a rendered `LandingAppScreen` or
 * a real capture — occupies exactly the same box. That is what keeps the page free of layout
 * shift: the space is reserved by CSS before anything loads.
 *
 * `light` / `dark` are there for the day real screenshots exist: pass both and the frame picks
 * the one matching the visitor's theme. Until then the slot carries a rendered screen, which
 * follows the theme by itself.
 */
const props = withDefaults(defineProps<{
  /** Rendered width in CSS pixels; also what `NuxtImg` sizes the capture to. */
  width?: number
  /** Screenshot for the light theme. */
  light?: string
  /** Screenshot for the dark theme; falls back to `light`. */
  dark?: string
  alt?: string
  /** Load the capture eagerly — for the one device above the fold. */
  priority?: boolean
}>(), {
  width: 300,
  light: undefined,
  dark: undefined,
  alt: '',
  priority: false
})

/** iPhone-ish. One constant, because the frame and the image both have to agree on it. */
const ASPECT_RATIO = '9 / 19.5'

const colorMode = useColorMode()

const resolvedSrc = computed(() => {
  if (!props.light && !props.dark) return undefined
  return colorMode.value === 'dark' ? (props.dark ?? props.light) : props.light
})
</script>
