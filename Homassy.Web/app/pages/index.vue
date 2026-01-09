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

<script setup lang="ts">
const router = useRouter()
const authStore = useAuthStore()
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

// Check if user is already authenticated on mount
onMounted(async () => {
  console.debug('[Index] Checking existing authentication...')

  // Load tokens from cookies if not already loaded
  await authStore.loadFromCookies()

  // If already authenticated with valid user, redirect to activity
  if (authStore.isAuthenticated) {
    console.debug('[Index] User is authenticated, redirecting to activity')
    await router.push('/activity')
    return
  }

  // If tokens exist but no user, they're likely invalid - clear them
  const { accessToken, refreshToken } = authStore.getTokensFromCookies()
  if ((accessToken || refreshToken) && !authStore.user) {
    console.debug('[Index] Tokens exist but no user - clearing invalid tokens')
    authStore.clearAuthData()
  }

  console.debug('[Index] Ready to show landing page')
})

definePageMeta({
  layout: 'public'
})
</script>
