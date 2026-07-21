<template>
  <div>
    <!-- Content Section (page identity lives in the persistent AppHeader) -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6 space-y-6 max-w-2xl mx-auto">
      <!-- Identity card: avatar + name; tap to open the edit-profile drawer -->
      <ProfileIdentityCard
        :loading="loading"
        :avatar-src="avatarSrc"
        :avatar-initial="avatarInitial"
        :primary-name="primaryName"
        :secondary-name="secondaryName"
        @select="openEditProfile"
      />

      <!-- Image cropper + upload progress in one drawer (phase-switched) -->
      <ImageCropper
        :is-open="imageCropperOpen"
        :image-src="cropperImageSrc"
        :default-aspect-ratio="1"
        :upload-status="uploadStatus"
        :upload-progress="uploadProgress"
        :upload-stage="uploadStage"
        :upload-error-message="uploadErrorMessage"
        @close="imageCropperOpen = false"
        @cropped="handleCroppedImage"
        @cancel-upload="handleCancelUpload"
        @close-upload="handleCloseUpload"
      />

      <!-- Auth-dependent content renders only after mount so SSR (user=null)
           and client hydration agree — otherwise the skeleton/values mismatch. -->
      <div v-if="!loading" class="space-y-6">
        <!-- Account -->
        <SettingsGroup :title="$t('profile.groups.account')">
        <SettingsRow
          :label="$t('profile.security.title')"
          icon="i-lucide-shield"
          @select="securityOpen = true"
        />
        <SettingsRow
          :label="$t('profile.notifications.title')"
          icon="i-lucide-bell"
          @select="notificationsOpen = true"
        />
      </SettingsGroup>

      <!-- Preferences -->
      <SettingsGroup :title="$t('profile.groups.preferences')">
        <SettingsRow
          :label="$t('profile.language')"
          icon="i-lucide-languages"
          :value="authStore.user?.language"
          :loading="savingField === 'language'"
          @select="openSelect('language')"
        />
        <SettingsRow
          :label="$t('profile.currency')"
          icon="i-lucide-coins"
          :value="authStore.user?.currency"
          :loading="savingField === 'currency'"
          @select="openSelect('currency')"
        />
        <SettingsRow
          :label="$t('profile.timeZone')"
          icon="i-lucide-clock"
          :value="authStore.user?.timeZone"
          :loading="savingField === 'timeZone'"
          @select="openSelect('timeZone')"
        />
        <ClientOnly>
          <SettingsRow static :chevron="false" :label="$t('profile.theme.label')" icon="i-lucide-palette">
            <template #trailing>
              <div class="flex items-center gap-0.5 rounded-lg border border-default p-0.5">
                <button
                  v-for="opt in themeOptions"
                  :key="opt.value"
                  type="button"
                  class="p-1.5 rounded-md transition-colors"
                  :class="colorMode.preference === opt.value
                    ? 'bg-primary-500 text-white'
                    : 'text-muted hover:text-default'"
                  :aria-label="opt.label"
                  :aria-pressed="colorMode.preference === opt.value"
                  @click="colorMode.preference = opt.value"
                >
                  <UIcon :name="opt.icon" class="h-4 w-4" />
                </button>
              </div>
            </template>
          </SettingsRow>
        </ClientOnly>
      </SettingsGroup>

      <!-- Family -->
      <SettingsGroup :title="$t('profile.groups.family')">
        <SettingsRow
          :label="$t('profile.family.title')"
          icon="i-lucide-users"
          @select="familyOpen = true"
        />
      </SettingsGroup>

      <!-- Master data -->
      <SettingsGroup :title="$t('profile.groups.data')">
        <SettingsRow
          :label="$t('profile.masterData.title')"
          :description="$t('profile.masterData.description')"
          icon="i-lucide-database"
          to="/profile/data"
        />
      </SettingsGroup>

      <!-- Logout -->
      <SettingsGroup>
        <SettingsRow
          :label="$t('auth.logout')"
          icon="i-lucide-log-out"
          variant="danger"
          :chevron="false"
          @select="onLogout"
        />
      </SettingsGroup>

      <!-- Version Info -->
      <div v-if="versionLoading || versionInfo" class="text-center text-xs text-gray-500 dark:text-gray-400 pt-2 pb-4">
        <template v-if="versionLoading">
          <USkeleton class="h-4 w-32 mx-auto" />
        </template>
        <template v-else-if="versionInfo">
          v{{ displayVersion }}
        </template>
      </div>
      </div>

      <!-- Preference select bottom sheet -->
      <SettingsSelectDrawer
        :open="activeSelect !== null"
        :title="selectConfig?.title || ''"
        :items="selectConfig?.items || []"
        :model-value="selectConfig?.value"
        :searchable="selectConfig?.searchable"
        :loading="isLoadingOptions"
        @update:open="(v) => { if (!v) activeSelect = null }"
        @save="onSavePreference"
      />

      <!-- Edit profile bottom sheet: avatar + name / display name -->
      <SettingsEditDrawer
        :open="nameDrawerOpen"
        :title="$t('profile.editProfile.title')"
        icon="i-lucide-user"
        :loading="savingName"
        :save-disabled="!nameForm.name.trim()"
        @update:open="(v) => nameDrawerOpen = v"
        @save="onSaveName"
        @cancel="nameDrawerOpen = false"
      >
        <template #body>
          <div class="space-y-6">
            <!-- Avatar -->
            <div class="flex flex-col items-center gap-3">
              <div class="border-2 border-primary-500 rounded-full p-0.5">
                <UAvatar
                  :src="avatarSrc"
                  :alt="primaryName || 'User'"
                  :text="avatarInitial"
                  class="h-24 w-24 text-3xl"
                />
              </div>
              <div class="flex items-center gap-2">
                <UButton
                  icon="i-lucide-upload"
                  color="primary"
                  variant="soft"
                  :label="hasAvatar ? $t('profile.changePhoto') : $t('profile.uploadAvatar')"
                  @click="triggerFileSelect"
                />
                <UButton
                  v-if="hasAvatar"
                  icon="i-lucide-trash-2"
                  color="error"
                  variant="soft"
                  :label="$t('profile.removePhoto')"
                  @click="onDeleteAvatar"
                />
              </div>
            </div>

            <!-- Name fields -->
            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium mb-1.5">{{ $t('profile.name') }}</label>
                <UInput v-model="nameForm.name" :placeholder="$t('profile.name')" class="w-full" />
              </div>
              <div>
                <label class="block text-sm font-medium mb-1.5">{{ $t('profile.displayName') }}</label>
                <UInput v-model="nameForm.displayName" :placeholder="$t('profile.displayName')" class="w-full" />
              </div>
            </div>
          </div>
        </template>
      </SettingsEditDrawer>

      <!-- Account sub-surfaces as drawers -->
      <SecurityDrawer :open="securityOpen" @update:open="(v) => securityOpen = v" />
      <NotificationsDrawer :open="notificationsOpen" @update:open="(v) => notificationsOpen = v" />
      <FamilyDrawer :open="familyOpen" @update:open="(v) => familyOpen = v" />
    </div>
  </div>
</template>


<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'
import { useProgressApi } from '~/composables/api/useProgressApi'
import { useVersionApi } from '~/composables/api/useVersionApi'
import { useUserPreferences, type PreferenceField } from '~/composables/useUserPreferences'
import ImageCropper from '~/components/ImageCropper.vue'
import imageCompression from 'browser-image-compression'
import { extractBase64 } from '~/composables/useImageCrop'
import type { VersionInfo } from '~/types/version'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const authStore = useAuthStore()
const { uploadProfilePictureWithProgress, deleteProfilePicture } = useUserApi()
const progressApi = useProgressApi()
const { getVersion } = useVersionApi()
const { t } = useI18n()
const colorMode = useColorMode()
const route = useRoute()

// Persistent header (auth layout) — page identity.
usePageHeader(() => ({
  icon: 'i-lucide-user',
  title: t('profile.title')
}))

const {
  languageSelectOptions,
  currencySelectOptions,
  timeZoneSelectOptions,
  isLoadingOptions,
  loadSelectOptions,
  savePreference,
  saveName
} = useUserPreferences()

// Start true on both server and client so the initial (hydrated) render matches;
// flip to false only after mount, when the auth store is guaranteed populated.
const loading = ref(true)
const imageCropperOpen = ref(false)
const cropperImageSrc = ref('')
const versionInfo = ref<VersionInfo | null>(null)
const versionLoading = ref(false)

// Upload progress state ('idle' = still cropping; the cropper drawer switches
// to the progress view once this leaves 'idle').
const currentUploadJobId = ref<string | null>(null)
const uploadProgress = ref(0)
const uploadStage = ref('validating')
const uploadStatus = ref<'idle' | 'inprogress' | 'completed' | 'failed' | 'cancelled'>('idle')
const uploadErrorMessage = ref<string | undefined>(undefined)
let stopPolling: (() => void) | null = null

const isProduction = import.meta.env.PROD

const displayVersion = computed(() => {
  if (!versionInfo.value) return ''
  return isProduction ? versionInfo.value.shortVersion : versionInfo.value.version
})

// --- User display (single source of truth: the auth store) -----------------
const hasAvatar = computed(() => !!authStore.user?.profilePictureBase64)
const avatarSrc = computed(() => {
  const b64 = authStore.user?.profilePictureBase64
  return b64 ? `data:image/jpeg;base64,${b64}` : undefined
})
const avatarInitial = computed(() => {
  const name = authStore.user?.displayName || authStore.user?.name
  if (!name) return '?'
  return name.trim().split(/\s+/).map(w => w.charAt(0).toUpperCase()).join('')
})
const hasDisplayName = computed(() =>
  !!(authStore.user?.displayName && authStore.user.displayName.trim())
)
const primaryName = computed(() =>
  hasDisplayName.value ? authStore.user?.displayName : authStore.user?.name
)
const secondaryName = computed(() =>
  hasDisplayName.value ? authStore.user?.name : null
)

// --- Theme -----------------------------------------------------------------
const themeOptions = computed(() => [
  { value: 'system', icon: 'i-lucide-monitor', label: t('profile.theme.system') },
  { value: 'light', icon: 'i-lucide-sun', label: t('profile.theme.light') },
  { value: 'dark', icon: 'i-lucide-moon', label: t('profile.theme.dark') }
])

// --- Preference select drawer ----------------------------------------------
const activeSelect = ref<PreferenceField | null>(null)
const savingField = ref<PreferenceField | null>(null)

const selectConfig = computed(() => {
  switch (activeSelect.value) {
    case 'language':
      return { title: t('profile.language'), items: languageSelectOptions.value, value: authStore.user?.language, searchable: false }
    case 'currency':
      return { title: t('profile.currency'), items: currencySelectOptions.value, value: authStore.user?.currency, searchable: true }
    case 'timeZone':
      return { title: t('profile.timeZone'), items: timeZoneSelectOptions.value, value: authStore.user?.timeZone, searchable: true }
    default:
      return null
  }
})

async function openSelect(field: PreferenceField) {
  activeSelect.value = field
  await loadSelectOptions()
}

async function onSavePreference(value: string) {
  const field = activeSelect.value
  if (!field) return
  savingField.value = field
  try {
    await savePreference(field, value)
  } finally {
    savingField.value = null
  }
}

// --- Account sub-surface drawers -------------------------------------------
const securityOpen = ref(false)
const notificationsOpen = ref(false)
const familyOpen = ref(false)

// --- Edit profile drawer (avatar + name) -----------------------------------
const nameDrawerOpen = ref(false)
const savingName = ref(false)
const nameForm = ref({ name: '', displayName: '' })

function openEditProfile() {
  nameForm.value = {
    name: authStore.user?.name || '',
    displayName: authStore.user?.displayName || ''
  }
  nameDrawerOpen.value = true
}

async function onSaveName() {
  if (!nameForm.value.name.trim()) return
  savingName.value = true
  try {
    const ok = await saveName(nameForm.value.name.trim(), nameForm.value.displayName.trim())
    if (ok) nameDrawerOpen.value = false
  } finally {
    savingName.value = false
  }
}

// --- Version ---------------------------------------------------------------
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
  // The store is normally already populated by auth initialize(); only fetch
  // when it isn't, so we never blank the header on a warm navigation.
  if (!authStore.user) {
    await authStore.fetchUserFromBackend()
  }
  loading.value = false

  // Deep-link / reauth-return: ?open=security|notifications|family reopens the
  // matching drawer (e.g. after the passkey re-auth redirect).
  const open = route.query.open
  if (open === 'security') securityOpen.value = true
  else if (open === 'notifications') notificationsOpen.value = true
  else if (open === 'family') familyOpen.value = true

  await fetchVersion()
})

// --- Avatar upload / delete ------------------------------------------------
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
      uploadStatus.value = 'idle'
      imageCropperOpen.value = true
    }
    reader.readAsDataURL(file)
  }

  input.click()
}

async function handleCroppedImage(base64: string) {
  // Keep the drawer open; switch it to the upload progress view.
  uploadStatus.value = 'inprogress'
  uploadProgress.value = 0
  uploadStage.value = 'validating'
  uploadErrorMessage.value = undefined

  try {
    const blob = await fetch(base64).then(r => r.blob())
    const compressed = await imageCompression(blob as File, {
      maxWidthOrHeight: 500,
      maxSizeMB: 0.5,
      useWebWorker: true
    })

    const reader = new FileReader()
    reader.onload = async () => {
      const compressedBase64 = reader.result as string
      const pureBase64 = extractBase64(compressedBase64)
      await uploadWithProgress(pureBase64)
    }
    reader.readAsDataURL(compressed)
  } catch (error) {
    console.error('Failed to process image:', error)
    uploadStatus.value = 'failed'
  }
}

async function uploadWithProgress(base64Data: string) {
  try {
    uploadProgress.value = 0
    uploadStage.value = 'validating'
    uploadStatus.value = 'inprogress'
    uploadErrorMessage.value = undefined

    const response = await uploadProfilePictureWithProgress({ imageBase64: base64Data })

    if (response.data?.jobId) {
      currentUploadJobId.value = response.data.jobId

      stopPolling = progressApi.pollProgress(response.data.jobId, async (progress) => {
        uploadProgress.value = progress.percentage
        uploadStage.value = progress.stage
        uploadStatus.value = progress.status
        uploadErrorMessage.value = progress.errorMessage

        if (progress.status === 'completed') {
          // Pull the canonical (server-processed) avatar into the store so the
          // navbar avatar and this page repaint together.
          await authStore.fetchUserFromBackend()
          setTimeout(() => {
            handleCloseUpload()
          }, 500)
        }
      })
    }
  } catch (error) {
    console.error('Failed to start upload:', error)
    uploadStatus.value = 'failed'
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
  uploadStatus.value = 'cancelled'
}

function handleCloseUpload() {
  if (stopPolling) {
    stopPolling()
    stopPolling = null
  }
  imageCropperOpen.value = false
  currentUploadJobId.value = null
  uploadStatus.value = 'idle'
}

async function onDeleteAvatar() {
  await deleteProfilePicture()
  await authStore.fetchUserFromBackend()
}

async function onLogout() {
  await authStore.logout()
}
</script>
