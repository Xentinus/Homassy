<template>
  <div>
    <!-- Search + filters bar, teleported into the persistent AppHeader (which
         renders a skeleton in this slot while it is stale/loading). -->
    <Teleport to="#app-header-search">
      <!-- Search row (always visible) + filters trigger -->
      <div class="space-y-2">
        <div class="flex gap-2">
          <UFieldGroup size="md" orientation="horizontal" class="flex-1">
            <UInput
              v-model="searchQuery"
              class="flex-1"
              type="text"
              :placeholder="$t('common.searchPlaceholder')"
            >
              <template #trailing>
                <UButton
                  v-if="searchQuery"
                  icon="i-lucide-x"
                  size="xs"
                  color="neutral"
                  variant="ghost"
                  :aria-label="$t('common.clear')"
                  @click="() => { searchQuery = '' }"
                />
              </template>
            </UInput>
            <BarcodeScannerButton v-if="showCameraButton" @scanned="handleBarcodeScanned" />
          </UFieldGroup>
          <UChip :show="activeFilterCount > 0" :text="activeFilterCount" color="primary" size="2xl">
            <UButton
              icon="i-lucide-sliders-horizontal"
              color="primary"
              size="md"
              :aria-label="$t('pages.products.filters.toggle')"
              :aria-expanded="filtersOpen"
              @click="() => { filtersOpen = true }"
            >
              <span class="hidden sm:inline">{{ $t('pages.products.filters.toggle') }}</span>
            </UButton>
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
            :aria-label="`${$t('pages.products.filters.removeFilter')}: ${f.label}`"
            @click="f.clear()"
          />
          <UButton
            :label="$t('pages.products.filters.clearAll')"
            size="xs"
            color="neutral"
            variant="ghost"
            class="shrink-0"
            @click="clearAllFilters"
          />
        </div>
      </div>
    </Teleport>

    <!-- Filter drawer (bottom sheet) -->
    <AppDrawer v-model:open="filtersOpen" :title="$t('pages.products.filters.toggle')" icon="i-lucide-sliders-horizontal">
        <div class="space-y-5 pb-2">
          <FilterChipGroup
            v-model="expirationFilter"
            :label="$t('pages.products.filterLabels.expiration')"
            :options="expirationOptions"
          />
          <FilterChipGroup
            v-model="scopeFilter"
            :label="$t('pages.products.filterLabels.scope')"
            :options="scopeOptions"
          />
          <FilterChipGroup
            v-model="eatableFilter"
            :label="$t('pages.products.filterLabels.eatable')"
            :options="eatableOptions"
          />
          <FilterChipGroup
            v-model="groupBy"
            :label="$t('pages.products.filterLabels.groupBy')"
            :options="groupByOptions"
          />

          <!-- Boolean property toggles -->
          <div role="group" :aria-label="$t('pages.products.filterLabels.properties')">
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
              {{ $t('pages.products.filterLabels.properties') }}
            </p>
            <div class="flex flex-wrap gap-2">
              <UButton
                :label="$t('pages.products.filters.favorites')"
                icon="i-lucide-star"
                size="sm"
                class="rounded-full"
                :color="favoritesFilter === 'favorites' ? 'primary' : 'neutral'"
                :variant="favoritesFilter === 'favorites' ? 'solid' : 'outline'"
                :aria-pressed="favoritesFilter === 'favorites'"
                @click="() => { favoritesFilter = favoritesFilter === 'favorites' ? 'all' : 'favorites' }"
              />
              <UButton
                :label="$t('pages.products.filterLabels.barcode')"
                icon="i-lucide-barcode"
                size="sm"
                class="rounded-full"
                :color="barcodeFilter === 'withBarcode' ? 'primary' : 'neutral'"
                :variant="barcodeFilter === 'withBarcode' ? 'solid' : 'outline'"
                :aria-pressed="barcodeFilter === 'withBarcode'"
                @click="() => { barcodeFilter = barcodeFilter === 'withBarcode' ? 'all' : 'withBarcode' }"
              />
            </div>
          </div>

          <!-- Stock quantity range -->
          <div>
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
                {{ $t('pages.products.filterLabels.quantity') }}
              </p>
              <span class="text-sm text-gray-500 dark:text-gray-400 tabular-nums">
                {{ quantityRange[0] }} – {{ quantityRange[1] }}
              </span>
            </div>
            <USlider
              v-model="quantityRange"
              :min="0"
              :max="maxStockQuantity"
              :step="1"
              color="primary"
            />
          </div>
        </div>

      <template #footer>
        <div class="flex items-center gap-2 w-full">
          <UButton
            :label="$t('pages.products.filters.clearAll')"
            color="neutral"
            variant="ghost"
            size="lg"
            :disabled="activeFilterCount === 0"
            @click="clearAllFilters"
          />
          <UButton
            class="flex-1"
            size="lg"
            color="primary"
            :label="$t('pages.products.filters.showResults', { count: filteredProducts.length })"
            @click="() => { filtersOpen = false }"
          />
        </div>
      </template>
    </AppDrawer>

    <!-- Content Section. Extra right padding while the index rail is up, so it overlays the
         gutter rather than the cards. -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6" :class="showIndexRail ? 'pr-9 sm:pr-12 lg:pr-16' : ''">

    <PullToRefreshIndicator
      :pull-distance="pullDistance"
      :is-pulling="isPulling"
      :is-refreshing="isRefreshing"
      :is-ready="isReady"
    />

    <RealtimeConnectionBar class="mb-4" />

    <!-- "What changed while you were away" (#127). Hosted here, on the screen the app opens to,
         rather than in the layout: the card's lifetime should be this visit to this screen, and a
         layout-level host would keep it on screen across every navigation for the rest of the
         session. It renders nothing unless something actually happened while the user was away,
         and issues no request at all for a short gap - see useAwayDelta. -->
    <AwayDeltaCard v-if="awayDelta" :delta="awayDelta" class="mb-4" @acknowledge="acknowledgeAwayDelta" />

    <!-- Loading State — first load only. A pull-to-refresh, a socket reconnect
         or a filter change keeps the grid mounted (PullToRefreshIndicator gives
         the feedback); swapping it out would remount every card and replay the
         bubble animation, and would swallow the leave animation of a card
         removed in the same tick. -->
    <div v-if="isLoading && !hasLoaded" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6 gap-4">
      <SkeletonCard v-for="i in 8" :key="i" :lines="2" />
    </div>

    <template v-else>
      <!-- Empty state — rendered next to the (then empty) grid, never in place
           of it. `allProducts` decides which of the two it is: an inventory that
           has never had anything in it gets the call to action, one that is only
           filtered down to nothing gets "clear filters". -->
      <EmptyState
        v-if="filteredProducts.length === 0 && !isLoading"
        :illustration="hasNoInventory ? 'products' : 'search'"
        :title="hasNoInventory ? $t('pages.products.noInventory') : $t('pages.products.noResultsTitle')"
        :description="hasNoInventory ? $t('pages.products.noInventoryHint') : $t('pages.products.tryDifferentSearch')"
        :action-label="hasNoInventory ? $t('pages.products.addProductButton') : $t('common.filters.clear')"
        :action-icon="hasNoInventory ? 'i-lucide-plus' : 'i-lucide-filter-x'"
        @action="onEmptyStateAction"
      />

      <!-- Products grid. Ungrouped it is one flat AnimatedList, as before; grouped it is one
           list per section under a sticky header. Separate lists on purpose — a header inside a
           TransitionGroup would join the cards' FLIP animation. -->
      <AnimatedList v-if="sections.length === 0" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6 gap-4">
        <div
          v-for="entry in displayedProductsView"
          :key="entry.product.publicId"
          class="relative rounded-2xl"
          :class="{ 'row-updated-flash': entry.updated }"
        >
          <DetailedProductCard
            :product="entry.product"
            :search-query="searchQuery"
            @select="openOverview"
          />
          <span v-if="entry.updated" class="sr-only">{{ $t('realtime.updatedFlash') }}</span>
        </div>
      </AnimatedList>

      <template v-else>
        <section v-for="section in displayedSectionsView" :key="section.key" :aria-labelledby="`section-${section.key}`">
          <!-- Sticky under the app header, whose measured height is published as
               --app-header-height. -->
          <h2
            :id="`section-${section.key}`"
            class="sticky z-20 -mx-1 mb-2 flex items-baseline gap-2 bg-default/95 px-1 py-2 backdrop-blur"
            :style="{ top: 'calc(var(--app-header-height, 5.5rem) - 0.25rem)' }"
          >
            <span class="text-sm font-bold uppercase tracking-wide text-highlighted">{{ section.label }}</span>
            <span class="text-xs text-muted tabular-nums">{{ section.count }}</span>
          </h2>

          <AnimatedList class="mb-4 grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6 gap-4">
            <div
              v-for="entry in section.items"
              :key="entry.product.publicId"
              class="relative rounded-2xl"
              :class="{ 'row-updated-flash': entry.updated }"
            >
              <DetailedProductCard
                :product="entry.product"
                :search-query="searchQuery"
                @select="openOverview"
              />
              <span v-if="entry.updated" class="sr-only">{{ $t('realtime.updatedFlash') }}</span>
            </div>
          </AnimatedList>
        </section>
      </template>
    </template>

    <!-- Sentinel for intersection observer -->
    <div v-if="hasMoreProducts" ref="sentinelRef" class="w-full min-h-[1px]">
      <!-- Loading skeletons while loading more -->
      <div v-if="loadingMore" class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6 gap-4 mt-4">
        <SkeletonCard v-for="i in 8" :key="i" :lines="2" />
      </div>
    </div>
    </div>

    <!-- Fast-scroll index. Built from every section, not the rendered ones, so it can reach a
         group the incremental renderer has not got to yet. -->
    <SectionIndexRail
      :sections="railSections"
      :min-sections="MIN_INDEX_SECTIONS"
      :aria-label="$t('pages.products.sectionIndex')"
      top="calc(var(--app-header-height, 5.5rem) + 1rem)"
      bottom="7rem"
      @select="jumpToSection"
    />

    <!-- Barcode Scanner Modal -->
    <BarcodeScannerModal :on-barcode-detected="handleBarcodeScanned" />

    <!-- Add Inventory Wizard (bottom-sheet) -->
    <AddInventoryItemModal v-model:open="isAddInventoryOpen" @created="handleInventoryCreated" />

    <!-- Inventory overview (bottom-sheet), opened by tapping a card -->
    <InventoryOverviewDrawer v-model:open="isOverviewOpen" :product-public-id="overviewProductId" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, watch, onBeforeUnmount } from 'vue'
import { useProductsApi } from '../../composables/api/useProductsApi'
import type {
  InventoryGridProductInfo,
  InventoryUpsertedEvent,
  InventoryDeletedEvent,
  ProductDeletedEvent,
  ProductFavoriteChangedEvent
} from '../../types/product'
import { ProductCategoryGroup } from '../../types/enums'
import { normalizeForSearch } from '../../utils/stringUtils'
import { getProductCategoryGroup, PRODUCT_CATEGORY_GROUP_ORDER } from '../../utils/productCategoryGroups'
import { useCameraAvailability } from '../../composables/useCameraAvailability'
import { useInventorySocket } from '../../composables/useInventorySocket'
import { useEventBus } from '../../composables/useEventBus'
import { useAwayDelta } from '../../composables/useAwayDelta'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

// "What changed while you were away" (#127). The composable owns the gap decision, the request and
// the stored last-seen; this page only hosts the card. Deliberately called here at setup so its
// own onMounted hook registers - it never awaits anything on the boot path (see useAwayDelta).
const { delta: awayDelta, acknowledge: acknowledgeAwayDelta } = useAwayDelta()

/**
 * How the grid is grouped. `none` keeps the flat, urgency-ordered list this page has always had.
 *
 * Storage location is deliberately not offered, though the issue that asked for this listed it: it
 * is not in the grid's payload (`InventoryGridItemInfo` omits it on purpose) and a product with
 * items in several locations has no single one to group under.
 */
type GroupBy = 'none' | 'name' | 'category'

const GROUP_BY_VALUES: string[] = ['none', 'name', 'category']

/** Fewer sections than this and the index rail is more chrome than help. */
const MIN_INDEX_SECTIONS = 5

interface ProductSection {
  /** Stable across re-renders — it is the DOM id of the sticky header, and the rail's key. */
  key: string
  /** Full name, for the header and the rail's bubble. */
  label: string
  /** One or two characters, for the rail itself. */
  tick: string
  /** Sort position; ties are broken on the label. */
  order: number
  items: InventoryGridProductInfo[]
}

const { getDetailedProducts } = useProductsApi()
const { isExpired: checkIsExpired, isExpiringSoon: checkIsExpiringSoon } = useExpirationCheck()
const { t: $t, locale: $locale } = useI18n()
const { showCameraButton } = useCameraAvailability()
// The scanner overlay is already mounted on this page (BarcodeScannerModal); this is
// only how the "Scan barcode" app shortcut opens it (#118).
const { openScanner } = useBarcodeScanner()
const inventorySocket = useInventorySocket()
const eventBus = useEventBus()
// Shared with InventoryOverviewDrawer / InventoryItemRow's optimistic delete + move — see
// useUndoableAction.ts. This grid never itself starts a pending action, but its own realtime
// handlers below must still yield to one started elsewhere (e.g. from the product's own overview
// drawer, open on top of this same grid).
const { isPendingEntity } = useUndoableAction()

// Persistent header (auth layout) — page identity + info popover.
usePageHeader(() => ({
  icon: 'i-lucide-package',
  title: $t('pages.products.title'),
  info: $t('pages.products.description'),
  hasSearch: true
}))

// The add-inventory wizard (bottom-sheet) opened from the nav FAB.
const isAddInventoryOpen = ref(false)

// The inventory-overview bottom-sheet, opened by tapping a product card.
const isOverviewOpen = ref(false)
const overviewProductId = ref<string | null>(null)
const openOverview = (publicId: string) => {
  overviewProductId.value = publicId
  isOverviewOpen.value = true
}

// Register the page's add-action(s) on the dynamic nav FAB.
useFabActions(() => [
  {
    label: $t('pages.products.addProductButton'),
    icon: 'i-lucide-plus',
    handler: () => { isAddInventoryOpen.value = true }
  }
])

// Manifest app shortcuts land here with an action already chosen (#118): "Add item"
// and "Scan barcode" both open this page, so the shortcut has to do what the FAB and
// the camera button would have done. `useDeepLinkAction` strips the parameter, so a
// later back-navigation to /products does not reopen the drawer.
useDeepLinkAction({
  add: () => { isAddInventoryOpen.value = true },
  scan: () => openScanner()
})

const { pullDistance, isPulling, isRefreshing, isReady } = usePullToRefresh(() => loadProducts())

// LocalStorage key for filter settings (single consolidated object)
const FILTERS_KEY = 'productsFilters'

// State
const allProducts = ref<InventoryGridProductInfo[]>([])
const isLoading = ref(false)
// Distinguishes the first load (skeletons) from a refetch (keep the grid mounted
// so the bubble animation is not replayed for every card).
const hasLoaded = ref(false)
const searchQuery = ref('')
const filtersOpen = ref(false)

// "Show all N in Inventory" in the command palette (#111) lands here with the term it was
// searched with, so the grid opens already filtered to what the reader was looking at.
useSearchHandoff({
  search: (term) => { searchQuery.value = term }
})

// Independent, individually-combinable filters (values validated on load/use)
const expirationFilter = ref('all')
const eatableFilter = ref('all')
const favoritesFilter = ref('all')
const barcodeFilter = ref('all')
const scopeFilter = ref('all')
const minQuantity = ref<number | null>(null)
const maxQuantity = ref<number | null>(null)
const groupBy = ref<GroupBy>('none')

// Pagination state
const currentPage = ref(1)
const pageSize = 20
const loadingMore = ref(false)
const sentinelRef = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

// Filter dropdown options
const expirationOptions = computed(() => [
  { label: $t('pages.products.filters.all'), value: 'all' },
  { label: $t('pages.products.filters.expired'), value: 'expired' },
  { label: $t('pages.products.filters.expiringSoon'), value: 'expiringSoon' }
])

const groupByOptions = computed(() => [
  { label: $t('pages.products.groupBy.none'), value: 'none' },
  { label: $t('pages.products.groupBy.name'), value: 'name' },
  { label: $t('pages.products.groupBy.category'), value: 'category' }
])

const eatableOptions = computed(() => [
  { label: $t('pages.products.filters.all'), value: 'all' },
  { label: $t('pages.products.filters.eatable'), value: 'eatable' },
  { label: $t('pages.products.filters.notEatable'), value: 'notEatable' }
])

// Scope filter options (family vs personal)
const scopeOptions = computed(() => [
  { label: $t('pages.products.scopeFilters.all'), value: 'all' },
  { label: $t('pages.products.scopeFilters.family'), value: 'family' },
  { label: $t('pages.products.scopeFilters.personal'), value: 'personal' }
])

// Coerce a value to a non-negative number, or null when empty/invalid
const normalizeQty = (v: unknown): number | null => {
  if (v === '' || v === null || v === undefined) return null
  const n = Number(v)
  return Number.isFinite(n) && n >= 0 ? n : null
}

// Largest single-product total stock — upper bound for the quantity slider
const maxStockQuantity = computed(() => {
  const max = allProducts.value.reduce((m, p) => Math.max(m, totalQuantity(p)), 0)
  return Math.max(1, Math.ceil(max))
})

// Slider range proxy: full range [0, max] means "no quantity filter"
const quantityRange = computed<number[]>({
  get: () => [minQuantity.value ?? 0, maxQuantity.value ?? maxStockQuantity.value],
  set: (range: number[]) => {
    const [lo, hi] = range
    minQuantity.value = lo == null || lo <= 0 ? null : lo
    maxQuantity.value = hi == null || hi >= maxStockQuantity.value ? null : hi
  }
})

// Number of active (non-default) filters, shown as a badge on the filter toggle
const activeFilterCount = computed(() =>
  (expirationFilter.value !== 'all' ? 1 : 0) +
  (eatableFilter.value !== 'all' ? 1 : 0) +
  (favoritesFilter.value !== 'all' ? 1 : 0) +
  (barcodeFilter.value !== 'all' ? 1 : 0) +
  (scopeFilter.value !== 'all' ? 1 : 0) +
  (minQuantity.value != null || maxQuantity.value != null ? 1 : 0)
)

const optLabel = (options: { label: string, value: string }[], value: string) =>
  options.find(o => o.value === value)?.label ?? value

// Active filters as dismissible chips shown under the search box
const activeFilters = computed(() => {
  const chips: { key: string, label: string, clear: () => void }[] = []
  if (expirationFilter.value !== 'all') {
    chips.push({ key: 'expiration', label: optLabel(expirationOptions.value, expirationFilter.value), clear: () => { expirationFilter.value = 'all' } })
  }
  if (scopeFilter.value !== 'all') {
    chips.push({ key: 'scope', label: optLabel(scopeOptions.value, scopeFilter.value), clear: () => { scopeFilter.value = 'all' } })
  }
  if (eatableFilter.value !== 'all') {
    chips.push({ key: 'eatable', label: optLabel(eatableOptions.value, eatableFilter.value), clear: () => { eatableFilter.value = 'all' } })
  }
  if (favoritesFilter.value === 'favorites') {
    chips.push({ key: 'favorites', label: $t('pages.products.filters.favorites'), clear: () => { favoritesFilter.value = 'all' } })
  }
  if (barcodeFilter.value === 'withBarcode') {
    chips.push({ key: 'barcode', label: $t('pages.products.filterLabels.barcode'), clear: () => { barcodeFilter.value = 'all' } })
  }
  if (minQuantity.value != null || maxQuantity.value != null) {
    chips.push({
      key: 'quantity',
      label: `${$t('pages.products.filterLabels.quantity')}: ${minQuantity.value ?? 0}–${maxQuantity.value ?? maxStockQuantity.value}`,
      clear: () => { minQuantity.value = null; maxQuantity.value = null }
    })
  }
  return chips
})

const clearAllFilters = () => {
  expirationFilter.value = 'all'
  eatableFilter.value = 'all'
  favoritesFilter.value = 'all'
  barcodeFilter.value = 'all'
  scopeFilter.value = 'all'
  minQuantity.value = null
  maxQuantity.value = null
}

/**
 * True when the inventory itself is empty, as opposed to filtered down to
 * nothing — the two empty states say different things and offer different
 * actions. The search box is part of the filter set here, so a search that
 * matches nothing still counts as "filtered", not "empty".
 */
const hasNoInventory = computed(() => allProducts.value.length === 0)

const onEmptyStateAction = () => {
  if (hasNoInventory.value) isAddInventoryOpen.value = true
  else clearAllFilters()
}

// All filter values as one object — drives persistence and pagination reset
const filterState = computed(() => ({
  expiration: expirationFilter.value,
  eatable: eatableFilter.value,
  favorites: favoritesFilter.value,
  barcode: barcodeFilter.value,
  scope: scopeFilter.value,
  minQuantity: minQuantity.value,
  maxQuantity: maxQuantity.value,
  groupBy: groupBy.value
}))

// Computed - client-side filtering
const filteredProducts = computed(() => {
  let result = allProducts.value

  // Text search filter
  if (searchQuery.value.trim()) {
    const normalized = normalizeForSearch(searchQuery.value)
    result = result.filter(product =>
      normalizeForSearch(product.name).includes(normalized) ||
      normalizeForSearch(product.brand).includes(normalized) ||
      normalizeForSearch(product.barcode).includes(normalized)
    )
  }

  // Independent filters — all AND-combined

  // Expiration status
  if (expirationFilter.value === 'expired') {
    result = result.filter(product => hasExpiredItems(product))
  } else if (expirationFilter.value === 'expiringSoon') {
    result = result.filter(product => hasExpiringSoonItems(product))
  }

  // Eatable
  if (eatableFilter.value === 'eatable') {
    result = result.filter(product => product.isEatable)
  } else if (eatableFilter.value === 'notEatable') {
    result = result.filter(product => !product.isEatable)
  }

  // Favorites
  if (favoritesFilter.value === 'favorites') {
    result = result.filter(product => product.isFavorite)
  }

  // Barcode
  if (barcodeFilter.value === 'withBarcode') {
    result = result.filter(product => !!product.barcode && product.barcode.trim() !== '')
  }

  // Scope (family vs personal)
  if (scopeFilter.value === 'family') {
    result = result.filter(product => hasFamilyItems(product))
  } else if (scopeFilter.value === 'personal') {
    result = result.filter(product => hasPersonalItems(product))
  }

  // Stock-quantity range (unit-agnostic total; see totalQuantity)
  if (minQuantity.value != null || maxQuantity.value != null) {
    result = result.filter(product => {
      const total = totalQuantity(product)
      if (minQuantity.value != null && total < minQuantity.value) return false
      if (maxQuantity.value != null && total > maxQuantity.value) return false
      return true
    })
  }

  // Sort products by urgency and then alphabetically
  const expiredProducts: InventoryGridProductInfo[] = []
  const expiringSoonProducts: InventoryGridProductInfo[] = []
  const otherProducts: InventoryGridProductInfo[] = []

  result.forEach(product => {
    if (hasExpiredItems(product)) {
      expiredProducts.push(product)
    } else if (hasExpiringSoonItems(product)) {
      expiringSoonProducts.push(product)
    } else {
      otherProducts.push(product)
    }
  })

  // Sort each category alphabetically
  const sortAlphabetically = (a: InventoryGridProductInfo, b: InventoryGridProductInfo) => {
    const nameA = a.name.toLowerCase()
    const nameB = b.name.toLowerCase()
    return nameA.localeCompare(nameB, 'hu')
  }

  expiredProducts.sort(sortAlphabetically)
  expiringSoonProducts.sort(sortAlphabetically)
  otherProducts.sort(sortAlphabetically)

  // Concatenate in priority order
  return [...expiredProducts, ...expiringSoonProducts, ...otherProducts]
})

// --- Sections -----------------------------------------------------------------------------------
//
// Grouping is computed over the **whole** filtered list, never the rendered slice: the index rail
// has to be able to point at a group the incremental renderer has not reached, and a rail built
// from what happens to be on screen would grow as you scrolled.

/**
 * The sections, in the order they are shown. Empty when not grouping — which is how the template
 * decides between one flat grid and a grid per section.
 *
 * Within a section the products keep `filteredProducts`' order (urgency, then alphabetical), so
 * grouping changes where a card sits, not how the list ranks what is inside a group.
 */
const sections = computed<ProductSection[]>(() => {
  if (groupBy.value === 'none') return []

  const buckets = new Map<string, ProductSection>()

  for (const product of filteredProducts.value) {
    const bucket = groupBy.value === 'name' ? nameBucket(product) : categoryBucket(product)
    const existing = buckets.get(bucket.key)

    if (existing) existing.items.push(product)
    else buckets.set(bucket.key, { ...bucket, items: [product] })
  }

  // `order` puts the category groups in the pickers' order and pushes the name buckets' "#" last;
  // the label breaks the remaining ties, which is every letter bucket.
  return [...buckets.values()].sort((a, b) =>
    a.order - b.order || a.label.localeCompare(b.label, $locale.value)
  )
})

/** First letter, uppercased. Digits and symbols share one bucket, sorted after the letters. */
function nameBucket(product: InventoryGridProductInfo): Omit<ProductSection, 'items'> {
  const initial = (product.name.trim()[0] ?? '').toLocaleUpperCase($locale.value)
  const isLetter = /\p{L}/u.test(initial)

  return isLetter
    ? { key: `name-${initial}`, label: initial, tick: initial, order: 0 }
    : { key: 'name-other', label: '#', tick: '#', order: 1 }
}

/**
 * The product's `ProductCategoryGroup`. Presentation-only — the API stores the category, not the
 * group — and a product with no category lands in `Other`, which is where the enum puts it too.
 */
function categoryBucket(product: InventoryGridProductInfo): Omit<ProductSection, 'items'> {
  const group = product.category == null
    ? ProductCategoryGroup.Other
    : getProductCategoryGroup(product.category) ?? ProductCategoryGroup.Other
  const label = $t(`enums.productCategoryGroup.${group}`)

  return {
    key: `category-${group}`,
    label,
    tick: label.slice(0, 2),
    order: PRODUCT_CATEGORY_GROUP_ORDER.indexOf(group)
  }
}

/** What the rail shows: every section, rendered or not. */
const railSections = computed(() =>
  sections.value.map(({ key, label, tick }) => ({ key, label, tick }))
)

/** Whether the rail is up — the content gutter widens to match, so it never covers a card. */
const showIndexRail = computed(() => railSections.value.length >= MIN_INDEX_SECTIONS)

/** The flat order the incremental renderer pages through — the grouped one while grouping. */
const orderedProducts = computed(() =>
  groupBy.value === 'none'
    ? filteredProducts.value
    : sections.value.flatMap(section => section.items)
)

// Paginated products for display (lazy loading)
const displayedProducts = computed(() => {
  const startIndex = 0
  const endIndex = currentPage.value * pageSize
  return orderedProducts.value.slice(startIndex, endIndex)
})

/**
 * The sections trimmed to what is rendered. `count` stays the section's real size, so a header does
 * not count up as you scroll into it.
 */
const displayedSections = computed(() => {
  const limit = currentPage.value * pageSize
  const out: { key: string, label: string, count: number, items: InventoryGridProductInfo[] }[] = []
  let taken = 0

  for (const section of sections.value) {
    if (taken >= limit) break

    const items = section.items.slice(0, limit - taken)
    taken += items.length
    out.push({ key: section.key, label: section.label, count: section.items.length, items })
  }

  return out
})

const hasMoreProducts = computed(() => {
  return displayedProducts.value.length < orderedProducts.value.length
})

// --- Reconnect "updated" flash -----------------------------------------------------------------
// On reconnect the grid is refetched wholesale (see handleInventoryReconnected below); this flashes
// the cards whose payload actually changed while the socket was down. Neutral, not member-coloured
// — see .row-updated-flash in main.css — since this is the server catching the client up, not any
// one person's edit (there is no per-item "changed by" attribution on this grid).
const rowUpdates = ref<Set<string>>(new Set())
const rowUpdateTimers = new Map<string, ReturnType<typeof setTimeout>>()
// Keep in sync with --attribution-flash in main.css (same "a card briefly needs your eye" duration).
const ROW_UPDATED_FLASH_MS = 1500

const flashUpdatedRow = (publicId: string) => {
  rowUpdates.value = new Set(rowUpdates.value).add(publicId)
  const existingTimer = rowUpdateTimers.get(publicId)
  if (existingTimer) clearTimeout(existingTimer)
  rowUpdateTimers.set(publicId, setTimeout(() => {
    rowUpdateTimers.delete(publicId)
    const next = new Set(rowUpdates.value)
    next.delete(publicId)
    rowUpdates.value = next
  }, ROW_UPDATED_FLASH_MS))
}

/** `displayedProducts` / `displayedSections`, each entry paired with whether it just got flashed. */
const withUpdateFlag = (products: InventoryGridProductInfo[]) =>
  products.map(product => ({ product, updated: rowUpdates.value.has(product.publicId) }))

const displayedProductsView = computed(() => withUpdateFlag(displayedProducts.value))

const displayedSectionsView = computed(() => displayedSections.value.map(section => ({
  ...section,
  items: withUpdateFlag(section.items)
})))

/**
 * Jumps to a section, rendering however much of the list it takes to get there first.
 *
 * That render-ahead is the point: the rail lists every group, so picking one past the rendered
 * slice has to reveal it rather than scroll to nothing. It only ever grows the rendered range, so
 * scrolling back up afterwards costs nothing.
 */
async function jumpToSection(key: string) {
  const index = sections.value.findIndex(section => section.key === key)
  if (index === -1) return

  let offset = 0
  for (let i = 0; i < index; i++) offset += sections.value[i]!.items.length

  const pagesNeeded = Math.ceil((offset + 1) / pageSize)
  if (currentPage.value < pagesNeeded) currentPage.value = pagesNeeded

  await nextTick()

  const header = document.getElementById(`section-${key}`)
  if (!header) return

  // Not scrollIntoView: the app header is fixed, so a section scrolled to the top of the viewport
  // would sit underneath it.
  const headerHeight = parseFloat(
    getComputedStyle(document.documentElement).getPropertyValue('--app-header-height')
  ) || 88

  window.scrollTo({
    top: header.getBoundingClientRect().top + window.scrollY - headerHeight - 8,
    behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth'
  })
}

// Helper function to check if product has expired items
const hasExpiredItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      return checkIsExpired(item.expirationAt)
    } catch {
      return false
    }
  })
}

// Helper function to check if product has items expiring soon (within 2 weeks)
const hasExpiringSoonItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => {
    if (!item.expirationAt) return false
    try {
      return checkIsExpiringSoon(item.expirationAt)
    } catch {
      return false
    }
  })
}

// Scope helpers: a product matches "family" if it has any shared item, "personal"
// if it has any personal item. A product with both kinds matches both scopes.
const hasFamilyItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => item.isSharedWithFamily === true)
}

const hasPersonalItems = (product: InventoryGridProductInfo): boolean => {
  return product.inventoryItems.some(item => item.isSharedWithFamily === false)
}

// Total stock across all items. Items may use different units (pcs, g, l), so this
// sum is unit-agnostic and only approximate when a product mixes units.
const totalQuantity = (product: InventoryGridProductInfo): number => {
  return product.inventoryItems.reduce((sum, item) => sum + item.currentQuantity, 0)
}

// Methods
// Load the grid: prefer the realtime snapshot (only card-needed fields), fall back to REST
// (SSR or socket down), mapping the heavier DTO down to the light grid shape.
const loadProducts = async () => {
  isLoading.value = true
  try {
    const snapshot = await inventorySocket.joinInventory()
    if (snapshot) {
      allProducts.value = snapshot
      return
    }

    const response = await getDetailedProducts({ returnAll: true })
    if (response.success && response.data) {
      allProducts.value = response.data.items.map(p => ({
        publicId: p.publicId,
        name: p.name,
        brand: p.brand,
        barcode: p.barcode,
        isEatable: p.isEatable,
        isFavorite: p.isFavorite,
        inventoryItems: p.inventoryItems.map(i => ({
          publicId: i.publicId,
          productPublicId: p.publicId,
          currentQuantity: i.currentQuantity,
          unit: i.unit,
          expirationAt: i.expirationAt,
          isSharedWithFamily: i.isSharedWithFamily
        }))
      }))
    }
  } finally {
    isLoading.value = false
    hasLoaded.value = true
  }
}

// Reload only if the socket is down — a connected client receives its own change back over the
// socket and patches in place, so no full refetch is needed.
const handleInventoryCreated = () => {
  if (!inventorySocket.isConnected.value) loadProducts()
}

/**
 * Re-syncs the grid after a dropped-then-recovered connection, then flashes whichever cards'
 * payload actually differs from what was on screen right before the refetch (by product public
 * id) — a full-payload comparison rather than tracking individual fields, since neither
 * `InventoryGridProductInfo` nor its items carry a version/updatedAt to diff more cheaply, and the
 * grid is small enough that this is cheap regardless.
 */
const handleInventoryReconnected = async () => {
  const before = new Map(allProducts.value.map(p => [p.publicId, JSON.stringify(p)]))
  await loadProducts()
  for (const product of allProducts.value) {
    if (before.get(product.publicId) !== JSON.stringify(product)) flashUpdatedRow(product.publicId)
  }
}

// --- Realtime patch handlers: mutate allProducts in place instead of refetching ---

const handleRealtimeInventoryUpserted = (payload: InventoryUpsertedEvent) => {
  // A local optimistic delete/move (started from this product's own overview drawer, or another
  // tab) is still pending for this item — an echo of the pre-change server state must not fight
  // it. The commit's own resolution is what reconciles once the window closes.
  if (isPendingEntity(payload.item.publicId)) return
  const { product, item } = payload
  const existing = allProducts.value.find(p => p.publicId === product.publicId)
  if (!existing) {
    // A product enters the grid only once it has an in-scope item.
    allProducts.value.push({ ...product, inventoryItems: [item] })
  } else {
    const idx = existing.inventoryItems.findIndex(i => i.publicId === item.publicId)
    if (idx >= 0) existing.inventoryItems.splice(idx, 1, item)
    else existing.inventoryItems.push(item)
  }
  eventBus.emit('inventory:updated')
}

const handleRealtimeInventoryDeleted = (payload: InventoryDeletedEvent) => {
  // See handleRealtimeInventoryUpserted above — a pending optimistic change on this item wins.
  if (isPendingEntity(payload.itemPublicId)) return
  const product = allProducts.value.find(p => p.publicId === payload.productPublicId)
  if (!product) return
  product.inventoryItems = product.inventoryItems.filter(i => i.publicId !== payload.itemPublicId)
  // A product with no in-scope inventory left is no longer in the grid.
  if (product.inventoryItems.length === 0) {
    allProducts.value = allProducts.value.filter(p => p.publicId !== payload.productPublicId)
  }
  eventBus.emit('inventory:deleted')
}

const handleRealtimeProductUpdated = (product: InventoryGridProductInfo) => {
  const existing = allProducts.value.find(p => p.publicId === product.publicId)
  if (!existing) return // no in-scope inventory → not shown in the grid
  // Catalog fields only — preserve local items and the per-user favorite flag.
  existing.name = product.name
  existing.brand = product.brand
  existing.barcode = product.barcode
  existing.isEatable = product.isEatable
}

const handleRealtimeProductFavoriteChanged = (payload: ProductFavoriteChangedEvent) => {
  const existing = allProducts.value.find(p => p.publicId === payload.publicId)
  if (existing) existing.isFavorite = payload.isFavorite
}

const handleRealtimeProductDeleted = (payload: ProductDeletedEvent) => {
  allProducts.value = allProducts.value.filter(p => p.publicId !== payload.publicId)
  eventBus.emit('product:deleted')
}

const loadMoreProducts = () => {
  if (loadingMore.value || !hasMoreProducts.value) return

  loadingMore.value = true

  // Simulate loading delay for better UX
  setTimeout(() => {
    currentPage.value++
    loadingMore.value = false
  }, 300)
}

// Barcode scanner handler
const handleBarcodeScanned = (barcode: string) => {
  searchQuery.value = barcode
}

// Watch for filter changes to reset pagination
watch(searchQuery, () => {
  currentPage.value = 1
})

// Reset pagination and persist whenever any filter changes
watch(filterState, (state) => {
  currentPage.value = 1
  localStorage.setItem(FILTERS_KEY, JSON.stringify(state))
})

// Watch for sentinel availability and setup observer
watch(sentinelRef, (newSentinel) => {
  // Disconnect previous observer if exists
  if (observer.value) {
    observer.value.disconnect()
  }

  // Setup new observer if sentinel exists
  if (newSentinel) {
    observer.value = new IntersectionObserver(
      (entries) => {
        const [entry] = entries
        if (entry && entry.isIntersecting && hasMoreProducts.value && !loadingMore.value) {
          loadMoreProducts()
        }
      },
      {
        root: null,
        rootMargin: '100px', // Trigger 100px before reaching bottom
        threshold: 0.1
      }
    )

    observer.value.observe(newSentinel)
  }
})

// Lifecycle
onMounted(() => {
  // Restore filter settings from localStorage, validating each field
  const saved = localStorage.getItem(FILTERS_KEY)

  if (saved) {
    try {
      const parsed = JSON.parse(saved)

      if (['all', 'expired', 'expiringSoon'].includes(parsed.expiration)) {
        expirationFilter.value = parsed.expiration
      }
      if (['all', 'eatable', 'notEatable'].includes(parsed.eatable)) {
        eatableFilter.value = parsed.eatable
      }
      if (['all', 'favorites'].includes(parsed.favorites)) {
        favoritesFilter.value = parsed.favorites
      }
      if (['all', 'withBarcode'].includes(parsed.barcode)) {
        barcodeFilter.value = parsed.barcode
      }
      if (['all', 'family', 'personal'].includes(parsed.scope)) {
        scopeFilter.value = parsed.scope
      }
      if (GROUP_BY_VALUES.includes(parsed.groupBy)) {
        groupBy.value = parsed.groupBy
      }
      minQuantity.value = normalizeQty(parsed.minQuantity)
      maxQuantity.value = normalizeQty(parsed.maxQuantity)
    } catch {
      // Ignore malformed stored filters
    }
  }

  loadProducts()

  // Live updates: patch the grid from server pushes instead of refetching.
  inventorySocket.on('InventoryUpserted', handleRealtimeInventoryUpserted)
  inventorySocket.on('InventoryDeleted', handleRealtimeInventoryDeleted)
  inventorySocket.on('ProductUpdated', handleRealtimeProductUpdated)
  inventorySocket.on('ProductFavoriteChanged', handleRealtimeProductFavoriteChanged)
  inventorySocket.on('ProductDeleted', handleRealtimeProductDeleted)
  inventorySocket.onReconnected(handleInventoryReconnected)
})

// Cleanup on unmount
onBeforeUnmount(() => {
  inventorySocket.off('InventoryUpserted', handleRealtimeInventoryUpserted)
  inventorySocket.off('InventoryDeleted', handleRealtimeInventoryDeleted)
  inventorySocket.off('ProductUpdated', handleRealtimeProductUpdated)
  inventorySocket.off('ProductFavoriteChanged', handleRealtimeProductFavoriteChanged)
  inventorySocket.off('ProductDeleted', handleRealtimeProductDeleted)
  inventorySocket.offReconnected(handleInventoryReconnected)

  if (observer.value) {
    observer.value.disconnect()
  }

  // Drop any pending "updated" flash timeouts so none fire after this page is gone.
  rowUpdateTimers.forEach(timer => clearTimeout(timer))
  rowUpdateTimers.clear()
})
</script>
