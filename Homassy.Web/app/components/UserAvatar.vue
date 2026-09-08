<template>
  <span
    class="relative inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full select-none"
    :style="{ width: `${size}px`, height: `${size}px` }"
    :role="alt ? 'img' : undefined"
    :aria-label="alt || undefined"
    :aria-hidden="alt ? undefined : 'true'"
  >
    <!-- Always rendered, underneath: this is both the loading placeholder and the permanent
         fallback for a user with no picture. Painting it unconditionally is what keeps a list
         from flashing empty circles while thumbnails arrive. -->
    <span
      class="absolute inset-0 flex items-center justify-center font-semibold text-white"
      :style="placeholderStyle"
    >{{ initials }}</span>

    <NuxtImg
      v-if="url && !failed"
      provider="none"
      :src="url"
      alt=""
      :width="size"
      :height="size"
      loading="lazy"
      decoding="async"
      crossorigin="use-credentials"
      class="relative h-full w-full object-cover transition-opacity duration-200"
      :class="loaded ? 'opacity-100' : 'opacity-0'"
      @load="loaded = true"
      @error="failed = true"
    />
  </span>
</template>

<script setup lang="ts">
/**
 * The app's one user avatar. Every place a person's picture appears — the activity feed, the nav,
 * the family drawer, the profile header — renders this, so they agree on the placeholder, the
 * sizing and the credentialed image request.
 *
 * `src` is the server-relative path the API returns in `profilePictureUrl` (already carrying its
 * version token), not a data URI: the bytes come from a cacheable endpoint now.
 */
const props = withDefaults(defineProps<{
  /** `profilePictureUrl` from the API, or null/undefined for a user with no picture. */
  src?: string | null
  /** Display name, used for the initials and the placeholder colour. */
  name?: string | null
  /** Rendered box in CSS pixels. Also the img's width/height, so nothing reflows on load. */
  size?: number
  /**
   * Accessible name. Leave unset where the name is already on screen next to the avatar (the
   * common case) — the avatar is then decorative and hidden from assistive tech rather than
   * announced twice.
   */
  alt?: string | null
}>(), {
  src: null,
  name: null,
  size: 40,
  alt: null
})

const { mediaUrl } = useMediaUrl()

const loaded = ref(false)
const failed = ref(false)

const url = computed(() => mediaUrl(props.src))

// A new URL means a new picture (the version token changed), so the fade-in and the error latch
// both have to re-arm — otherwise a replaced avatar shows at opacity 0, or a previously failed
// one never gets retried.
watch(url, () => {
  loaded.value = false
  failed.value = false
})

const initials = computed(() => {
  const name = (props.name ?? '').trim()
  if (!name) return '?'

  const words = name.split(/\s+/).slice(0, 2)
  return words.map(w => [...w][0]!.toUpperCase()).join('')
})

/**
 * Placeholder colour, derived from the name so a given person keeps the same one everywhere and
 * across reloads. Generated rather than taken from a theme token because there is no token for
 * "a colour per user"; the lightness is picked to carry white text and to sit against both the
 * light and the dark app background, so it needs no theme branch.
 */
const placeholderStyle = computed(() => {
  const seed = (props.name ?? '?')
  let hash = 0
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) | 0
  }

  const hue = Math.abs(hash) % 360
  return {
    backgroundImage: `linear-gradient(135deg, hsl(${hue} 42% 58%), hsl(${(hue + 35) % 360} 45% 42%))`,
    fontSize: `${Math.max(10, Math.round(props.size * 0.38))}px`
  }
})
</script>
