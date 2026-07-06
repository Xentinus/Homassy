<template>
  <div>
    <!-- Search row: input + optional trailing actions + filter toggle -->
    <div v-if="!showCreate" class="flex gap-2">
      <UInput
        :model-value="query"
        :placeholder="placeholder"
        icon="i-lucide-search"
        size="lg"
        class="flex-1"
        @update:model-value="(v) => emit('update:query', String(v))"
      />
      <slot name="search-trailing" />
      <UChip v-if="hasFilters" :show="filterCount > 0" :text="filterCount" color="primary" size="2xl">
        <UButton
          icon="i-lucide-sliders-horizontal"
          :color="filterCount > 0 ? 'primary' : 'neutral'"
          :variant="filterCount > 0 ? 'soft' : 'outline'"
          size="lg"
          square
          :aria-label="t('common.filter')"
          :aria-expanded="filtersOpen"
          @click="filtersOpen = !filtersOpen"
        />
      </UChip>
    </div>

    <!-- Inline filter panel (appears in place — no nested drawer) -->
    <div v-if="!showCreate && hasFilters && filtersOpen" class="mt-3 space-y-4 rounded-lg border border-default bg-elevated/50 p-3">
      <slot name="filters" />
    </div>

    <!-- Active filter chips -->
    <div v-if="!showCreate && filterCount > 0" class="mt-3 flex flex-wrap items-center gap-2">
      <slot name="chips" />
    </div>

    <!-- Search results, or the inline create form -->
    <div class="mt-3">
      <template v-if="!showCreate">
        <slot name="results" />
        <button
          v-if="query.trim()"
          type="button"
          class="mt-2 flex w-full items-center gap-2 rounded-lg border border-primary-300 bg-primary-50 px-3 py-2.5 text-sm font-medium text-primary-700 transition hover:bg-primary-100 dark:border-primary-800 dark:bg-primary-950/40 dark:text-primary-300 dark:hover:bg-primary-950/70"
          @click="startCreate"
        >
          <UIcon name="i-lucide-plus" class="h-4 w-4 shrink-0" />
          <span class="truncate">{{ createLabel }}</span>
        </button>
      </template>

      <div v-else>
        <UButton
          icon="i-lucide-arrow-left"
          color="neutral"
          variant="ghost"
          size="sm"
          class="mb-3"
          @click="emit('update:showCreate', false)"
        >
          {{ t('pages.shoppingLists.addProduct.backToSearch') }}
        </UButton>
        <slot name="create" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, useSlots } from 'vue'

/**
 * A "search or create" panel: a search field with an optional inline filter
 * (button → in-place panel + active-filter chips, no nested drawer), a list of
 * results, and a prominent "create «query»" row that opens an inline create form.
 *
 * The parent owns the data, filtering and the create logic; this component is the
 * reusable shell. Slots: `search-trailing`, `filters`, `chips`, `results`, `create`.
 */
withDefaults(defineProps<{
  /** Search text (v-model:query). */
  query: string
  /** Whether the inline create form is shown (v-model:show-create). */
  showCreate: boolean
  /** Search input placeholder. */
  placeholder: string
  /** Label for the "create «query»" row, e.g. `Create "Milk"`. */
  createLabel: string
  /** Number of active filters — drives the badge and the chips row. */
  filterCount?: number
}>(), {
  filterCount: 0
})

const emit = defineEmits<{
  'update:query': [value: string]
  'update:showCreate': [value: boolean]
  create: []
}>()

const { t } = useI18n()
const slots = useSlots()

const hasFilters = computed(() => !!slots.filters)
const filtersOpen = ref(false)

const startCreate = () => {
  emit('update:showCreate', true)
  emit('create')
}
</script>
