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
const route = useRoute()
const authStore = useAuthStore()
const { t } = useI18n()
const toast = useToast()

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

/**
 * Handle logout success notification
 */
function handleLogoutSuccess() {
  const logoutParam = route.query.logout
  const logoutFlag = localStorage.getItem('homassy_logout_success')
  
  if (logoutParam === 'success' || logoutFlag === 'true') {
    // Clear the flag
    localStorage.removeItem('homassy_logout_success')
    
    // Show toast notification
    toast.add({
      title: t('toast.loggedOut'),
      description: t('toast.signedOut'),
      color: 'success',
      icon: 'i-heroicons-arrow-left-on-rectangle'
    })
    
    // Clean up the URL by removing the query parameter
    if (logoutParam) {
      router.replace({ path: '/', query: {} })
    }
  }
}

// Check if user is already authenticated on mount
onMounted(async () => {
  console.debug('[Index] Checking existing authentication...')
  
  // Handle logout success toast first
  handleLogoutSuccess()

  // Initialize auth state
  await authStore.initialize()

  console.debug('[Index] Ready to show landing page')
})

definePageMeta({
  layout: 'public'
})
</script>
