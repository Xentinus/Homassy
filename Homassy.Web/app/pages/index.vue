<template>
  <div v-if="!leaving">
    <!-- Hero: the claim on the left, the actual product on the right. The device is the point —
         a visitor decides whether a household app is for them by looking at one screen of it. -->
    <section class="relative overflow-hidden">
      <div
        aria-hidden="true"
        class="pointer-events-none absolute -top-32 left-1/2 h-72 w-[46rem] max-w-[140vw] -translate-x-1/2 rounded-full bg-primary-300/30 blur-3xl dark:bg-primary-500/10"
      />

      <div class="mx-auto grid max-w-5xl items-center gap-12 px-4 py-16 sm:px-6 sm:py-24 lg:grid-cols-2">
        <div>
          <UBadge :label="$t('pages.home.heroBadge')" color="primary" variant="subtle" size="lg" />
          <h1 class="mt-5 text-4xl font-semibold tracking-tight sm:text-5xl">
            {{ $t('pages.home.hero.title') }}
          </h1>
          <p class="mt-4 text-lg text-muted">
            {{ $t('pages.home.hero.description') }}
          </p>

          <div class="mt-8 flex flex-wrap items-center gap-3">
            <UButton
              :label="$t('pages.home.cta.button')"
              to="/auth/register"
              color="primary"
              size="xl"
              icon="i-lucide-arrow-right"
            />
            <UButton
              :label="$t('pages.home.hero.secondary')"
              to="#features"
              color="neutral"
              variant="subtle"
              size="xl"
            />
          </div>

          <ul class="mt-8 flex flex-wrap gap-x-6 gap-y-2 text-sm text-muted">
            <li v-for="proof in heroProof" :key="proof" class="flex items-center gap-1.5">
              <UIcon name="i-lucide-check" class="h-4 w-4 text-primary-500" />
              {{ proof }}
            </li>
          </ul>
        </div>

        <!-- Eager, and the only device above the fold. -->
        <LandingDeviceFrame :width="300" priority :alt="$t('pages.home.hero.screenshotAlt')">
          <LandingAppScreen screen="inventory" />
        </LandingDeviceFrame>
      </div>
    </section>

    <!-- Live global counts. Kept and promoted rather than buried below the feature list: real
         numbers are the strongest signal that the project is alive. -->
    <HomepageStats />

    <div id="features">
      <LandingFeatureShowcase
        :eyebrow="$t('pages.home.showcase.inventory.eyebrow')"
        icon="i-lucide-package"
        :title="$t('pages.home.showcase.inventory.title')"
        :description="$t('pages.home.showcase.inventory.description')"
        :points="showcasePoints('inventory')"
      >
        <LandingDeviceFrame :width="280" :alt="$t('pages.home.showcase.inventory.title')">
          <LandingAppScreen screen="inventory" />
        </LandingDeviceFrame>
      </LandingFeatureShowcase>

      <LandingFeatureShowcase
        reversed
        :eyebrow="$t('pages.home.showcase.shopping.eyebrow')"
        icon="i-lucide-shopping-cart"
        :title="$t('pages.home.showcase.shopping.title')"
        :description="$t('pages.home.showcase.shopping.description')"
        :points="showcasePoints('shopping')"
      >
        <LandingDeviceFrame :width="280" :alt="$t('pages.home.showcase.shopping.title')">
          <LandingAppScreen screen="shopping" />
        </LandingDeviceFrame>
      </LandingFeatureShowcase>

      <LandingFeatureShowcase
        :eyebrow="$t('pages.home.showcase.scanner.eyebrow')"
        icon="i-lucide-scan-barcode"
        :title="$t('pages.home.showcase.scanner.title')"
        :description="$t('pages.home.showcase.scanner.description')"
        :points="showcasePoints('scanner')"
      >
        <LandingDeviceFrame :width="280" :alt="$t('pages.home.showcase.scanner.title')">
          <LandingAppScreen screen="scanner" />
        </LandingDeviceFrame>
      </LandingFeatureShowcase>

      <LandingFeatureShowcase
        reversed
        :eyebrow="$t('pages.home.showcase.calendar.eyebrow')"
        icon="i-lucide-calendar"
        :title="$t('pages.home.showcase.calendar.title')"
        :description="$t('pages.home.showcase.calendar.description')"
        :points="showcasePoints('calendar')"
      >
        <LandingDeviceFrame :width="280" :alt="$t('pages.home.showcase.calendar.title')">
          <LandingAppScreen screen="calendar" />
        </LandingDeviceFrame>
      </LandingFeatureShowcase>
    </div>

    <!-- The long tail of features, as a plain grid. Everything worth showing has been shown by
         now; this is for the reader who wants the list. -->
    <UPageSection
      id="everything"
      :title="$t('pages.home.keyFeatures')"
      :features="features"
    />

    <UPageSection
      id="highlights"
      :title="$t('pages.home.highlights.title')"
      :description="$t('pages.home.highlights.description')"
      :features="highlights"
    />

    <UPageSection
      id="cta"
      :title="$t('pages.home.cta.title')"
      :description="$t('pages.home.cta.description')"
      :links="[{ label: $t('pages.home.cta.button'), to: '/auth/register', color: 'primary', size: 'xl', icon: 'i-lucide-arrow-right' }]"
    />
  </div>
</template>

<script setup lang="ts">
const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const { t, locale } = useI18n()
const toast = useToast()
const config = useRuntimeConfig()

/**
 * Set only once we know an already-authenticated visitor is being sent to `/calendar`, so the
 * landing page is not still on screen underneath the navigation.
 *
 * It used to start `true` and gate the whole page on the session check finishing, which meant
 * the server rendered an empty `<div>`: nothing for a crawler or a link preview to read, on the
 * one page whose whole job is being read by strangers (#123). The session check can only run on
 * the client anyway — the Kratos cookie is httpOnly — so the page now renders first and the
 * redirect, when there is one, happens over it.
 */
const leaving = ref(false)

/**
 * Page metadata, per locale (#123).
 *
 * `app.vue` sets the app-wide defaults; this overrides them for the one page a stranger
 * actually lands on, and adds the card image so a shared link renders a preview instead of a
 * bare URL. The image has to be an absolute URL — a relative path is not resolvable by the
 * crawler that fetches it — so it is built from the public site URL.
 */
/** `og:locale` wants a POSIX-ish locale, not the two-letter code the app routes by. */
const OG_LOCALES: Record<string, string> = {
  en: 'en_US',
  hu: 'hu_HU',
  de: 'de_DE'
}

const siteUrl = computed(() => String(config.public.siteUrl ?? '').replace(/\/+$/, ''))
const ogImage = computed(() => `${siteUrl.value}/og-image.png`)

useSeoMeta({
  title: () => t('meta.home.title'),
  description: () => t('meta.home.description'),
  ogTitle: () => t('meta.home.ogTitle'),
  ogDescription: () => t('meta.home.ogDescription'),
  ogType: 'website',
  ogUrl: () => siteUrl.value || undefined,
  ogLocale: () => OG_LOCALES[locale.value] ?? 'en_US',
  ogImage: () => ogImage.value,
  ogImageWidth: 1200,
  ogImageHeight: 630,
  ogImageAlt: () => t('meta.home.ogImageAlt'),
  twitterCard: 'summary_large_image',
  twitterTitle: () => t('meta.home.ogTitle'),
  twitterDescription: () => t('meta.home.ogDescription'),
  twitterImage: () => ogImage.value,
  twitterImageAlt: () => t('meta.home.ogImageAlt')
})

const heroProof = computed(() => [
  t('pages.home.hero.proof.free'),
  t('pages.home.hero.proof.install'),
  t('pages.home.hero.proof.family')
])

type ShowcaseKey = 'inventory' | 'shopping' | 'scanner' | 'calendar'

/** Each showcase's bullet list, which is three keys per section rather than an array in JSON. */
const showcasePoints = (key: ShowcaseKey) => [
  t(`pages.home.showcase.${key}.points.one`),
  t(`pages.home.showcase.${key}.points.two`),
  t(`pages.home.showcase.${key}.points.three`)
]

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
  },
  {
    icon: 'i-lucide-fingerprint',
    title: t('pages.home.features.passkey.title'),
    description: t('pages.home.features.passkey.description')
  },
  {
    icon: 'i-lucide-smartphone',
    title: t('pages.home.features.pwa.title'),
    description: t('pages.home.features.pwa.description')
  },
  {
    icon: 'i-lucide-bell',
    title: t('pages.home.features.notifications.title'),
    description: t('pages.home.features.notifications.description')
  },
  {
    icon: 'i-lucide-activity',
    title: t('pages.home.features.activityFeed.title'),
    description: t('pages.home.features.activityFeed.description')
  }
])

const highlights = computed(() => [
  {
    icon: 'i-lucide-lock-open',
    title: t('pages.home.highlights.items.openSource.title'),
    description: t('pages.home.highlights.items.openSource.description')
  },
  {
    icon: 'i-lucide-globe',
    title: t('pages.home.highlights.items.multiLanguage.title'),
    description: t('pages.home.highlights.items.multiLanguage.description')
  },
  {
    icon: 'i-lucide-heart-handshake',
    title: t('pages.home.highlights.items.familyFirst.title'),
    description: t('pages.home.highlights.items.familyFirst.description')
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

  // If a valid session is restored, skip the landing page and go straight to
  // the calendar (same destination as a successful login).
  if (authStore.isAuthenticated) {
    console.debug('[Index] Existing session found, redirecting to /calendar')
    leaving.value = true
    await navigateTo('/calendar')
    return
  }

  console.debug('[Index] Ready to show landing page')
})

definePageMeta({
  layout: 'public'
})
</script>
