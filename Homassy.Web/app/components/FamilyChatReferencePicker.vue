<template>
  <AppDrawer
    :open="open"
    :title="t('familyChat.reference.pickerTitle')"
    :description="t('familyChat.reference.pickerDescription')"
    icon="i-lucide-paperclip"
    elevated
    :padded="false"
    @update:open="(value) => emit('update:open', value)"
  >
    <div class="flex h-full min-h-0 flex-col">
      <!-- Which kind to attach. A row of chips rather than a dropdown: there are four, and the
           whole point of the picker is to be faster than typing the thing's name. -->
      <div class="flex gap-2 overflow-x-auto border-b border-default px-4 py-3">
        <UButton
          v-for="kind in kinds"
          :key="kind.value"
          :variant="kind.value === activeKind ? 'solid' : 'outline'"
          :icon="kind.icon"
          color="primary"
          size="sm"
          class="shrink-0"
          @click="selectKind(kind.value)"
        >
          {{ kind.label }}
        </UButton>
      </div>

      <div class="border-b border-default px-4 py-3">
        <UInput
          v-model="search"
          icon="i-lucide-search"
          size="lg"
          :placeholder="t('common.search')"
          :aria-label="t('common.search')"
        />
      </div>

      <div class="min-h-0 flex-1 overflow-y-auto">
        <div v-if="loading" class="space-y-2 p-4">
          <USkeleton v-for="i in 5" :key="i" class="h-10 w-full rounded-lg" />
        </div>

        <p v-else-if="filtered.length === 0" class="p-6 text-center text-sm text-gray-500 dark:text-gray-400">
          {{ t('familyChat.reference.empty') }}
        </p>

        <ul v-else class="divide-y divide-default">
          <li v-for="option in filtered" :key="option.publicId">
            <button
              type="button"
              class="flex w-full items-center gap-3 px-4 py-3 text-left text-sm transition-colors hover:bg-elevated active:bg-elevated"
              @click="pick(option)"
            >
              <UIcon :name="iconFor(activeKind)" class="h-4 w-4 shrink-0 text-primary-500" />
              <span class="truncate">{{ option.text }}</span>
            </button>
          </li>
        </ul>
      </div>
    </div>
  </AppDrawer>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { FamilyChatReferenceKind, SelectValueType } from '~/types/enums'
import type { SelectValue } from '~/types/selectValue'

/**
 * Picks something in the app to attach to a chat message: a product, a shop, a storage place, a
 * shopping list.
 *
 * It reads the **same** `SelectValue` lists every other picker in the app reads, which is what
 * makes an attached reference valid by construction: the server validates a reference against that
 * same list, so anything offered here can be attached and anything not offered cannot.
 *
 * Lists are fetched per kind on first use and kept for the life of the picker - a family's products
 * do not change while somebody is choosing one, and re-fetching on every tab switch would make the
 * fastest part of the flow the slowest.
 */

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  pick: [reference: { kind: FamilyChatReferenceKind, publicId: string, label: string }]
}>()

const { t } = useI18n()
const { getSelectValues } = useSelectValueApi()

const KINDS: Array<{ value: FamilyChatReferenceKind, type: SelectValueType, icon: string, labelKey: string }> = [
  { value: FamilyChatReferenceKind.Product, type: SelectValueType.ProductCatalog, icon: 'i-lucide-package', labelKey: 'familyChat.reference.kind.product' },
  { value: FamilyChatReferenceKind.ShoppingLocation, type: SelectValueType.ShoppingLocation, icon: 'i-lucide-store', labelKey: 'familyChat.reference.kind.shoppingLocation' },
  { value: FamilyChatReferenceKind.StorageLocation, type: SelectValueType.StorageLocation, icon: 'i-lucide-archive', labelKey: 'familyChat.reference.kind.storageLocation' },
  { value: FamilyChatReferenceKind.ShoppingList, type: SelectValueType.ShoppingList, icon: 'i-lucide-list-checks', labelKey: 'familyChat.reference.kind.shoppingList' }
]

const activeKind = ref<FamilyChatReferenceKind>(FamilyChatReferenceKind.Product)
const search = ref('')
const loading = ref(false)
/** One cached list per kind — see the component's own note on why they are not re-fetched. */
const cache = ref<Partial<Record<FamilyChatReferenceKind, SelectValue[]>>>({})

const kinds = computed(() => KINDS.map(kind => ({ ...kind, label: t(kind.labelKey) })))

const options = computed<SelectValue[]>(() => cache.value[activeKind.value] ?? [])

const filtered = computed(() => {
  const needle = search.value.trim().toLowerCase()
  if (!needle) return options.value
  return options.value.filter(option => option.text.toLowerCase().includes(needle))
})

const iconFor = (kind: FamilyChatReferenceKind): string =>
  KINDS.find(k => k.value === kind)?.icon ?? 'i-lucide-paperclip'

const load = async (kind: FamilyChatReferenceKind): Promise<void> => {
  if (cache.value[kind]) return

  const type = KINDS.find(k => k.value === kind)?.type
  if (type === undefined) return

  loading.value = true
  try {
    const response = await getSelectValues(type)
    cache.value = { ...cache.value, [kind]: response?.data ?? [] }
  } catch {
    // An empty list is the honest answer here: the drawer shows its "nothing to attach" state
    // rather than a toast about a list the user did not ask for by name.
    cache.value = { ...cache.value, [kind]: [] }
  } finally {
    loading.value = false
  }
}

const selectKind = async (kind: FamilyChatReferenceKind): Promise<void> => {
  activeKind.value = kind
  search.value = ''
  await load(kind)
}

const pick = (option: SelectValue): void => {
  emit('pick', { kind: activeKind.value, publicId: option.publicId, label: option.text })
  emit('update:open', false)
}

watch(() => props.open, async (isOpen) => {
  if (!isOpen) return
  search.value = ''
  await load(activeKind.value)
})
</script>
