<template>
  <span class="relative flex h-full w-full items-center justify-center overflow-hidden">
    <!-- Always underneath: the loading state and the permanent fallback are the same thing, and
         painting it unconditionally keeps a grid from flashing empty tiles while thumbnails
         arrive. The two theme variants are CSS, not a computed value — the colour mode is unknown
         during SSR, so deciding it in script would be a hydration mismatch. -->
    <span
      class="absolute inset-0 flex items-center justify-center bg-[hsl(var(--cat-hue)_45%_94%)] dark:bg-[hsl(var(--cat-hue)_45%_18%)]"
      :style="{ '--cat-hue': visual.hue }"
      aria-hidden="true"
    >
      <UIcon
        :name="visual.icon"
        class="h-1/2 w-1/2 max-h-12 max-w-12 text-[hsl(var(--cat-hue)_40%_42%)] dark:text-[hsl(var(--cat-hue)_40%_62%)]"
      />
    </span>

    <NuxtImg
      v-if="url && !failed"
      provider="none"
      :src="url"
      :alt="alt ?? ''"
      loading="lazy"
      decoding="async"
      crossorigin="use-credentials"
      class="relative h-full w-full object-contain transition-opacity duration-200"
      :class="loaded ? 'opacity-100' : 'opacity-0'"
      @load="loaded = true"
      @error="failed = true"
    />
  </span>
</template>

<script setup lang="ts">
/**
 * The app's one product picture. Fills its parent, so the caller owns the box (a square grid
 * tile, a 56px header thumbnail, a 128px preview) and this owns the image and the placeholder.
 *
 * `src` is the server-relative path the API returns in `productImageUrl` / `productImageFullUrl`,
 * already carrying its version token. It also accepts a `data:` URI, which is what the edit form's
 * freshly-cropped preview passes before the upload has happened.
 *
 * `object-contain`, matching the stored thumbnail: product renditions are scaled to fit a box
 * rather than square-cropped, so nothing is clipped off a tall bottle or a wide label.
 */
const props = withDefaults(defineProps<{
  /** `productImageUrl` (or `productImageFullUrl`, or a `data:` URI), or null for no picture. */
  src?: string | null
  /** `ProductCategory` number; picks the placeholder's icon and colour. */
  category?: number | null
  /** Accessible name. Leave unset where the product name is already next to the image. */
  alt?: string | null
}>(), {
  src: null,
  category: null,
  alt: null
})

const { mediaUrl } = useMediaUrl()

const loaded = ref(false)
const failed = ref(false)

const url = computed(() => mediaUrl(props.src))

// A new URL means a new picture, so the fade-in and the error latch have to re-arm.
watch(url, () => {
  loaded.value = false
  failed.value = false
})

const visual = computed(() => getProductCategoryVisual(props.category))
</script>
