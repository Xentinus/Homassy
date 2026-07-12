<template>
  <UDrawer
    :open="open"
    :dismissible="false"
    :ui="{
      content: 'h-[94dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-1 flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'flex-1 min-h-0 overflow-y-auto p-4 sm:p-6'
    }"
    @update:open="(v) => emit('update:open', v)"
  >
    <template #header>
      <div ref="headerEl" class="w-full" style="touch-action: none">
        <div class="flex items-center gap-3">
          <UIcon name="i-lucide-shopping-cart" class="h-6 w-6 shrink-0 text-primary-500" />
          <DrawerTitle class="text-lg sm:text-xl font-semibold truncate">{{ location?.name }}</DrawerTitle>
          <DrawerDescription class="sr-only">{{ location?.name }}</DrawerDescription>
          <UButton class="ml-auto" icon="i-lucide-x" color="neutral" variant="ghost" :aria-label="$t('common.close')" @click="emit('update:open', false)" />
        </div>
      </div>
    </template>

    <template #body>
      <div v-if="location" class="space-y-6">
        <!-- Location info -->
        <section class="rounded-2xl border border-default bg-default p-4 space-y-3">
          <div class="flex items-center gap-2">
            <span v-if="location.color" class="h-3 w-3 rounded-full flex-shrink-0" :style="{ backgroundColor: location.color }" />
            <h2 class="text-base font-bold text-highlighted flex-1 truncate">{{ location.name }}</h2>
            <UIcon v-if="location.isSharedWithFamily" name="i-lucide-users" class="h-4 w-4 text-primary-500 flex-shrink-0" :title="$t('common.family')" />
          </div>
          <p v-if="location.description" class="text-sm text-muted">{{ location.description }}</p>
          <div v-if="hasAddress" class="flex items-start gap-2 text-sm">
            <UIcon name="i-lucide-map-pin" class="h-4 w-4 text-amber-600 dark:text-amber-400 mt-0.5 flex-shrink-0" />
            <div class="min-w-0 space-y-0.5 text-toned">
              <p v-if="location.country">{{ location.country }}</p>
              <p v-if="location.city || location.postalCode">
                <span v-if="location.postalCode">{{ location.postalCode }} </span>
                <span v-if="location.city">{{ location.city }}</span>
              </p>
              <p v-if="location.address">{{ location.address }}</p>
            </div>
          </div>
          <LocationMap
            :address="location.address"
            :city="location.city"
            :postal-code="location.postalCode"
            :country="location.country"
          />

          <div v-if="location.website || location.googleMaps" class="flex flex-wrap gap-2">
            <UButton v-if="location.website" icon="i-lucide-external-link" :label="$t('profile.shoppingLocations.website')" size="xs" color="primary" variant="soft" class="rounded-full" :to="location.website" target="_blank" rel="noopener noreferrer" />
            <UButton v-if="location.googleMaps" icon="i-lucide-map" :label="$t('profile.shoppingLocations.googleMaps')" size="xs" color="success" variant="soft" class="rounded-full" :to="location.googleMaps" target="_blank" rel="noopener noreferrer" />
          </div>
        </section>

        <!-- Purchase history -->
        <section class="space-y-2">
          <div class="flex items-center gap-2 px-1">
            <UIcon name="i-lucide-history" class="h-4 w-4 text-primary-500" />
            <h3 class="text-sm font-semibold text-toned">{{ $t('profile.shoppingLocations.purchases') }}</h3>
            <UBadge v-if="!isLoading" color="neutral" variant="soft" size="sm">{{ purchases.length }}</UBadge>
          </div>

          <div v-if="isLoading" class="space-y-2">
            <USkeleton class="h-16 w-full" />
            <USkeleton class="h-16 w-full" />
          </div>
          <div v-else-if="purchases.length === 0" class="text-center py-8 text-sm text-muted">
            {{ $t('profile.shoppingLocations.noPurchases') }}
          </div>
          <ul v-else class="space-y-2">
            <li v-for="purchase in purchases" :key="purchase.publicId" class="rounded-xl border border-default bg-default p-3">
              <div class="flex items-start justify-between gap-2">
                <div class="min-w-0">
                  <p class="text-sm font-bold text-highlighted break-words">{{ purchase.productName }}</p>
                  <p v-if="purchase.productBrand" class="text-xs text-muted truncate">{{ purchase.productBrand }}</p>
                </div>
                <div class="flex items-center gap-1.5 text-xs shrink-0">
                  <UIcon name="i-lucide-package-2" class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                  <span class="font-bold text-highlighted">{{ purchase.quantity }}</span>
                  <span v-if="purchase.unit !== undefined && purchase.unit !== null" class="text-toned">{{ $t(`enums.unit.${purchase.unit}`) }}</span>
                </div>
              </div>
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1 mt-1.5 text-xs text-muted">
                <span class="flex items-center gap-1.5">
                  <UIcon name="i-lucide-calendar" class="h-3.5 w-3.5 flex-shrink-0" />
                  {{ formatDate(purchase.purchasedAt) }}
                </span>
                <span v-if="purchase.price != null" class="flex items-center gap-1.5">
                  <UIcon name="i-lucide-tag" class="h-3.5 w-3.5 text-green-600 dark:text-green-400 flex-shrink-0" />
                  {{ purchase.price }}<template v-if="purchase.currency !== undefined && purchase.currency !== null"> {{ $t(`enums.currency.${purchase.currency}`) }}</template>
                </span>
              </div>
            </li>
          </ul>
        </section>
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'
import type { ShoppingLocationInfo, ShoppingLocationPurchaseInfo } from '~/types/location'
import { useLocationsApi } from '~/composables/api/useLocationsApi'

const props = defineProps<{
  open: boolean
  location: ShoppingLocationInfo | null
}>()

const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { locale } = useI18n()
const { getShoppingLocationPurchases } = useLocationsApi()

const purchases = ref<ShoppingLocationPurchaseInfo[]>([])
const isLoading = ref(false)

const hasAddress = computed(() =>
  !!(props.location?.country || props.location?.city || props.location?.postalCode || props.location?.address)
)

async function loadPurchases() {
  const publicId = props.location?.publicId
  if (!publicId) return
  isLoading.value = true
  try {
    const res = await getShoppingLocationPurchases(publicId)
    purchases.value = res.success && res.data ? res.data : []
  } catch {
    purchases.value = []
  } finally {
    isLoading.value = false
  }
}

watch(() => [props.open, props.location?.publicId] as const, ([isOpen, publicId]) => {
  if (isOpen && publicId) loadPurchases()
  else if (!isOpen) purchases.value = []
})

function formatDate(dateString: string): string {
  const code = locale.value === 'hu' ? 'hu-HU' : locale.value === 'de' ? 'de-DE' : 'en-US'
  return new Date(dateString).toLocaleDateString(code, { year: 'numeric', month: '2-digit', day: '2-digit' })
}

const headerEl = ref<HTMLElement | null>(null)
useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false)
})
</script>
