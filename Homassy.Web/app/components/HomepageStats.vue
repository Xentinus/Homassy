<template>
  <UPageSection
    id="stats"
    :title="$t('pages.home.stats.title')"
    :description="$t('pages.home.stats.description')"
  >
    <template #body>
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-10 sm:gap-16">
        <div v-for="group in groups" :key="group.key">
          <h3 class="text-sm font-semibold text-primary uppercase tracking-wider mb-6">
            {{ group.label }}
          </h3>
          <dl class="space-y-6">
            <div v-for="item in group.items" :key="item.key" class="flex items-start gap-3">
              <UIcon :name="item.icon" class="size-5 text-muted mt-1 shrink-0" />
              <div>
                <dd class="text-2xl font-bold text-highlighted leading-tight mb-0.5">
                  <USkeleton v-if="isLoading" class="h-7 w-24" />
                  <template v-else>{{ formatNumber(item.value) }}</template>
                </dd>
                <dt class="text-sm text-muted">{{ item.label }}</dt>
              </div>
            </div>
          </dl>
        </div>
      </div>
    </template>
  </UPageSection>
</template>

<script setup lang="ts">
import type { GlobalStatistics } from '~/types/statistics'

const { t } = useI18n()
const { getStatistics } = useStatisticsApi()

const isLoading = ref(true)
const data = ref<GlobalStatistics | null>(null)

const groups = computed(() => [
  {
    key: 'products',
    label: t('pages.home.stats.groups.products'),
    items: [
      {
        key: 'totalProducts',
        icon: 'i-lucide-package',
        label: t('pages.home.stats.labels.products'),
        value: data.value?.totalProducts ?? 0
      },
      {
        key: 'totalInventoryItems',
        icon: 'i-lucide-warehouse',
        label: t('pages.home.stats.labels.inventoryItems'),
        value: data.value?.totalInventoryItems ?? 0
      }
    ]
  },
  {
    key: 'shopping',
    label: t('pages.home.stats.groups.shopping'),
    items: [
      {
        key: 'totalShoppingLists',
        icon: 'i-lucide-shopping-cart',
        label: t('pages.home.stats.labels.shoppingLists'),
        value: data.value?.totalShoppingLists ?? 0
      },
      {
        key: 'totalPurchasedItems',
        icon: 'i-lucide-circle-check',
        label: t('pages.home.stats.labels.purchasedItems'),
        value: data.value?.totalPurchasedItems ?? 0
      }
    ]
  },
  {
    key: 'locations',
    label: t('pages.home.stats.groups.locations'),
    items: [
      {
        key: 'totalShoppingLocations',
        icon: 'i-lucide-store',
        label: t('pages.home.stats.labels.shoppingLocations'),
        value: data.value?.totalShoppingLocations ?? 0
      },
      {
        key: 'totalStorageLocations',
        icon: 'i-lucide-archive',
        label: t('pages.home.stats.labels.storageLocations'),
        value: data.value?.totalStorageLocations ?? 0
      }
    ]
  }
])

function formatNumber(value: number): string {
  return value.toLocaleString()
}

onMounted(async () => {
  try {
    const response = await getStatistics()
    if (response.success && response.data) {
      data.value = response.data
    }
  }
  catch {
    // On error, keep data as null — values show as 0
  }
  finally {
    isLoading.value = false
  }
})
</script>
