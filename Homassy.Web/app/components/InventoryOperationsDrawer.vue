<template>
  <AppDrawer
    :open="open"
    :title="view === 'split' ? $t('pages.products.details.splitModal.title') : view === 'move' ? $t('pages.products.details.moveModal.title') : $t('pages.products.details.operationsTitle')"
    icon="i-lucide-settings"
    :loading="isSplitting || isMoving"
    @update:open="(value) => emit('update:open', value)"
  >
    <!-- List view: select items, then pick an operation -->
    <div v-if="view === 'list'" class="space-y-3">
      <p class="text-sm text-muted">{{ $t('pages.products.details.operationsHint') }}</p>

      <div v-if="items.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
        {{ $t('pages.products.details.noInventoryItems') }}
      </div>

      <button
        v-for="it in items"
        :key="it.publicId"
        type="button"
        class="w-full flex items-center justify-between gap-3 rounded-xl border-2 px-4 py-3 text-left transition"
        :class="selectedIds.includes(it.publicId)
          ? 'border-primary-400 bg-primary-50 dark:border-primary-600 dark:bg-primary-950/40'
          : 'border-gray-200 dark:border-gray-700 hover:bg-elevated/50'"
        @click="toggleSelection(it.publicId)"
      >
        <div class="min-w-0">
          <div class="font-semibold text-highlighted">
            {{ it.currentQuantity }} {{ $t(`enums.unit.${it.unit}`) }}
          </div>
          <div v-if="it.storageLocation" class="text-sm text-muted truncate">
            {{ it.storageLocation.name }}
          </div>
          <div v-if="it.expirationAt" class="text-xs text-muted">
            {{ $t('common.expirationDate') }}: {{ formatDate(it.expirationAt) }}
          </div>
        </div>
        <UIcon
          :name="selectedIds.includes(it.publicId) ? 'i-lucide-check-circle-2' : 'i-lucide-circle'"
          class="h-5 w-5 shrink-0"
          :class="selectedIds.includes(it.publicId) ? 'text-primary-500' : 'text-gray-400'"
        />
      </button>
    </div>

    <!-- Split view -->
    <div v-else-if="view === 'split' && splitTarget" class="space-y-4">
      <div class="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
        <div class="text-sm text-gray-600 dark:text-gray-400">
          {{ $t('pages.products.details.splitModal.currentQuantity') }}
        </div>
        <div class="text-lg font-semibold">
          {{ splitTarget.currentQuantity }} {{ $t(`enums.unit.${splitTarget.unit}`) }}
        </div>
      </div>

      <div>
        <label class="block text-sm font-medium mb-1">
          {{ $t('pages.products.details.splitModal.splitQuantity') }} <span class="text-red-500">*</span>
        </label>
        <UInput
          v-model.number="splitQuantity"
          type="number"
          :min="0.1"
          :max="splitTarget.currentQuantity - 0.1"
          step="0.1"
          :placeholder="$t('pages.products.details.splitModal.splitQuantityPlaceholder')"
          class="w-full"
        />
        <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">
          {{ $t('pages.products.details.splitModal.maxQuantity', { max: (splitTarget.currentQuantity - 0.1).toFixed(1) }) }}
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <UButton
          v-for="step in [-10, -1, -0.1, 0.1, 1, 10]"
          :key="step"
          size="sm"
          variant="outline"
          :label="step > 0 ? `+${step}` : `${step}`"
          @click="adjustSplitQuantity(step)"
        />
      </div>

      <div class="p-3 bg-blue-50 dark:bg-blue-900/20 rounded-lg border border-blue-200 dark:border-blue-800">
        <div class="text-sm text-blue-600 dark:text-blue-400">
          {{ $t('pages.products.details.splitModal.remainingPreview') }}
        </div>
        <div class="text-lg font-semibold text-blue-700 dark:text-blue-300">
          {{ splitQuantity ? (splitTarget.currentQuantity - splitQuantity).toFixed(1) : splitTarget.currentQuantity }}
          {{ $t(`enums.unit.${splitTarget.unit}`) }}
        </div>
      </div>
    </div>

    <!-- Move view -->
    <div v-else-if="view === 'move'" class="space-y-4">
      <div class="p-3 bg-gray-50 dark:bg-gray-800 rounded-lg text-sm">
        <span class="font-medium text-gray-700 dark:text-gray-300">
          {{ $t('pages.products.details.operationsSelectedCount', { count: selectedIds.length }) }}
        </span>
      </div>

      <div>
        <label class="block text-sm font-medium mb-1">
          {{ $t('pages.products.details.moveModal.targetLocation') }} <span class="text-red-500">*</span>
        </label>
        <USelect
          v-model="selectedStorageId"
          :items="storageOptions"
          :loading="isLoadingStorages"
          :disabled="isMoving"
          :placeholder="$t('pages.products.details.moveModal.targetLocationPlaceholder')"
          class="w-full"
        />
      </div>
    </div>

    <template #footer>
      <!-- List footer -->
      <template v-if="view === 'list'">
        <UButton
          :label="$t('pages.products.details.splitItem')"
          color="neutral"
          variant="outline"
          icon="i-lucide-scissors"
          :disabled="selectedIds.length !== 1"
          @click="openSplit"
        />
        <UButton
          :label="$t('pages.products.details.moveItem')"
          color="primary"
          icon="i-lucide-move"
          :disabled="selectedIds.length === 0"
          @click="openMove"
        />
      </template>

      <!-- Split footer -->
      <template v-else-if="view === 'split'">
        <UButton :label="$t('common.previous')" color="neutral" variant="ghost" icon="i-lucide-arrow-left" @click="view = 'list'" />
        <UButton
          :label="$t('pages.products.details.splitModal.confirm')"
          color="primary"
          icon="i-lucide-check"
          :loading="isSplitting"
          :disabled="!isSplitValid"
          @click="handleSplit"
        />
      </template>

      <!-- Move footer -->
      <template v-else-if="view === 'move'">
        <UButton :label="$t('common.previous')" color="neutral" variant="ghost" icon="i-lucide-arrow-left" @click="view = 'list'" />
        <UButton
          :label="$t('pages.products.details.moveModal.move')"
          color="primary"
          icon="i-lucide-check"
          :loading="isMoving"
          :disabled="isLoadingStorages || !selectedStorageId"
          @click="handleMove"
        />
      </template>
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { InventoryItemInfo, SplitInventoryItemRequest, MoveInventoryItemsRequest } from '../types/product'
import { SelectValueType } from '../types/enums'
import type { SelectValue } from '../types/selectValue'

interface Props {
  open: boolean
  items: InventoryItemInfo[]
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  changed: []
}>()

const { t: $t } = useI18n()
const { formatDate } = useDateFormat()
const { splitInventoryItem, moveInventoryItems } = useProductsApi()
const { getSelectValues } = useSelectValueApi()
const toast = useToast()

type View = 'list' | 'split' | 'move'
const view = ref<View>('list')
const selectedIds = ref<string[]>([])

// Split state
const isSplitting = ref(false)
const splitQuantity = ref<number | null>(null)

// Move state
const isMoving = ref(false)
const isLoadingStorages = ref(false)
const selectedStorageId = ref<string | null>(null)
const storageOptions = ref<{ label: string; value: string }[]>([])

// Reset when the drawer opens/closes.
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  view.value = 'list'
  selectedIds.value = []
  splitQuantity.value = null
  selectedStorageId.value = null
})

const splitTarget = computed<InventoryItemInfo | null>(() => {
  if (selectedIds.value.length !== 1) return null
  return props.items.find(i => i.publicId === selectedIds.value[0]) ?? null
})

const isSplitValid = computed(() => {
  if (!splitTarget.value || !splitQuantity.value) return false
  return splitQuantity.value > 0 && splitQuantity.value < splitTarget.value.currentQuantity
})

const toggleSelection = (publicId: string) => {
  const idx = selectedIds.value.indexOf(publicId)
  if (idx >= 0) selectedIds.value.splice(idx, 1)
  else selectedIds.value.push(publicId)
}

const openSplit = () => {
  if (!splitTarget.value) return
  splitQuantity.value = Math.min(1, splitTarget.value.currentQuantity - 0.1)
  view.value = 'split'
}

const adjustSplitQuantity = (step: number) => {
  if (!splitTarget.value) return
  const currentValue = splitQuantity.value || 0
  const newValue = currentValue + step
  const maxAllowed = splitTarget.value.currentQuantity - 0.1
  if (newValue < 0.1) splitQuantity.value = 0.1
  else if (newValue > maxAllowed) splitQuantity.value = Math.round(maxAllowed * 10) / 10
  else splitQuantity.value = Math.round(newValue * 10) / 10
}

const handleSplit = async () => {
  if (!splitTarget.value || !isSplitValid.value) {
    toast.add({
      title: $t('toast.error'),
      description: $t('pages.products.details.splitModal.invalidQuantity'),
      color: 'error'
    })
    return
  }

  isSplitting.value = true
  try {
    const request: SplitInventoryItemRequest = { quantity: splitQuantity.value! }
    const response = await splitInventoryItem(splitTarget.value.publicId, request)
    if (response.success) {
      emit('changed')
      emit('update:open', false)
    }
  } catch (error) {
    console.error('Failed to split inventory item:', error)
  } finally {
    isSplitting.value = false
  }
}

const openMove = async () => {
  view.value = 'move'
  await loadStorageLocations()
}

const loadStorageLocations = async () => {
  isLoadingStorages.value = true
  try {
    const response = await getSelectValues(SelectValueType.StorageLocation)
    if (response.success && response.data) {
      storageOptions.value = response.data.map((s: SelectValue) => ({
        label: s.text,
        value: s.publicId
      }))
    }
  } catch (error) {
    console.error('Failed to load storage locations:', error)
  } finally {
    isLoadingStorages.value = false
  }
}

const handleMove = async () => {
  if (!selectedStorageId.value || selectedIds.value.length === 0) return

  isMoving.value = true
  try {
    const request: MoveInventoryItemsRequest = {
      inventoryItemPublicIds: [...selectedIds.value],
      storageLocationPublicId: selectedStorageId.value
    }
    const response = await moveInventoryItems(request)
    if (response.success) {
      emit('changed')
      emit('update:open', false)
    }
  } catch (error) {
    console.error('Failed to move inventory items:', error)
  } finally {
    isMoving.value = false
  }
}
</script>
