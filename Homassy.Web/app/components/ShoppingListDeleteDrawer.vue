<template>
  <AppDrawer
    :open="open"
    :title="$t('pages.shoppingLists.deleteModal.title')"
    icon="i-lucide-trash-2"
    fit="content"
    @update:open="(v) => emit('update:open', v)"
  >
    <div class="space-y-4">
      <p class="text-sm text-muted">{{ $t('pages.shoppingLists.deleteModal.description') }}</p>

      <!-- Warning: the list's items go with it -->
      <div class="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <p class="text-sm font-medium text-red-600 dark:text-red-400">
          {{ $t('pages.shoppingLists.deleteModal.warning') }}
        </p>
      </div>

      <div class="space-y-3">
        <div>
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ $t('pages.shoppingLists.deleteModal.listName') }}:
          </span>
          <span class="text-sm ml-2">{{ list?.name }}</span>
        </div>

        <div v-if="list?.description">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ $t('common.description') }}:
          </span>
          <span class="text-sm ml-2">{{ list.description }}</span>
        </div>

        <!-- Only callers holding a loaded detail know the total item count. -->
        <div v-if="itemCount !== null" class="pt-2 border-t border-gray-200 dark:border-gray-700">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ $t('pages.shoppingLists.deleteModal.itemCount') }}:
          </span>
          <span class="text-sm ml-2">{{ itemCount }}</span>
        </div>
      </div>
    </div>

    <template #footer>
      <UButton
        :label="$t('pages.shoppingLists.deleteModal.cancel')"
        color="neutral"
        variant="outline"
        @click="emit('update:open', false)"
      />
      <UButton
        :label="$t('pages.shoppingLists.deleteModal.confirm')"
        color="error"
        :loading="isDeleting"
        @click="handleDelete"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useShoppingListApi } from '~/composables/api/useShoppingListApi'
import type { ShoppingListInfo } from '~/types/shoppingList'

/**
 * Delete confirmation for a shopping list. Owns the API call and emits `deleted` so the parent can
 * drop it from its local state. Shared by /shopping-lists (triggered from the list menu, where the
 * loaded detail supplies `itemCount`) and DataShoppingListCard (which has no total, so the count row
 * is hidden there).
 */
const props = withDefaults(defineProps<{
  open: boolean
  list?: ShoppingListInfo | null
  itemCount?: number | null
}>(), {
  list: null,
  itemCount: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  deleted: [publicId: string]
}>()

const { t } = useI18n()
const toast = useToast()
const { deleteShoppingList } = useShoppingListApi()

const isDeleting = ref(false)

async function handleDelete() {
  const publicId = props.list?.publicId
  if (!publicId) return

  isDeleting.value = true
  try {
    await deleteShoppingList(publicId)
    emit('deleted', publicId)
    emit('update:open', false)
  } catch (error) {
    console.error('Failed to delete shopping list:', error)
    toast.add({ title: t('common.error'), description: t('pages.shoppingLists.deleteFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    isDeleting.value = false
  }
}
</script>
