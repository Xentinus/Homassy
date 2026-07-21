<template>
  <AppDrawer
    :open="open"
    :title="location?.name"
    icon="i-lucide-warehouse"
    @update:open="(v) => emit('update:open', v)"
  >
    <div v-if="location" class="space-y-6">
      <!-- Location info -->
      <section class="rounded-2xl border border-default bg-default p-4 space-y-2">
        <div class="flex items-center gap-2">
          <span v-if="location.color" class="h-3 w-3 rounded-full flex-shrink-0" :style="{ backgroundColor: location.color }" />
          <h2 class="text-base font-bold text-highlighted flex-1 truncate">{{ location.name }}</h2>
        </div>
        <p v-if="location.description" class="text-sm text-muted">{{ location.description }}</p>
        <div class="flex flex-wrap gap-2 pt-1">
          <span v-if="location.isFreezer" class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-elevated text-toned">
            <UIcon name="i-lucide-snowflake" class="h-3.5 w-3.5 text-blue-600 dark:text-blue-400" />
            {{ $t('profile.storageLocations.freezer') }}
          </span>
          <span class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-elevated text-toned">
            <UIcon :name="location.isSharedWithFamily ? 'i-lucide-users' : 'i-lucide-user'" class="h-3.5 w-3.5" :class="location.isSharedWithFamily ? 'text-primary-500' : 'text-gray-400'" />
            {{ location.isSharedWithFamily ? $t('common.family') : $t('common.personal') }}
          </span>
        </div>
      </section>

      <!-- Current stock -->
      <section class="space-y-2">
        <div class="flex items-center gap-2 px-1">
          <UIcon name="i-lucide-package-2" class="h-4 w-4 text-primary-500" />
          <h3 class="text-sm font-semibold text-toned">{{ $t('profile.storageLocations.inStock') }}</h3>
          <UBadge v-if="!isLoading" color="neutral" variant="soft" size="sm">{{ items.length }}</UBadge>
        </div>

        <div v-if="isLoading" class="space-y-2">
          <USkeleton class="h-16 w-full" />
          <USkeleton class="h-16 w-full" />
        </div>
        <div v-else-if="items.length === 0" class="text-center py-8 text-sm text-muted">
          {{ $t('profile.storageLocations.noStock') }}
        </div>
        <ul v-else class="space-y-2">
          <li v-for="item in items" :key="item.publicId" class="rounded-xl border border-default bg-default p-3">
            <div class="flex items-start justify-between gap-2">
              <div class="min-w-0">
                <p class="text-sm font-bold text-highlighted break-words">{{ item.productName }}</p>
                <p v-if="item.productBrand" class="text-xs text-muted truncate">{{ item.productBrand }}</p>
              </div>
              <div class="flex items-center gap-1.5 text-xs shrink-0">
                <UIcon name="i-lucide-package-2" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                <span class="font-bold text-highlighted">{{ item.currentQuantity }}</span>
                <span class="text-toned">{{ $t(`enums.unit.${item.unit}`) }}</span>
              </div>
            </div>
            <div v-if="item.expirationAt" class="flex items-center gap-1.5 mt-1.5 text-xs text-muted">
              <UIcon name="i-lucide-calendar-clock" class="h-3.5 w-3.5 text-orange-600 dark:text-orange-400 flex-shrink-0" />
              <span>{{ formatDate(item.expirationAt) }}</span>
            </div>
          </li>
        </ul>
      </section>
    </div>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { StorageLocationInfo, StorageLocationInventoryItemInfo } from '~/types/location'
import { useLocationsApi } from '~/composables/api/useLocationsApi'

const props = defineProps<{
  open: boolean
  location: StorageLocationInfo | null
}>()

const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { locale } = useI18n()
const { getStorageLocationInventory } = useLocationsApi()

const items = ref<StorageLocationInventoryItemInfo[]>([])
const isLoading = ref(false)

async function loadInventory() {
  const publicId = props.location?.publicId
  if (!publicId) return
  isLoading.value = true
  try {
    const res = await getStorageLocationInventory(publicId)
    items.value = res.success && res.data ? res.data : []
  } catch {
    items.value = []
  } finally {
    isLoading.value = false
  }
}

watch(() => [props.open, props.location?.publicId] as const, ([isOpen, publicId]) => {
  if (isOpen && publicId) loadInventory()
  else if (!isOpen) items.value = []
})

function formatDate(dateString: string): string {
  const code = locale.value === 'hu' ? 'hu-HU' : locale.value === 'de' ? 'de-DE' : 'en-US'
  return new Date(dateString).toLocaleDateString(code, { year: 'numeric', month: '2-digit', day: '2-digit' })
}
</script>
