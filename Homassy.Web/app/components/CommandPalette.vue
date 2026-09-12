<template>
  <UModal
    :open="isOpen"
    :fullscreen="!isDesktop"
    :title="t('search.title')"
    :description="t('search.description')"
    :ui="{ content: 'lg:max-w-2xl' }"
    @update:open="onOpenChange"
  >
    <template #content>
      <UCommandPalette
        v-model:search-term="searchTerm"
        :groups="groups"
        :loading="isSearching"
        :placeholder="t('search.placeholder')"
        :fuse="{ resultLimit: MAX_STATIC_RESULTS }"
        preserve-group-order
        close
        class="h-full lg:h-[min(32rem,70dvh)]"
        @update:open="close"
      >
        <template #empty>
          <div class="flex flex-col items-center gap-2 px-4 py-10 text-center">
            <UIcon
              :name="hasQuery ? 'i-lucide-search-x' : 'i-lucide-search'"
              class="h-8 w-8 text-muted"
            />
            <p class="text-sm font-medium">
              {{ hasQuery ? t('search.noResults', { query: searchTerm.trim() }) : t('search.emptyTitle') }}
            </p>
            <p class="text-xs text-muted max-w-xs">
              {{ hasQuery ? t('search.noResultsHint') : t('search.emptyHint') }}
            </p>
          </div>
        </template>
      </UCommandPalette>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed, watch, onMounted, onUnmounted } from 'vue'
import type { CommandPaletteItem } from '@nuxt/ui'
import { SearchResultKind } from '~/types/search'
import type { SearchResultGroup, SearchResultItem } from '~/types/search'

/**
 * The global command palette (#111).
 *
 * One text field over one server endpoint: products, inventory, shopping lists, locations and
 * automations come back ranked and capped in a single response, next to static "go to …"
 * destinations and two actions. Mounted once, in the authenticated layout — see
 * `useCommandPalette` for the state it reads and `useCommandPaletteShortcut` for `Ctrl/Cmd+K`.
 *
 * A modal at every width, fullscreen below `lg`: that is the "full-height sheet with the
 * keyboard focused" the phone wants, without a second component to keep in step with this one.
 */

/** How many static (navigation / action / recent) rows fuse is allowed to return per group. */
const MAX_STATIC_RESULTS = 20

const { t } = useI18n()
const router = useRouter()
const { mediaUrl } = useMediaUrl()
const { highlightText } = useSearchHighlight()
const { isDesktop } = useBreakpoint()
const {
  isOpen,
  query,
  results,
  recent,
  isSearching,
  close,
  toggle,
  setQuery,
  rememberSearch,
  clearRecent
} = useCommandPalette()

// `Ctrl/Cmd+K` lives here rather than in the layout because this is the component that is
// mounted exactly once — a per-page listener would stack up one handler per navigation.
const onKeydown = (event: KeyboardEvent) => {
  if (event.key !== 'k' && event.key !== 'K') return
  if (!event.metaKey && !event.ctrlKey) return

  // The browser's own "search bookmarks" chord is on the same keys.
  event.preventDefault()
  toggle()
}

onMounted(() => window.addEventListener('keydown', onKeydown))
onUnmounted(() => window.removeEventListener('keydown', onKeydown))

// The palette owns the term, but `UCommandPalette` binds it with v-model, so the write goes
// back through `setQuery` (which is what debounces and sequences the request).
const searchTerm = computed({
  get: () => query.value,
  set: (value: string) => setQuery(value)
})

const hasQuery = computed(() => searchTerm.value.trim().length > 0)

const onOpenChange = (value: boolean) => {
  if (!value) close()
}

// Reopening should not show the last visit's answers under an empty field.
watch(isOpen, (open) => {
  if (!open) setQuery('')
})

/** Icon, group heading and list page for each searchable type — one table, no switch per use. */
const KIND_META: Record<SearchResultKind, { icon: string, labelKey: string, listPath: string }> = {
  [SearchResultKind.Product]: {
    icon: 'i-lucide-package',
    labelKey: 'search.groups.products',
    listPath: '/profile/products'
  },
  [SearchResultKind.InventoryItem]: {
    icon: 'i-lucide-warehouse',
    labelKey: 'search.groups.inventoryItems',
    listPath: '/products'
  },
  [SearchResultKind.ShoppingList]: {
    icon: 'i-lucide-shopping-cart',
    labelKey: 'search.groups.shoppingLists',
    listPath: '/shopping-lists'
  },
  [SearchResultKind.ShoppingLocation]: {
    icon: 'i-lucide-store',
    labelKey: 'search.groups.shoppingLocations',
    listPath: '/profile/shopping-locations'
  },
  [SearchResultKind.StorageLocation]: {
    icon: 'i-lucide-archive',
    labelKey: 'search.groups.storageLocations',
    listPath: '/profile/storage-locations'
  },
  [SearchResultKind.Automation]: {
    icon: 'i-lucide-workflow',
    labelKey: 'search.groups.automations',
    listPath: '/profile/automation'
  }
}

/**
 * Where a hit opens.
 *
 * Two of the six have no page of their own: an inventory item is read on its product's page,
 * and a location or a list is opened by its own list page through a `select` parameter (see
 * `useSearchHandoff`), so the palette never needs a second route for the same entity.
 */
const routeFor = (item: SearchResultItem) => {
  switch (item.kind) {
    case SearchResultKind.Product:
      return { path: `/products/${item.publicId}` }
    case SearchResultKind.InventoryItem:
      return { path: `/products/${item.parentPublicId ?? item.publicId}` }
    case SearchResultKind.ShoppingList:
      return { path: '/shopping-lists', query: { select: item.publicId } }
    case SearchResultKind.ShoppingLocation:
      return { path: '/profile/shopping-locations', query: { select: item.publicId } }
    case SearchResultKind.StorageLocation:
      return { path: '/profile/storage-locations', query: { select: item.publicId } }
    case SearchResultKind.Automation:
      return { path: `/profile/automation/${item.publicId}` }
  }
}

const go = (to: { path: string, query?: Record<string, string> }, remember = true) => {
  if (remember) rememberSearch(searchTerm.value)
  close()
  router.push(to)
}

// `Readonly` throughout: the palette state hands these out through `readonly()`, and nothing
// here mutates a result — it only reads one into a row.
const toPaletteItem = (item: Readonly<SearchResultItem>): CommandPaletteItem => {
  const meta = KIND_META[item.kind]
  const picture = mediaUrl(item.imageUrl)
  const term = searchTerm.value.trim()

  return {
    // The server ranked these, so its own highlight is the honest one: highlight what was
    // actually typed rather than letting fuse re-score rows it never filtered.
    labelHtml: highlightText(item.title, term),
    suffixHtml: item.subtitle ? highlightText(item.subtitle, term) : undefined,
    label: item.title,
    suffix: item.subtitle ?? undefined,
    ...(picture ? { avatar: { src: picture, alt: item.title } } : { icon: meta.icon }),
    onSelect: () => go(routeFor(item))
  }
}

/**
 * The "show all …" row that closes every non-trivial group. `hasMore` means the server stopped
 * counting, so the row says "show all" without a number rather than quoting a floor as a total.
 */
const showAllItem = (group: Readonly<Omit<SearchResultGroup, 'items'>>): CommandPaletteItem => {
  const meta = KIND_META[group.kind]
  const label = group.hasMore
    ? t('search.showAll', { page: t(meta.labelKey) })
    : t('search.showAllCount', { count: group.totalCount, page: t(meta.labelKey) })

  return {
    label,
    icon: 'i-lucide-arrow-right',
    onSelect: () => go({ path: meta.listPath, query: { search: searchTerm.value.trim() } })
  }
}

const resultGroups = computed(() => {
  const groups = results.value?.groups ?? []

  return groups.map(group => ({
    id: `results-${group.kind}`,
    label: t(KIND_META[group.kind].labelKey),
    // The server already ranked and capped these; fuse must not filter them again.
    ignoreFilter: true,
    items: [
      ...group.items.map(toPaletteItem),
      ...(group.totalCount > group.items.length ? [showAllItem(group)] : [])
    ]
  }))
})

/** Destinations, filtered by fuse so typing "cal" reaches the calendar without a server hit. */
const navigationGroup = computed(() => ({
  id: 'navigation',
  label: t('search.groups.navigation'),
  items: [
    { label: t('search.goTo', { page: t('nav.calendar') }), icon: 'i-lucide-calendar', onSelect: () => go({ path: '/calendar' }, false) },
    { label: t('search.goTo', { page: t('nav.products') }), icon: 'i-lucide-package', onSelect: () => go({ path: '/products' }, false) },
    { label: t('search.goTo', { page: t('nav.shoppingLists') }), icon: 'i-lucide-shopping-cart', onSelect: () => go({ path: '/shopping-lists' }, false) },
    { label: t('search.goTo', { page: t('activity.title') }), icon: 'i-lucide-activity', onSelect: () => go({ path: '/activity' }, false) },
    { label: t('search.goTo', { page: t('insights.title') }), icon: 'i-lucide-chart-line', onSelect: () => go({ path: '/insights' }, false) },
    { label: t('search.goTo', { page: t('nav.profile') }), icon: 'i-lucide-user', onSelect: () => go({ path: '/profile' }, false) }
  ] satisfies CommandPaletteItem[]
}))

/**
 * The two things worth doing straight from the palette. Both land on the products page with an
 * `?action=`, which is the same mechanism the installed app's shortcuts use — one place decides
 * what "add" and "scan" open.
 */
const actionsGroup = computed(() => ({
  id: 'actions',
  label: t('search.groups.actions'),
  items: [
    {
      label: t('search.actions.addItem'),
      icon: 'i-lucide-plus',
      onSelect: () => go({ path: '/products', query: { action: 'add' } }, false)
    },
    {
      label: t('search.actions.scanBarcode'),
      icon: 'i-lucide-scan-barcode',
      onSelect: () => go({ path: '/products', query: { action: 'scan' } }, false)
    }
  ] satisfies CommandPaletteItem[]
}))

/** This device's recent terms, offered only on the empty field — under a query they are noise. */
const recentGroup = computed(() => {
  if (hasQuery.value || recent.value.length === 0) return null

  return {
    id: 'recent',
    label: t('search.groups.recent'),
    ignoreFilter: true,
    items: [
      ...recent.value.map(term => ({
        label: term,
        icon: 'i-lucide-history',
        onSelect: () => setQuery(term)
      })),
      {
        label: t('search.clearRecent'),
        icon: 'i-lucide-trash-2',
        onSelect: () => clearRecent()
      }
    ] satisfies CommandPaletteItem[]
  }
})

const groups = computed(() => [
  ...(recentGroup.value ? [recentGroup.value] : []),
  ...resultGroups.value,
  navigationGroup.value,
  actionsGroup.value
])
</script>
