<template>
  <header
    ref="headerRef"
    class="fixed inset-x-0 top-0 z-40 border-b border-gray-200 dark:border-gray-800 bg-default/95 backdrop-blur"
    :style="{ paddingTop: 'env(safe-area-inset-top)' }"
  >
    <div class="px-6 sm:px-10 lg:px-16 py-4">
      <div class="flex flex-wrap items-center gap-3">
        <!-- Back button -->
        <NuxtLink v-if="header.backTo" :to="header.backTo" class="shrink-0">
          <UButton
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
            :aria-label="t('common.back')"
          />
        </NuxtLink>

        <!-- Leading icon (or skeleton) -->
        <USkeleton v-if="showTitleSkeleton" class="h-7 w-7 rounded-md shrink-0" />
        <UIcon
          v-else-if="header.icon"
          :name="header.icon"
          class="h-7 w-7 text-primary-500 shrink-0"
        />

        <!-- Title + subtitle column -->
        <div class="min-w-0 flex-1">
          <!-- Title row -->
          <div class="flex items-center gap-1.5">
            <USkeleton v-if="showTitleSkeleton" class="h-7 w-40 rounded-md" />
            <template v-else>
              <h1 class="text-2xl font-semibold leading-tight truncate">{{ header.title }}</h1>
              <UPopover v-if="header.info">
                <UButton
                  icon="i-lucide-info"
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  :aria-label="t('common.info')"
                />
                <template #content>
                  <p class="p-3 max-w-xs text-sm text-gray-600 dark:text-gray-400">
                    {{ header.info }}
                  </p>
                </template>
              </UPopover>
            </template>
          </div>

          <!-- Subtitle row -->
          <div v-if="showSubtitleRow" class="flex items-center gap-1.5 mt-0.5">
            <USkeleton v-if="showSubtitleSkeleton" class="h-4 w-28 rounded" />
            <template v-else>
              <span
                v-if="header.subtitleColor"
                class="w-2.5 h-2.5 rounded-full shrink-0"
                :style="{ backgroundColor: header.subtitleColor }"
              />
              <span class="text-sm text-gray-600 dark:text-gray-400 truncate">{{ header.subtitle }}</span>
              <UIcon
                v-if="header.subtitleIcon"
                :name="header.subtitleIcon"
                class="h-3.5 w-3.5 text-primary-500 shrink-0"
              />
            </template>
          </div>
        </div>

        <!-- Page-specific trailing actions (teleport target). Kept INSIDE UApp
             (not <body>) so it does not hit the isolation/z-index trap that makes
             body-teleported overlays paint above the nav and swallow taps. -->
        <!-- Page search (teleport target). Full-width second row on mobile; inline on
             the right, next to the title, from md: up. A skeleton stands in while the
             header is stale/loading, matching the title skeleton above. Kept inside UApp
             (not <body>) so it does not hit the teleport isolation/z-index trap. -->
        <USkeleton
          v-if="showSearchSkeleton"
          class="order-last w-full md:order-none md:w-80 h-9 rounded-md"
        />
        <div
          v-show="!showSearchSkeleton"
          id="app-header-search"
          class="order-last w-full md:order-none md:w-80 empty:hidden"
        />

        <div id="app-header-actions" class="shrink-0 flex items-center gap-2 empty:hidden" />
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'

const { t } = useI18n()
const route = useRoute()
const header = usePageHeaderState()

// Gate on mount to avoid an SSR/hydration mismatch: SSR and the first client
// render both show the skeleton; after mount the committed state drives it.
const mounted = ref(false)

// True while the header belongs to a different route (mid-navigation) or the
// page has flagged an async load — the "skeleton, then load" window.
const isStale = computed(() =>
  !mounted.value || header.value.path !== route.path || !!header.value.loading
)

const showTitleSkeleton = computed(() => isStale.value || !header.value.title)

// Search lives in a teleport target below the title; skeleton it in lockstep with
// the title whenever the header is stale/loading, but only for pages that have one.
const showSearchSkeleton = computed(() => isStale.value && !!header.value.hasSearch)

// The subtitle row only appears once the header is committed for the current
// route, so navigation shows a single title skeleton (no phantom subtitle line).
const showSubtitleRow = computed(() =>
  mounted.value
  && header.value.path === route.path
  && (!!header.value.hasSubtitle || header.value.subtitle != null)
)

const showSubtitleSkeleton = computed(() =>
  showSubtitleRow.value && (!!header.value.loading || header.value.subtitle == null)
)

// Publish the measured header height as --app-header-height so the layout can pad
// UMain and pages can offset sticky sub-bars / scroll containers below it.
const headerRef = ref<HTMLElement | null>(null)
let observer: ResizeObserver | null = null

function measure() {
  if (headerRef.value) {
    document.documentElement.style.setProperty('--app-header-height', `${headerRef.value.offsetHeight}px`)
  }
}

onMounted(() => {
  mounted.value = true
  if (!import.meta.client || !headerRef.value) return
  observer = new ResizeObserver(measure)
  observer.observe(headerRef.value)
  measure()
})

onBeforeUnmount(() => observer?.disconnect())
</script>
