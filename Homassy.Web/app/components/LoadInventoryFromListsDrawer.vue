<template>
  <WizardDrawer
    :open="open"
    :title="t('loadInventory.title')"
    icon="i-lucide-clipboard-list"
    :steps="steps"
    :current-step="currentStep"
    :loading="isCommitting"
    override-footer
    @update:open="onOpenChange"
  >
    <template #footer>
      <!-- Selection: cancel, or carry the chosen items into the per-item pass. -->
      <template v-if="phase === 'select'">
        <UButton
          :label="t('common.cancel')"
          color="neutral"
          variant="ghost"
          @click="close"
        />
        <UButton
          :label="t('loadInventory.startButton', { count: selectedIds.size })"
          color="primary"
          trailing-icon="i-lucide-arrow-right"
          :disabled="selectedIds.size === 0"
          @click="startQueue"
        />
      </template>

      <!-- Per item: skipping is a first-class action, not a cancel. -->
      <template v-else-if="phase === 'detail'">
        <UButton
          :label="t('loadInventory.skipButton')"
          color="neutral"
          variant="ghost"
          icon="i-lucide-skip-forward"
          :disabled="isCommitting"
          @click="skipCurrent"
        />
        <UButton
          :label="t('loadInventory.addButton')"
          color="primary"
          icon="i-lucide-package-plus"
          :loading="isCommitting"
          :disabled="!canCommit"
          @click="commitCurrent"
        />
      </template>

      <template v-else>
        <UButton :label="t('common.close')" color="primary" icon="i-lucide-check" @click="close" />
      </template>
    </template>

    <template #default>
      <!-- ============ Selection ============ -->
      <div v-if="phase === 'select'" class="space-y-3">
        <!-- Sticky so the search field and the running count stay reachable while the list
             scrolls: the count is the only feedback that a tap far down the list registered. -->
        <div class="sticky -top-4 z-10 -mx-4 -mt-4 space-y-2 border-b border-default bg-default px-4 pb-2 pt-4">
          <UInput
            v-model="searchQuery"
            icon="i-lucide-search"
            :placeholder="t('loadInventory.searchPlaceholder')"
            class="w-full"
          />
          <div class="flex items-center justify-between text-sm">
            <span class="text-muted">{{ t('loadInventory.selectedCount', { count: selectedIds.size }) }}</span>
            <UButton
              v-if="selectedIds.size > 0"
              :label="t('loadInventory.clearSelection')"
              size="xs"
              color="neutral"
              variant="ghost"
              @click="clearSelection"
            />
          </div>
        </div>

        <div v-if="isLoading" class="space-y-2">
          <SkeletonRow v-for="i in 4" :key="i" />
        </div>

        <EmptyState
          v-else-if="items.length === 0"
          :illustration="searchQuery.trim() ? 'search' : 'shoppingList'"
          :title="searchQuery.trim() ? t('loadInventory.empty.noMatch') : t('loadInventory.empty.title')"
          :description="searchQuery.trim() ? undefined : t('loadInventory.empty.description')"
        />

        <ul v-else class="flex flex-col gap-2">
          <li v-for="item in items" :key="item.publicId">
            <button
              type="button"
              class="w-full rounded-lg border px-3 py-2.5 text-left transition"
              :class="selectedIds.has(item.publicId)
                ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40'
                : 'border-default hover:bg-elevated/50'"
              @click="toggle(item.publicId)"
            >
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0 flex-1">
                  <p class="truncate text-sm font-medium">
                    {{ item.productName }}
                    <span v-if="item.productBrand" class="text-muted">· {{ item.productBrand }}</span>
                  </p>
                  <p class="mt-0.5 truncate text-xs text-muted">
                    {{ item.quantity }} {{ t(`enums.unit.${item.unit}`) }} · {{ item.shoppingListName }}
                  </p>
                  <!-- The trip's fingerprint: when and where. Two rows of the same product are only
                       tellable apart by these, which is why they are on the card and not in a detail view. -->
                  <div class="mt-1 flex flex-wrap items-center gap-1.5">
                    <UBadge v-if="item.purchasedAt" color="success" variant="soft" size="sm">
                      {{ formatDate(item.purchasedAt) }}
                    </UBadge>
                    <UBadge v-else color="warning" variant="soft" size="sm">
                      {{ t('loadInventory.notPurchasedYet') }}
                    </UBadge>
                    <UBadge v-if="item.shoppingLocationName" color="neutral" variant="soft" size="sm">
                      {{ item.shoppingLocationName }}
                    </UBadge>
                  </div>
                </div>
                <UIcon
                  v-if="selectedIds.has(item.publicId)"
                  name="i-lucide-check"
                  class="mt-1 h-5 w-5 shrink-0 text-primary-500"
                />
              </div>
            </button>
          </li>
        </ul>

        <UButton
          v-if="hasMore"
          :label="t('loadInventory.loadMore')"
          color="neutral"
          variant="outline"
          block
          :loading="isLoadingMore"
          @click="loadMore"
        />
      </div>

      <!-- ============ Per-item detail ============ -->
      <div v-else-if="phase === 'detail' && currentItem" class="space-y-5">
        <div class="rounded-lg border border-default p-3">
          <p class="text-sm font-semibold">
            {{ currentItem.productName }}
            <span v-if="currentItem.productBrand" class="font-normal text-muted">· {{ currentItem.productBrand }}</span>
          </p>
          <p class="mt-0.5 text-xs text-muted">{{ currentItem.shoppingListName }}</p>
          <div class="mt-2 flex flex-wrap items-center gap-1.5">
            <UBadge v-if="currentItem.purchasedAt" color="success" variant="soft" size="sm">
              {{ formatDate(currentItem.purchasedAt) }}
            </UBadge>
            <UBadge v-if="currentItem.shoppingLocationName" color="neutral" variant="soft" size="sm">
              <span class="flex items-center gap-1">
                <UIcon name="i-lucide-shopping-cart" class="h-3 w-3" />
                {{ currentItem.shoppingLocationName }}
              </span>
            </UBadge>
          </div>
          <!-- The shop is never asked for: the list item already records it and the purchase info
               inherits it, so asking again could only introduce a contradiction. -->
          <p class="mt-2 text-xs text-muted">{{ t('loadInventory.shoppingLocationCarriedOver') }}</p>
        </div>

        <UFormField :label="t('pages.addProduct.inventory.form.quantity')" name="quantity" required>
          <UInput
            v-model.number="form.quantity"
            type="number"
            step="0.01"
            min="0.01"
            class="w-full"
          >
            <template #trailing>
              <span class="text-sm text-muted">{{ t(`enums.unit.${currentItem.unit}`) }}</span>
            </template>
          </UInput>
        </UFormField>

        <UFormField :label="t('loadInventory.storageLocation')" name="storageLocation" required>
          <USelectMenu
            v-model="form.storageLocationPublicId"
            :items="storageLocationOptions"
            value-key="value"
            :placeholder="t('loadInventory.storageLocationPlaceholder')"
            :search-input="{ placeholder: t('common.search') }"
            :loading="isLoadingStorageLocations"
            class="w-full"
          />
        </UFormField>

        <UFormField :label="t('pages.addProduct.inventory.form.expirationAt')" name="expirationAt">
          <UInputDate v-model="form.expirationAt" :locale="inputDateLocale" class="w-full" />
        </UFormField>

        <div class="grid grid-cols-2 gap-4">
          <UFormField :label="t('pages.addProduct.inventory.form.price')" name="price">
            <UInput v-model.number="form.price" type="number" step="0.01" min="0" class="w-full" />
          </UFormField>

          <UFormField
            v-if="form.price !== undefined && form.price > 0"
            :label="t('pages.addProduct.inventory.form.currency')"
            name="currency"
          >
            <USelect v-model="form.currency" :items="currencyOptions" class="w-full" />
          </UFormField>
        </div>

        <UFormField :label="t('pages.addProduct.inventory.form.isSharedWithFamily')" name="isSharedWithFamily">
          <UCheckbox
            v-model="form.isSharedWithFamily"
            :label="t('pages.addProduct.inventory.form.isSharedWithFamilyLabel')"
          />
        </UFormField>
      </div>

      <!-- ============ Summary ============ -->
      <div v-else class="space-y-4 py-6 text-center">
        <UIcon name="i-lucide-check-circle-2" class="mx-auto h-12 w-12 text-primary-500" />
        <p class="text-lg font-semibold">{{ t('loadInventory.summary.title') }}</p>
        <p class="text-sm text-muted">
          {{ t('loadInventory.summary.added', { count: addedCount }) }}
          <template v-if="skippedCount > 0">
            · {{ t('loadInventory.summary.skipped', { count: skippedCount }) }}
          </template>
        </p>
      </div>
    </template>
  </WizardDrawer>
</template>

<script setup lang="ts">
/**
 * Loads a whole shopping trip into the inventory (#63).
 *
 * Two passes, because they answer different questions. The first is "which of the things we bought
 * are we stocking?" — a multi-select over every list the user can see, not one list at a time, since
 * a trip is one trip whichever list it was written on. The second is "where does each one go?",
 * which only the person holding the shopping can answer and which therefore has to be per item.
 *
 * Each item is committed on its own, through the existing quick-purchase endpoint. That is what
 * makes the flow interruptible: closing the sheet halfway keeps everything already committed, and
 * the server's own "already loaded" marker keeps those items out of the picker next time.
 */
import { computed, ref, watch } from 'vue'
import type { Ref } from 'vue'
import { watchDebounced } from '@vueuse/core'
import type { DateValue } from '@internationalized/date'
import type { LoadableShoppingListItemInfo } from '~/types/shoppingList'
import type { StorageLocationInfo } from '~/types/location'
import { Currency } from '~/types/enums'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  /** At least one inventory item was created — the page reloads its grid. */
  created: []
}>()

const { t } = useI18n()
const toast = useToast()
const { formatDate } = useDateFormat()
const { inputDateLocale } = useInputDateLocale()
const { getLoadableInventoryItems, quickPurchaseItem } = useShoppingListApi()
const { getStorageLocations } = useLocationsApi()

const PAGE_SIZE = 30

type Phase = 'select' | 'detail' | 'summary'

const phase = ref<Phase>('select')

// ---- Selection ----
const items = ref<LoadableShoppingListItemInfo[]>([])
const selectedIds = ref(new Set<string>())
const searchQuery = ref('')
const isLoading = ref(false)
const isLoadingMore = ref(false)
const pageNumber = ref(1)
const totalCount = ref(0)

const hasMore = computed(() => items.value.length < totalCount.value)

// ---- Per-item pass ----
const queue = ref<LoadableShoppingListItemInfo[]>([])
const queueIndex = ref(0)
const addedCount = ref(0)
const skippedCount = ref(0)
const isCommitting = ref(false)

const currentItem = computed(() => queue.value[queueIndex.value] ?? null)

interface DetailForm {
  quantity: number
  storageLocationPublicId: string | undefined
  expirationAt: DateValue | null
  price: number | undefined
  currency: Currency | undefined
  isSharedWithFamily: boolean
}

// Cast back to the declared shape: `ref()` runs the value through `UnwrapRef`, which recurses into
// `DateValue` and strips the private brands off its class members, so the v-model no longer matches
// what UInputDate accepts. Runtime reactivity is unchanged — this is only the type.
const form = ref<DetailForm>({
  quantity: 1,
  storageLocationPublicId: undefined,
  expirationAt: null,
  price: undefined,
  currency: undefined,
  isSharedWithFamily: true
}) as Ref<DetailForm>

const canCommit = computed(() =>
  !!form.value.storageLocationPublicId && form.value.quantity > 0
)

// ---- Storage locations ----
const storageLocations = ref<StorageLocationInfo[]>([])
const isLoadingStorageLocations = ref(false)
const storageLocationOptions = computed(() =>
  storageLocations.value.map(l => ({ label: l.name, value: l.publicId }))
)

const currencyOptions = computed(() => [
  { label: t('enums.currency.135'), value: Currency.Huf },
  { label: t('enums.currency.105'), value: Currency.Eur },
  { label: t('enums.currency.279'), value: Currency.Usd }
])

// ---- Wizard chrome ----
const steps = computed(() => {
  const list = [{ label: t('loadInventory.steps.select') }]
  for (let i = 0; i < queue.value.length; i++) {
    list.push({ label: `${i + 1} / ${queue.value.length}` })
  }
  if (queue.value.length > 0) list.push({ label: t('loadInventory.steps.summary') })
  return list
})

const currentStep = computed(() => {
  if (phase.value === 'select') return 0
  if (phase.value === 'detail') return queueIndex.value + 1
  return Math.max(steps.value.length - 1, 0)
})

const loadItems = async (append = false) => {
  if (append) isLoadingMore.value = true
  else isLoading.value = true

  try {
    const response = await getLoadableInventoryItems({
      pageNumber: pageNumber.value,
      pageSize: PAGE_SIZE,
      searchText: searchQuery.value.trim() || undefined
    })

    if (response.success && response.data) {
      items.value = append ? [...items.value, ...response.data.items] : response.data.items
      totalCount.value = response.data.totalCount
    }
  } finally {
    isLoading.value = false
    isLoadingMore.value = false
  }
}

const loadMore = async () => {
  pageNumber.value += 1
  await loadItems(true)
}

const loadStorageLocations = async () => {
  isLoadingStorageLocations.value = true
  try {
    const response = await getStorageLocations({ returnAll: true })
    if (response.success && response.data) {
      storageLocations.value = [...response.data.items].sort((a, b) =>
        a.name.localeCompare(b.name, 'hu')
      )
    }
  } finally {
    isLoadingStorageLocations.value = false
  }
}

// Server-side search: the picker spans every list, so filtering what one page happens to hold
// would hide matches rather than find them.
watchDebounced(searchQuery, () => {
  pageNumber.value = 1
  loadItems()
}, { debounce: 300 })

const toggle = (publicId: string) => {
  // A new Set, not a mutation: Vue tracks Set identity for the reactive read in the template.
  const next = new Set(selectedIds.value)
  if (next.has(publicId)) next.delete(publicId)
  else next.add(publicId)
  selectedIds.value = next
}

const clearSelection = () => {
  selectedIds.value = new Set()
}

const resetForm = () => {
  const item = currentItem.value
  form.value = {
    quantity: item?.quantity ?? 1,
    storageLocationPublicId: undefined,
    expirationAt: null,
    price: undefined,
    currency: undefined,
    // Carried across items: a shopping trip is normally all-personal or all-shared, and
    // re-ticking it seven times is the kind of thing that makes people stop using a flow.
    isSharedWithFamily: form.value.isSharedWithFamily
  }
}

const startQueue = () => {
  // Keep the picker's order, which is the order the user just read.
  queue.value = items.value.filter(i => selectedIds.value.has(i.publicId))
  if (queue.value.length === 0) return

  queueIndex.value = 0
  addedCount.value = 0
  skippedCount.value = 0
  phase.value = 'detail'
  resetForm()
}

const advance = () => {
  if (queueIndex.value + 1 < queue.value.length) {
    queueIndex.value += 1
    resetForm()
    return
  }

  phase.value = 'summary'
}

const skipCurrent = () => {
  skippedCount.value += 1
  advance()
}

const toIsoDate = (date: DateValue | null): string | undefined => {
  if (!date) return undefined
  // Noon, so a timezone offset cannot push the date onto the day before.
  return new Date(date.year, date.month - 1, date.day, 12, 0, 0).toISOString()
}

const commitCurrent = async () => {
  const item = currentItem.value
  if (!item || !canCommit.value) return

  isCommitting.value = true
  try {
    const response = await quickPurchaseItem({
      shoppingListItemPublicId: item.publicId,
      // Only used for an item that is not yet marked purchased; the server keeps the original
      // date for one that already is.
      purchasedAt: item.purchasedAt ?? new Date().toISOString(),
      quantity: form.value.quantity,
      price: form.value.price && form.value.price > 0 ? form.value.price : undefined,
      currency: form.value.price && form.value.price > 0 ? form.value.currency : undefined,
      storageLocationPublicId: form.value.storageLocationPublicId,
      expirationAt: toIsoDate(form.value.expirationAt),
      isSharedWithFamily: form.value.isSharedWithFamily
    })

    if (response.success) {
      addedCount.value += 1
      advance()
      return
    }

    // `useApiClient` already showed the single toast for this failure. Staying on the item — rather
    // than advancing past it — is the recovery: the storage location or the quantity is usually
    // what needs correcting, and skipping is one tap away if it is not.
  } catch {
    toast.add({
      title: t('toast.error'),
      description: t('loadInventory.commitFailed'),
      color: 'error'
    })
  } finally {
    isCommitting.value = false
  }
}

const close = () => {
  emit('update:open', false)
}

const onOpenChange = (value: boolean) => {
  emit('update:open', value)
}

watch(() => props.open, (isOpen) => {
  if (isOpen) {
    phase.value = 'select'
    items.value = []
    selectedIds.value = new Set()
    searchQuery.value = ''
    pageNumber.value = 1
    totalCount.value = 0
    queue.value = []
    queueIndex.value = 0
    loadItems()
    loadStorageLocations()
    return
  }

  // Closing mid-flow is a legitimate exit, not a cancel: what was committed is in the stock, and
  // the grid behind the sheet has to show it.
  if (addedCount.value > 0) {
    emit('created')
  }
  addedCount.value = 0
  skippedCount.value = 0
})
</script>
