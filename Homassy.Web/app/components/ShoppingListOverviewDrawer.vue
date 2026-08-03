<template>
  <AppDrawer
    :open="open"
    :title="list?.name"
    icon="i-lucide-list-checks"
    @update:open="(v) => emit('update:open', v)"
  >
    <div v-if="list" class="space-y-6">
      <!-- List info -->
      <section class="rounded-2xl border border-default bg-default p-4 space-y-2">
        <div class="flex items-center gap-2">
          <span v-if="list.color" class="h-3 w-3 rounded-full flex-shrink-0" :style="{ backgroundColor: list.color }" />
          <h2 class="text-base font-bold text-highlighted flex-1 truncate">{{ list.name }}</h2>
        </div>
        <p v-if="list.description" class="text-sm text-muted">{{ list.description }}</p>
        <div class="flex flex-wrap gap-2 pt-1">
          <span class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-elevated text-toned">
            <UIcon :name="list.isSharedWithFamily ? 'i-lucide-users' : 'i-lucide-user'" class="h-3.5 w-3.5" :class="list.isSharedWithFamily ? 'text-primary-500' : 'text-gray-400'" />
            {{ list.isSharedWithFamily ? $t('common.family') : $t('common.personal') }}
          </span>
        </div>
      </section>

      <!-- Items still to buy -->
      <section class="space-y-2">
        <div class="flex items-center gap-2 px-1">
          <UIcon name="i-lucide-shopping-basket" class="h-4 w-4 text-primary-500" />
          <h3 class="text-sm font-semibold text-toned">{{ $t('profile.shoppingLists.pendingItemCount') }}</h3>
          <UBadge v-if="!isLoading" color="neutral" variant="soft" size="sm">{{ items.length }}</UBadge>
        </div>

        <div v-if="isLoading" class="space-y-2">
          <USkeleton class="h-16 w-full" />
          <USkeleton class="h-16 w-full" />
        </div>
        <div v-else-if="items.length === 0" class="text-center py-8 text-sm text-muted">
          {{ $t('pages.shoppingLists.noItemsInList') }}
        </div>
        <ul v-else class="space-y-2">
          <li v-for="item in items" :key="item.publicId" class="rounded-xl border border-default bg-default p-3">
            <div class="flex items-start justify-between gap-2">
              <div class="min-w-0">
                <p class="text-sm font-bold text-highlighted break-words">{{ itemName(item) }}</p>
                <p v-if="item.note" class="text-xs text-muted truncate">{{ item.note }}</p>
              </div>
              <div class="flex items-center gap-1.5 text-xs shrink-0">
                <span class="font-bold text-highlighted">{{ item.quantity }}</span>
                <span class="text-toned">{{ $t(`enums.unit.${item.unit}`) }}</span>
              </div>
            </div>
            <div v-if="item.deadlineAt" class="flex items-center gap-1.5 mt-1.5 text-xs text-muted">
              <UIcon name="i-lucide-calendar-clock" class="h-3.5 w-3.5 text-orange-600 dark:text-orange-400 flex-shrink-0" />
              <span>{{ formatDate(item.deadlineAt) }}</span>
            </div>
          </li>
        </ul>
      </section>
    </div>

    <template #footer>
      <UButton
        :label="$t('profile.shoppingLists.openList')"
        color="primary"
        variant="soft"
        icon="i-lucide-external-link"
        :disabled="!list"
        @click="openList"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useShoppingListApi } from '~/composables/api/useShoppingListApi'
import type { ShoppingListInfo, ShoppingListItemInfo } from '~/types/shoppingList'

const props = defineProps<{
  open: boolean
  list: ShoppingListInfo | null
}>()

const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { locale } = useI18n()
const { getShoppingListDetails } = useShoppingListApi()

const items = ref<ShoppingListItemInfo[]>([])
const isLoading = ref(false)

async function loadItems() {
  const publicId = props.list?.publicId
  if (!publicId) return
  isLoading.value = true
  try {
    // showPurchased: false — the overview previews what is still to buy.
    const res = await getShoppingListDetails(publicId, false)
    items.value = res.success && res.data ? res.data.items : []
  } catch {
    items.value = []
  } finally {
    isLoading.value = false
  }
}

watch(() => [props.open, props.list?.publicId] as const, ([isOpen, publicId]) => {
  if (isOpen && publicId) loadItems()
  else if (!isOpen) items.value = []
})

function itemName(item: ShoppingListItemInfo): string {
  return item.product?.name || item.customName || ''
}

function formatDate(dateString: string): string {
  const code = locale.value === 'hu' ? 'hu-HU' : locale.value === 'de' ? 'de-DE' : 'en-US'
  return new Date(dateString).toLocaleDateString(code, { year: 'numeric', month: '2-digit', day: '2-digit' })
}

/**
 * Managing a list's items lives on /shopping-lists. That page restores its selection from this
 * localStorage key, so seeding it before navigating opens the tapped list.
 */
function openList() {
  const publicId = props.list?.publicId
  if (!publicId) return
  localStorage.setItem('lastSelectedShoppingListId', publicId)
  emit('update:open', false)
  navigateTo('/shopping-lists')
}
</script>
