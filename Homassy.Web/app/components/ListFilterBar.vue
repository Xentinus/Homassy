<template>
  <div>
    <!-- Fixed Header -->
    <div
      ref="headerRef"
      class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 space-y-3"
    >
      <!-- Title row -->
      <div class="flex items-center gap-3">
        <NuxtLink :to="backTo">
          <UButton
            icon="i-lucide-arrow-left"
            color="neutral"
            variant="ghost"
          />
        </NuxtLink>
        <UIcon :name="icon" class="h-7 w-7 text-primary-500" />
        <h1 class="text-2xl font-semibold">{{ title }}</h1>
      </div>

      <!-- Search row + filter trigger -->
      <div class="flex items-center gap-2">
        <UFieldGroup size="md" orientation="horizontal" class="flex-1">
          <UInput
            v-model="search"
            trailing-icon="i-lucide-search"
            :placeholder="searchPlaceholder"
            class="flex-1"
          />
          <slot name="search-trailing" />
        </UFieldGroup>
        <UChip :show="filterCount > 0" :text="filterCount" color="primary" size="2xl">
          <UButton
            icon="i-lucide-sliders-horizontal"
            color="neutral"
            variant="outline"
            :aria-label="$t('common.filters.toggle')"
            :aria-expanded="filtersOpen"
            @click="filtersOpen = true"
          />
        </UChip>
      </div>

      <!-- Active filter chips (dismissible) -->
      <div
        v-if="activeFilters.length"
        class="flex items-center gap-2 overflow-x-auto pb-1 -mx-1 px-1"
      >
        <UButton
          v-for="f in activeFilters"
          :key="f.key"
          :label="f.label"
          size="xs"
          color="primary"
          variant="soft"
          trailing-icon="i-lucide-x"
          class="rounded-full shrink-0"
          :aria-label="`${$t('common.filters.removeFilter')}: ${f.label}`"
          @click="f.clear()"
        />
        <UButton
          :label="$t('common.filters.clearAll')"
          size="xs"
          color="neutral"
          variant="ghost"
          class="shrink-0"
          @click="emit('clear-all')"
        />
      </div>
    </div>

    <!-- Spacer matching the fixed header height so content flows naturally -->
    <div :style="{ height: `${headerHeight}px` }" />

    <!-- Filter drawer (bottom sheet) -->
    <UDrawer v-model:open="filtersOpen" :title="$t('common.filters.toggle')">
      <template #body>
        <div class="space-y-5 pb-2">
          <slot name="filters" />
        </div>
      </template>
      <template #footer>
        <div class="flex items-center gap-2 w-full">
          <UButton
            :label="$t('common.filters.clearAll')"
            color="neutral"
            variant="ghost"
            size="lg"
            :disabled="filterCount === 0"
            @click="emit('clear-all')"
          />
          <UButton
            class="flex-1"
            size="lg"
            color="primary"
            :label="$t('common.filters.showResults', { count: resultCount })"
            @click="filtersOpen = false"
          />
        </div>
      </template>
    </UDrawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'

withDefaults(defineProps<{
  title: string
  icon: string
  backTo?: string
  searchPlaceholder?: string
  activeFilters: { key: string, label: string, clear: () => void }[]
  filterCount: number
  resultCount: number
}>(), {
  backTo: '/profile',
  searchPlaceholder: ''
})

const emit = defineEmits<{ 'clear-all': [] }>()

const search = defineModel<string>('search', { default: '' })

const filtersOpen = ref(false)
const headerRef = ref<HTMLElement | null>(null)
// Seed with a sensible default (~the previous pt-40) so content doesn't slip
// under the fixed header before the ResizeObserver measures the real height.
const headerHeight = ref(160)
let observer: ResizeObserver | null = null

const GAP = 8 // small breathing room between the fixed header and content

function measure() {
  if (headerRef.value) headerHeight.value = headerRef.value.offsetHeight + GAP
}

onMounted(() => {
  if (!import.meta.client || !headerRef.value) return
  observer = new ResizeObserver(measure)
  observer.observe(headerRef.value)
  measure()
})

onBeforeUnmount(() => {
  observer?.disconnect()
})
</script>
