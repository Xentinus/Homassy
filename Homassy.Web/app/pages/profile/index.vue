<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6">
      <!-- Header -->
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-user" class="h-7 w-7 text-primary-500" />
        <h1 class="text-2xl font-semibold">{{ $t('profile.title') }}</h1>
      </div>
    </div>

    <!-- Content Section with padding to account for fixed header -->
    <div class="pt-28 px-4 sm:px-8 lg:px-14 pb-6 space-y-6">
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

    <!-- Upload Progress Modal -->
    <UploadProgressModal
      :is-open="isUploadProgressOpen"
      :progress="uploadProgress"
      :stage="uploadStage"
      :status="uploadStatus"
      :error-message="uploadErrorMessage"
      @update:is-open="isUploadProgressOpen = $event"
      @cancel="handleCancelUpload"
      @close="handleCloseUploadModal"
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
      <ButtonCard
        icon="i-lucide-settings"
        :title="$t('profile.settings')"
        :description="$t('profile.settingsCardDescription')"
        to="/profile/settings"
      />
      <ButtonCard
        icon="i-lucide-users"
        :title="$t('profile.family.title')"
        :description="$t('profile.family.cardDescription')"
        to="/profile/family"
      />
      <ButtonCard
        icon="i-lucide-bell"
        :title="$t('profile.notifications.title')"
        :description="$t('profile.notifications.cardDescription')"
        to="/profile/notifications"
      />
      <ButtonCard
        icon="i-lucide-package"
        :title="$t('profile.allProducts.title')"
        :description="$t('profile.allProducts.cardDescription')"
        to="/profile/products"
      />
      <ButtonCard
        icon="i-lucide-shopping-cart"
        :title="$t('profile.shoppingLocations.title')"
        :description="$t('profile.shoppingLocations.cardDescription')"
        to="/profile/shopping-locations"
      />
      <ButtonCard
        icon="i-lucide-warehouse"
        :title="$t('profile.storageLocations.title')"
        :description="$t('profile.storageLocations.cardDescription')"
        to="/profile/storage-locations"
      />
    </div>

    <!-- Version Info -->
    <div v-if="versionLoading || versionInfo" class="text-center text-xs text-gray-500 dark:text-gray-400 mt-8 pb-4">
      <template v-if="versionLoading">
        <USkeleton class="h-4 w-32 mx-auto" />
      </template>
      <template v-else-if="versionInfo">
        v{{ displayVersion }}
      </template>
    </div>
    </div>
  </div>
</template>

<script setup lang="ts">

import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'
import { useProgressApi } from '~/composables/api/useProgressApi'
import { useFamilyApi } from '~/composables/api/useFamilyApi'
import { useVersionApi } from '~/composables/api/useVersionApi'
import { useRouter } from 'vue-router'
import ImageCropper from '~/components/ImageCropper.vue'
import UploadProgressModal from '~/components/UploadProgressModal.vue'
import imageCompression from 'browser-image-compression'
import { extractBase64 } from '~/composables/useImageCrop'
import type { VersionInfo } from '~/types/version'


definePageMeta({ layout: 'auth', middleware: 'auth' })


const authStore = useAuthStore()
const { uploadProfilePicture, uploadProfilePictureWithProgress, deleteProfilePicture, getUserProfile } = useUserApi()
const progressApi = useProgressApi()
const { leaveFamily } = useFamilyApi()
const { getVersion } = useVersionApi()
const router = useRouter()
const { t, setLocale } = useI18n()
const colorMode = useColorMode()
const toast = useToast()

const userProfile = ref<any>(null)
const loading = ref(true)
const isLeavingFamily = ref(false)
const imageCropperOpen = ref(false)
const cropperImageSrc = ref('')
const versionInfo = ref<VersionInfo | null>(null)
const versionLoading = ref(false)

// Upload progress state
const isUploadProgressOpen = ref(false)
const currentUploadJobId = ref<string | null>(null)
const uploadProgress = ref(0)
const uploadStage = ref('validating')
const uploadStatus = ref<'inprogress' | 'completed' | 'failed' | 'cancelled'>('inprogress')
const uploadErrorMessage = ref<string | undefined>(undefined)
let stopPolling: (() => void) | null = null

// Check if we're in production mode
const isProduction = import.meta.env.PROD

// Computed property for version display
const displayVersion = computed(() => {
  if (!versionInfo.value) return ''
  return isProduction ? versionInfo.value.shortVersion : versionInfo.value.version
})

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
  isLeavingFamily.value = true
  try {
    await leaveFamily()
    await fetchUserProfile()
  } catch (error) {
    console.error('Failed to leave family:', error)
    toast.add({
      title: t('profile.family.leaveFailed'),
      color: 'red',
      icon: 'i-lucide-alert-circle'
    })
  } finally {
    isLeavingFamily.value = false
  }
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

      await uploadWithProgress(pureBase64)
    }
    reader.readAsDataURL(compressed)
  } catch (error) {
    console.error('Failed to process image:', error)
  }
}

async function uploadWithProgress(base64Data: string) {
  try {
    // Open progress modal
    isUploadProgressOpen.value = true
    uploadProgress.value = 0
    uploadStage.value = 'validating'
    uploadStatus.value = 'inprogress'
    uploadErrorMessage.value = undefined

    // Start async upload
    const response = await uploadProfilePictureWithProgress({ imageBase64: base64Data })

    if (response.data?.jobId) {
      currentUploadJobId.value = response.data.jobId

      // Start polling for progress
      stopPolling = progressApi.pollProgress(response.data.jobId, async (progress) => {
        uploadProgress.value = progress.percentage
        uploadStage.value = progress.stage
        uploadStatus.value = progress.status
        uploadErrorMessage.value = progress.errorMessage

        // If completed or failed, stop polling and update UI
        if (progress.status === 'completed') {
          await fetchUserProfile()
          // Auto-close modal after success
          setTimeout(() => {
            handleCloseUploadModal()
          }, 500)
        }
      })
    }
  } catch (error) {
    console.error('Failed to start upload:', error)
    isUploadProgressOpen.value = false
  }
}

function handleCancelUpload() {
  if (currentUploadJobId.value) {
    progressApi.cancelJob(currentUploadJobId.value).catch(error => {
      console.error('Failed to cancel upload:', error)
    })
    if (stopPolling) {
      stopPolling()
      stopPolling = null
    }
  }
}

function handleCloseUploadModal() {
  if (stopPolling) {
    stopPolling()
    stopPolling = null
  }
  isUploadProgressOpen.value = false
  currentUploadJobId.value = null
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
