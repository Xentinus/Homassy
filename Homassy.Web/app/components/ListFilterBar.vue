<template>
  <div>
    <!-- Search + filter bar, teleported into the persistent AppHeader (the page
         identity lives there too via usePageHeader). The header renders a
         skeleton in this slot while it is stale/loading. -->
    <Teleport to="#app-header-search">
      <div class="space-y-3">
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
    </Teleport>

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
import { ref } from 'vue'

const props = withDefaults(defineProps<{
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

// The page identity (back arrow + icon + title) is rendered by the persistent
// AppHeader in the auth layout; feed it from this bar's props.
usePageHeader(() => ({
  backTo: props.backTo,
  icon: props.icon,
  title: props.title,
  hasSearch: true
}))
</script>
