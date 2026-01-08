<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header -->
    <div class="flex items-center gap-3">
      <UIcon name="i-lucide-user" class="h-7 w-7 text-primary-500" />
      <h1 class="text-2xl font-semibold">{{ $t('profile.title') }}</h1>
    </div>

    <!-- Avatar Section -->
    <div class="flex flex-col items-center gap-4">
      <template v-if="loading">
        <USkeleton class="h-40 w-40 rounded-full" />
        <USkeleton class="h-6 w-32 mt-2" />
        <USkeleton class="h-4 w-24 mt-1" />
      </template>
      <template v-else>
        <!-- Avatar with delete icon overlay -->
        <div class="relative inline-block">
          <div class="border-4 border-primary-500 rounded-full p-1">
            <UAvatar
              :src="avatarSrc"
              :alt="primaryName || 'User'"
              :text="avatarInitial"
              class="h-40 w-40 text-6xl"
            />
          </div>
          <UButton
            v-if="hasAvatar"
            icon="i-lucide-trash-2"
            color="error"
            size="xs"
            class="absolute -top-1 -right-1 shadow-md rounded-full"
            @click="onDeleteAvatar"
          />
        </div>

        <!-- Name Display -->
        <div class="text-center">
          <p class="text-2xl font-semibold">{{ primaryName }}</p>
          <p v-if="secondaryName" class="text-sm text-gray-600 dark:text-gray-400 mt-1">
            {{ secondaryName }}
          </p>
        </div>

        <!-- Upload Button -->
        <UButton v-if="!hasAvatar" color="primary" variant="soft" class="w-full" @click="triggerFileSelect">
          <UIcon name="i-lucide-upload" class="h-4 w-4 mr-2" />
          {{ $t('profile.uploadAvatar') }}
        </UButton>
      </template>
    </div>

    <!-- Image Cropper Modal -->
    <ImageCropper
      :is-open="imageCropperOpen"
      :image-src="cropperImageSrc"
      :default-aspect-ratio="1"
      @close="imageCropperOpen = false"
      @cropped="handleCroppedImage"
    />

    <!-- Action Buttons -->
    <div class="space-y-3">
      <!-- Light/Dark mode toggle -->
      <ClientOnly>
        <UButton color="neutral" variant="soft" class="w-full" @click="toggleColorMode">
          <UIcon :name="colorMode.value === 'dark' ? 'i-lucide-sun' : 'i-lucide-moon'" class="h-4 w-4 mr-2" />
          {{ colorModeText }}
        </UButton>
      </ClientOnly>
      <UButton color="error" variant="soft" class="w-full" @click="onLogout">
        <UIcon name="i-lucide-log-out" class="h-4 w-4 mr-2" />
        {{ $t('auth.logout') }}
      </UButton>
    </div>

    <!-- Cards Grid: Settings, Family, ... -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-8">
      <template v-if="loading">
        <USkeleton class="h-32 w-full rounded-lg" />
        <USkeleton class="h-32 w-full rounded-lg" />
        <USkeleton class="h-32 w-full rounded-lg" />
      </template>
      <template v-else>
        <!-- Settings Card -->
        <NuxtLink to="/profile/settings">
          <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
            <UIcon name="i-lucide-settings" class="h-7 w-7 text-primary-500 mr-2" />
            <div class="flex-1">
              <h2 class="text-base font-semibold mb-1">{{ $t('profile.settings') }}</h2>
              <div class="space-y-2 text-sm">
                <div class="flex items-center">
                  <span class="text-gray-600 dark:text-gray-400 flex-1">{{ $t('profile.language') }}</span>
                  <span class="font-medium text-right min-w-[80px] w-full justify-end flex">{{ userProfile?.language }}</span>
                </div>
                <div class="flex items-center">
                  <span class="text-gray-600 dark:text-gray-400 flex-1">{{ $t('profile.currency') }}</span>
                  <span class="font-medium text-right min-w-[80px] w-full justify-end flex">{{ userProfile?.currency }}</span>
                </div>
                <div class="flex items-center">
                  <span class="text-gray-600 dark:text-gray-400 flex-1">{{ $t('profile.timeZone') }}</span>
                  <span class="font-medium text-right min-w-[80px] w-full justify-end flex">{{ userProfile?.timeZone }}</span>
                </div>
              </div>
            </div>
            <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
          </div>
        </NuxtLink>

        <!-- Family Card -->
        <NuxtLink to="/profile/family">
          <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
            <UIcon name="i-lucide-users" class="h-7 w-7 text-primary-500 mr-2" />
            <div>
              <h2 class="text-base font-semibold mb-1">{{ $t('profile.family.title') }}</h2>
              <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.cardDescription') }}</div>
            </div>
            <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
          </div>
        </NuxtLink>

        <!-- Notifications Card -->
        <NuxtLink to="/profile/notifications">
          <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
            <UIcon name="i-lucide-bell" class="h-7 w-7 text-primary-500 mr-2" />
            <div>
              <h2 class="text-base font-semibold mb-1">{{ $t('profile.notifications.title') }}</h2>
              <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.cardDescription') }}</div>
            </div>
            <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
          </div>
        </NuxtLink>

        <!-- All Products Card -->
        <NuxtLink to="/profile/products">
          <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
            <UIcon name="i-lucide-package" class="h-7 w-7 text-primary-500 mr-2" />
            <div>
              <h2 class="text-base font-semibold mb-1">{{ $t('profile.allProducts.title') }}</h2>
              <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.allProducts.cardDescription') }}</div>
            </div>
            <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
          </div>
        </NuxtLink>

        <!-- Shopping Locations Card -->
        <NuxtLink to="/profile/shopping-locations">
          <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
            <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 text-primary-500 mr-2" />
            <div>
              <h2 class="text-base font-semibold mb-1">{{ $t('profile.shoppingLocations.title') }}</h2>
              <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.shoppingLocations.cardDescription') }}</div>
            </div>
            <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
          </div>
        </NuxtLink>

        <!-- Storage Locations Card -->
        <NuxtLink to="/profile/storage-locations">
          <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
            <UIcon name="i-lucide-warehouse" class="h-7 w-7 text-primary-500 mr-2" />
            <div>
              <h2 class="text-base font-semibold mb-1">{{ $t('profile.storageLocations.title') }}</h2>
              <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.storageLocations.cardDescription') }}</div>
            </div>
            <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
          </div>
        </NuxtLink>
      </template>
    </div>

    <!-- Version Info -->
    <div v-if="versionLoading || versionInfo" class="text-center text-xs text-gray-500 dark:text-gray-400 mt-8 pb-4">
      <template v-if="versionLoading">
        <USkeleton class="h-4 w-32 mx-auto" />
      </template>
      <template v-else-if="versionInfo">
        v{{ versionInfo.shortVersion }}
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">

import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'
import { useFamilyApi } from '~/composables/api/useFamilyApi'
import { useVersionApi } from '~/composables/api/useVersionApi'
import { useRouter } from 'vue-router'
import ImageCropper from '~/components/ImageCropper.vue'
import imageCompression from 'browser-image-compression'
import { extractBase64 } from '~/composables/useImageCrop'
import type { VersionInfo } from '~/types/version'


definePageMeta({ layout: 'auth', middleware: 'auth' })


const authStore = useAuthStore()
const { uploadProfilePicture, deleteProfilePicture, getUserProfile } = useUserApi()
const { leaveFamily } = useFamilyApi()
const { getVersion } = useVersionApi()
const router = useRouter()
const { t, setLocale } = useI18n()
const colorMode = useColorMode()

const userProfile = ref<any>(null)
const loading = ref(true)
const imageCropperOpen = ref(false)
const cropperImageSrc = ref('')
const versionInfo = ref<VersionInfo | null>(null)
const versionLoading = ref(false)

async function fetchUserProfile() {
  loading.value = true
  try {
    const res = await getUserProfile()
    userProfile.value = res.data
    // Sync language locale and cookie
    if (res.data?.language) {
      const localeCode = authStore.syncLanguageLocale(res.data.language)
      await setLocale(localeCode)
    }
  } catch {
    userProfile.value = null
  }
  loading.value = false
}

async function fetchVersion() {
  versionLoading.value = true
  try {
    const res = await getVersion()
    versionInfo.value = res.data || null
  } catch {
    versionInfo.value = null
  }
  versionLoading.value = false
}

onMounted(async () => {
  await fetchUserProfile()
  await fetchVersion()
})

async function onLeaveFamily() {
  await leaveFamily()
  await fetchUserProfile()
}

const hasAvatar = computed(() => !!userProfile.value?.profilePictureBase64)
const avatarSrc = computed(() => {
  const b64 = userProfile.value?.profilePictureBase64
  return b64 ? `data:image/jpeg;base64,${b64}` : undefined
})

const avatarInitial = computed(() => {
  const name = userProfile.value?.displayName || userProfile.value?.name
  if (!name) return '?'
  const words = name.trim().split(/\s+/)
  return words.map((word: string) => word.charAt(0).toUpperCase()).join('')
})

// Name display
const hasDisplayName = computed(() =>
  !!(userProfile.value?.displayName && userProfile.value.displayName.trim())
)
const primaryName = computed(() =>
  hasDisplayName.value ? userProfile.value?.displayName : userProfile.value?.name
)
const secondaryName = computed(() =>
  hasDisplayName.value ? userProfile.value?.name : null
)

// Color mode
const colorModeText = computed(() =>
  colorMode.value === 'dark' ? t('profile.lightMode') : t('profile.darkMode')
)

onMounted(() => {
  // Auth middleware has already restored user via refreshAccessToken
})

function triggerFileSelect() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = 'image/*'

  input.onchange = (e: Event) => {
    const file = (e.target as HTMLInputElement).files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = (event) => {
      cropperImageSrc.value = event.target?.result as string
      imageCropperOpen.value = true
    }
    reader.readAsDataURL(file)
  }

  input.click()
}

async function handleCroppedImage(base64: string) {
  imageCropperOpen.value = false

  try {
    // Compress using browser-image-compression
    const blob = await fetch(base64).then(r => r.blob())
    const compressed = await imageCompression(blob as File, {
      maxWidthOrHeight: 500,
      maxSizeMB: 0.5,
      useWebWorker: true
    })

    // Convert to base64
    const reader = new FileReader()
    reader.onload = async () => {
      const compressedBase64 = reader.result as string
      const pureBase64 = extractBase64(compressedBase64)

      await uploadProfilePicture({ imageBase64: pureBase64 })
      await fetchUserProfile()
    }
    reader.readAsDataURL(compressed)
  } catch (error) {
    console.error('Failed to process image:', error)
  }
}

async function onDeleteAvatar() {
  await deleteProfilePicture()
  await fetchUserProfile()
}

async function onLogout() {
  await authStore.logout()
}

function toggleColorMode() {
  colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'
}
</script>
