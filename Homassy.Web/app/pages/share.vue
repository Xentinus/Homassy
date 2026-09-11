<template>
  <div class="px-4 sm:px-8 lg:px-14 pb-6 space-y-6 max-w-2xl mx-auto">
    <template v-if="loading">
      <USkeleton class="h-24 w-full rounded-xl" />
      <USkeleton class="h-32 w-full rounded-xl" />
    </template>

    <!-- Nothing to act on: a direct visit to /share, a reload after the payload was
         consumed, or a share that arrived too long ago. -->
    <EmptyState
      v-else-if="!shared"
      illustration="notFound"
      :title="$t('share.empty.title')"
      :description="$t('share.empty.description')"
      :action-label="$t('share.empty.action')"
      @action="navigateTo('/calendar')"
    />

    <template v-else>
      <!-- What arrived -->
      <div class="rounded-xl border border-default bg-default p-4 space-y-3">
        <div class="flex items-start gap-3">
          <div v-if="imageObjectUrl" class="h-20 w-20 shrink-0 rounded-lg overflow-hidden border border-default">
            <img :src="imageObjectUrl" :alt="$t('share.imageAlt')" class="h-full w-full object-cover">
          </div>
          <div class="min-w-0 flex-1 space-y-1">
            <p v-if="shared.title" class="font-medium break-words">{{ shared.title }}</p>
            <p v-if="shared.text" class="text-sm text-muted break-words line-clamp-4">{{ shared.text }}</p>
            <a
              v-if="shared.url"
              :href="shared.url"
              target="_blank"
              rel="noopener noreferrer"
              class="text-sm text-primary-500 break-all underline"
            >{{ shared.url }}</a>
            <p v-if="!shared.title && !shared.text && !shared.url && shared.files.length" class="text-sm text-muted">
              {{ $t('share.imageOnly', { count: shared.files.length }) }}
            </p>
          </div>
        </div>
      </div>

      <!-- What to do with it -->
      <SettingsGroup :title="$t('share.chooseTitle')" :hint="$t('share.chooseHint')">
        <SettingsRow
          v-if="suggestedName"
          :label="$t('share.toShoppingList')"
          :description="suggestedName"
          icon="i-lucide-shopping-cart"
          @select="goToShoppingList"
        />
        <SettingsRow
          :label="$t('share.toProduct')"
          :description="suggestedName || $t('share.toProductNoName')"
          icon="i-lucide-package-plus"
          @select="productDrawerOpen = true"
        />
      </SettingsGroup>
    </template>

    <!-- The product case stays here rather than being handed to another page: a
         shared image is a File object, which cannot travel in a URL. -->
    <ProductFormDrawer
      :open="productDrawerOpen"
      :initial-name="suggestedName ?? undefined"
      :pending-image="sharedImage"
      @update:open="(v) => productDrawerOpen = v"
      @saved="onProductSaved"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * Landing page for the Web Share Target (#118).
 *
 * `public/sw-share.js` answers the manifest's POST share action, stashes the payload
 * and redirects here as a plain GET. This page reads that payload (destructively — see
 * `useShareTarget`) and offers the two things a share can become:
 *
 * - **a shopping-list item**, handed off to `/shopping-lists?action=add-custom`, which
 *   already owns list selection, the add-item wizard and the realtime group;
 * - **a product**, handled right here, because a shared image is a `File` and there is
 *   no way to carry one through a navigation.
 *
 * It uses the `auth` layout and middleware like every other private page, which is
 * what makes a share into a not-yet-logged-in app work: the middleware bounces to
 * `/auth/login?return_to=/share` and the login page pushes back here afterwards. The
 * payload is in the cache, not the URL, so it survives that round trip intact.
 */
import type { ProductInfo } from '~/types/product'
import type { SharedContent } from '~/composables/useShareTarget'
import { shareNameFrom } from '~/utils/shareText'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { t } = useI18n()
const toast = useToast()
const { takeSharedContent, setHandoffItemName } = useShareTarget()

const loading = ref(true)
const shared = ref<SharedContent | null>(null)
const productDrawerOpen = ref(false)

usePageHeader(() => ({
  icon: 'i-lucide-share-2',
  title: t('share.title'),
  loading: loading.value
}))

/** The first shared image, which is the only one either flow can use. */
const sharedImage = computed(() => shared.value?.files[0] ?? null)

/** Object URL for the preview above. Revoked on unmount — it pins the blob otherwise. */
const imageObjectUrl = ref<string | null>(null)

const suggestedName = computed(() => (shared.value ? shareNameFrom(shared.value) : null))

onMounted(async () => {
  shared.value = await takeSharedContent()
  loading.value = false

  const file = sharedImage.value
  if (file) imageObjectUrl.value = URL.createObjectURL(file)
})

onBeforeUnmount(() => {
  if (imageObjectUrl.value) URL.revokeObjectURL(imageObjectUrl.value)
})

function goToShoppingList() {
  setHandoffItemName(suggestedName.value)
  navigateTo({ path: '/shopping-lists', query: { action: 'add-custom' } })
}

function onProductSaved(product: ProductInfo) {
  toast.add({
    title: t('toast.success'),
    description: t('share.productCreated', { name: product.name }),
    color: 'success',
    icon: 'i-lucide-check-circle'
  })
  navigateTo(`/products/${product.publicId}`)
}
</script>
