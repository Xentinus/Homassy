<template>
  <Teleport to="body">
    <div
      class="shopping-mode fixed inset-0 z-50 flex flex-col bg-default"
      role="dialog"
      aria-modal="true"
      :aria-label="$t('shoppingList.shoppingMode.title')"
    >
      <!-- Header: what is left, how far along, and the way out. Kept to one row of controls:
           anything more is something to mis-tap while walking. -->
      <header class="shrink-0 border-b border-default px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <div class="flex items-center gap-3">
          <UButton
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            size="lg"
            :aria-label="$t('shoppingList.shoppingMode.exit')"
            @click="emit('close')"
          />
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-semibold text-highlighted">{{ listName }}</p>
            <p class="text-xs text-muted tabular-nums">
              {{ $t('shoppingList.shoppingMode.progress', { done: doneCount, total: totalCount }) }}
            </p>
          </div>
          <UIcon
            v-if="wakeLock.isActive.value"
            name="i-lucide-sun"
            class="h-5 w-5 shrink-0 text-amber-500"
            :title="$t('shoppingList.shoppingMode.screenAwake')"
          />
          <slot name="presence" />
        </div>

        <!-- Progress bar. `aria-valuetext` carries the same "3 of 12" the eye gets, because the
             raw percentage is not what anyone standing in a shop wants read out. -->
        <div
          class="mt-3 h-2 w-full overflow-hidden rounded-full bg-elevated"
          role="progressbar"
          :aria-valuemin="0"
          :aria-valuemax="totalCount"
          :aria-valuenow="doneCount"
          :aria-valuetext="$t('shoppingList.shoppingMode.progress', { done: doneCount, total: totalCount })"
        >
          <div
            class="h-full rounded-full bg-primary transition-[width] duration-300 ease-out"
            :style="{ width: `${progressPercent}%` }"
          />
        </div>

        <!-- Running total from the prices the household has actually paid (#128). States the
             unpriced count out loud for the same reason the planning view does: a total that
             silently omits items reads as the cost of the whole trolley. -->
        <div v-if="estimateLines.length || remainingEstimate.unpricedCount > 0" class="mt-2 flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-sm">
          <span v-for="line in estimateLines" :key="line" class="font-semibold tabular-nums text-highlighted">{{ line }}</span>
          <span v-if="remainingEstimate.unpricedCount > 0" class="text-xs text-muted">
            {{ $t('price.estimate.unpriced', { count: remainingEstimate.unpricedCount }) }}
          </span>
        </div>
      </header>

      <!-- The list itself -->
      <div class="min-h-0 flex-1 overflow-y-auto px-3 pb-[max(1rem,env(safe-area-inset-bottom))] pt-3">
        <!-- Everything done: the celebration, then the mode closes itself. -->
        <div v-if="isComplete" class="flex h-full flex-col items-center justify-center gap-4 text-center">
          <div class="shopping-mode-celebrate flex h-24 w-24 items-center justify-center rounded-full bg-primary/15">
            <UIcon name="i-lucide-party-popper" class="h-12 w-12 text-primary" />
          </div>
          <div>
            <p class="text-lg font-bold text-highlighted">{{ $t('shoppingList.shoppingMode.doneTitle') }}</p>
            <p class="mt-1 text-sm text-muted">{{ $t('shoppingList.shoppingMode.doneBody', { count: totalCount }) }}</p>
          </div>
        </div>

        <template v-else>
          <!-- "Buy here" first, exactly as the planning view pins it — the section the user is
               standing in front of belongs at the top of a one-handed screen. -->
          <p v-if="hereRows.length" class="mb-2 flex items-center gap-2 px-1 text-xs font-semibold uppercase tracking-wide text-blue-600 dark:text-blue-400">
            <UIcon name="i-lucide-store" class="h-4 w-4" />
            {{ $t('shoppingList.shoppingMode.hereSection') }}
          </p>

          <AnimatedList class="space-y-2" :stagger="false">
            <ShoppingModeRow
              v-for="row in hereRows"
              :key="row.item.publicId"
              :item="row.item"
              :attribution="row.attribution"
              here
              @toggle="onToggle(row.item)"
            />
          </AnimatedList>

          <p v-if="hereRows.length && restRows.length" class="mb-2 mt-5 flex items-center gap-2 px-1 text-xs font-semibold uppercase tracking-wide text-muted">
            <UIcon name="i-lucide-list" class="h-4 w-4" />
            {{ $t('shoppingList.shoppingMode.restSection') }}
          </p>

          <AnimatedList class="space-y-2" :stagger="false">
            <ShoppingModeRow
              v-for="row in restRows"
              :key="row.item.publicId"
              :item="row.item"
              :attribution="row.attribution"
              @toggle="onToggle(row.item)"
            />
          </AnimatedList>
        </template>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
/**
 * Shopping mode (#131): the shopping list as it is actually used inside a shop.
 *
 * The planning view is built for deciding what to buy — a filter bar, metadata on every card,
 * drawers for editing. None of that survives a trolley in one hand. This is the same list with
 * everything removed except what the next thirty seconds need: what is left to buy, how much of it,
 * and a target big enough to hit while walking. Anything else is one tap away on the row itself.
 *
 * What it does NOT own: the items array, the purchase request, or the undo window. Ticking a row
 * emits the same `purchase-requested` the planning card emits, so the page runs the identical
 * optimistic path and both views stay honest about what has actually been written.
 */
import type { CSSProperties } from 'vue'
import type { ShoppingListItemInfo, PurchaseShoppingListItemRequest } from '~/types/shoppingList'
import type { BestKnownPrice } from '~/types/insights'
import { estimateListTotal } from '~/utils/priceEstimate'
import { formatCurrency } from '~/utils/chart/format'

/** What the page already knows about who last touched a row, reused here unchanged. */
export interface ShoppingModeAttribution {
  name: string
  style: CSSProperties
}

const props = withDefaults(defineProps<{
  /** Every item on the open list, purchased ones included — the progress counter needs the total. */
  items: ShoppingListItemInfo[]
  listName?: string
  /** Ids buyable at the store the user is standing in, from the page's own proximity tracking. */
  hereItemIds?: string[]
  /** Best known price per product public id (#128), for the running total. */
  bestPrices?: Record<string, BestKnownPrice>
  /**
   * Live "changed by" per item public id (#97). Two people shopping the same list see each other's
   * ticks land, which is what stops the same thing going in the trolley twice.
   */
  attributions?: Record<string, ShoppingModeAttribution>
  /** Where the user is standing, recorded on the purchase so the price lands on the right shop. */
  currentStorePublicId?: string
}>(), {
  listName: '',
  hereItemIds: () => [],
  bestPrices: () => ({}),
  attributions: () => ({}),
  currentStorePublicId: undefined
})

const emit = defineEmits<{
  /** The user left, or the list finished and the celebration is over. */
  close: []
  /** A row was ticked — the page owns the optimistic write and the undo window. */
  'purchase-requested': [item: ShoppingListItemInfo, request: PurchaseShoppingListItemRequest]
}>()

const { t, locale } = useI18n()
const haptics = useHaptics()
const wakeLock = useWakeLock()

/** How long the celebration stays up before the mode closes itself. */
const CELEBRATION_MS = 2600

const pendingItems = computed(() => props.items.filter(item => !item.purchasedAt))
const doneCount = computed(() => props.items.length - pendingItems.value.length)
const totalCount = computed(() => props.items.length)
const progressPercent = computed(() =>
  totalCount.value === 0 ? 0 : Math.round((doneCount.value / totalCount.value) * 100))

const hereIds = computed(() => new Set(props.hereItemIds))

/**
 * The one ordering rule of this screen: the aisle order the user set (#113), with the rows they can
 * pick up right here lifted out on top. `sortOrder` is zero on a list nobody has dragged, so that
 * case falls through to the name and behaves exactly as it did before manual order existed.
 */
const sortForShop = (a: ShoppingListItemInfo, b: ShoppingListItemInfo) =>
  a.sortOrder - b.sortOrder || displayName(a).localeCompare(displayName(b), locale.value)

const displayName = (item: ShoppingListItemInfo): string =>
  item.product?.name || item.customName || t('common.unnamed')

const toRow = (item: ShoppingListItemInfo) => ({
  item,
  attribution: props.attributions[item.publicId] ?? null
})

const hereRows = computed(() =>
  pendingItems.value.filter(item => hereIds.value.has(item.publicId)).sort(sortForShop).map(toRow))

const restRows = computed(() =>
  pendingItems.value.filter(item => !hereIds.value.has(item.publicId)).sort(sortForShop).map(toRow))

/** True only once something was actually bought — an empty list is not a finished shop. */
const isComplete = computed(() => totalCount.value > 0 && pendingItems.value.length === 0)

/** The running total covers what is still in front of the user, so it falls as rows are ticked. */
const remainingEstimate = computed(() => estimateListTotal(
  pendingItems.value.map((item) => {
    const best = item.productPublicId ? props.bestPrices[item.productPublicId] : undefined
    return {
      productId: item.productPublicId ?? item.publicId,
      quantity: item.quantity,
      bestPrice: best ? { unitPrice: best.unitPrice, currency: best.currency } : undefined
    }
  })
))

/** One line per currency — there is no exchange rate here, so there is no single figure. */
const estimateLines = computed(() => Object.entries(remainingEstimate.value.totalsByCurrency)
  .sort(([a], [b]) => a.localeCompare(b))
  .map(([currency, total]) => t('price.estimate.approx', {
    amount: formatCurrency(total, currency, locale.value)
  })))

const onToggle = (item: ShoppingListItemInfo) => {
  // Deliberately heavier than the planning view's tap: this is confirmation felt through a coat
  // pocket, by someone who is not looking at the screen.
  haptics.impact()
  emit('purchase-requested', item, {
    shoppingListItemPublicId: item.publicId,
    purchasedAt: new Date().toISOString(),
    shoppingLocationPublicId: props.currentStorePublicId
  })
}

// Finishing the list closes the mode, after the celebration has had its moment. The page leaves
// this timing here on purpose: closing the screen the moment the last row is ticked would cut the
// celebration off mid-animation.
let celebrationTimer: ReturnType<typeof setTimeout> | null = null
watch(isComplete, (complete) => {
  if (!complete) return
  haptics.success()
  celebrationTimer = setTimeout(() => emit('close'), CELEBRATION_MS)
})

// A list emptied by deleting rather than by buying — nothing was accomplished, so there is nothing
// to celebrate, and an in-store screen with no items on it is just a wall.
watch(totalCount, (total) => {
  if (total === 0) emit('close')
}, { immediate: true })

onMounted(() => { void wakeLock.request() })

onBeforeUnmount(() => {
  if (celebrationTimer) clearTimeout(celebrationTimer)
  void wakeLock.release()
})
</script>

<style scoped>
/* The overlay owns the whole screen, so the page behind it must not scroll under the finger. */
.shopping-mode {
  overscroll-behavior: contain;
}

.shopping-mode-celebrate {
  animation: shopping-mode-pop 600ms cubic-bezier(0.22, 1, 0.36, 1);
}

@keyframes shopping-mode-pop {
  0% { transform: scale(0.6); opacity: 0; }
  60% { transform: scale(1.08); opacity: 1; }
  100% { transform: scale(1); opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
  .shopping-mode-celebrate {
    animation: none;
  }
}
</style>
