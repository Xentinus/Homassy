<template>
  <div>
    <UPageHero
      :title="$t('pages.home.title')"
      :description="$t('pages.home.description')"
      orientation="horizontal"
    >
      <img src="/favicon.svg" alt="Homassy Logo" class="hidden lg :block w-full max-w-[256px]">
    </UPageHero>

    <UPageSection
      id="features"
      :title="$t('pages.home.keyFeatures')"
      :features="features"
    />
  </div>
</template>

<script setup>
const { t } = useI18n()

const features = computed(() => [
  {
    icon: 'i-lucide-user-check',
    title: t('pages.home.features.userAuth.title'),
    description: t('pages.home.features.userAuth.description')
  },
  {
    icon: 'i-lucide-users',
    title: t('pages.home.features.familyManagement.title'),
    description: t('pages.home.features.familyManagement.description')
  },
  {
    icon: 'i-lucide-package-check',
    title: t('pages.home.features.productsInventory.title'),
    description: t('pages.home.features.productsInventory.description')
  },
  {
    icon: 'i-lucide-shopping-cart',
    title: t('pages.home.features.shoppingLists.title'),
    description: t('pages.home.features.shoppingLists.description')
  },
  {
    icon: 'i-lucide-map-pin',
    title: t('pages.home.features.locations.title'),
    description: t('pages.home.features.locations.description')
  },
  {
    icon: 'i-lucide-barcode',
    title: t('pages.home.features.barcodeQuality.title'),
    description: t('pages.home.features.barcodeQuality.description')
  }
])

definePageMeta({
  layout: 'public'
})
</script>
