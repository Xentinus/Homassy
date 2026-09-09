<template>
  <div>
    <!-- Content Section (page identity lives in the persistent AppHeader) -->
    <div class="px-4 sm:px-8 lg:px-14 pb-6 space-y-6 max-w-2xl mx-auto">
      <!-- Identity card: avatar + name; tap to open the edit-profile drawer -->
      <ProfileIdentityCard
        :loading="loading"
        :avatar-src="avatarSrc"
        :primary-name="primaryName"
        :secondary-name="secondaryName"
        :public-id="authStore.user?.publicId"
        :identity-color="authStore.user?.identityColor"
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

        <!-- Vibration is device-local and absent on iOS Safari, so the row only
             exists where the browser actually has the Vibration API. -->
        <ClientOnly>
          <SettingsRow
            v-if="hapticsSupported"
            static
            :chevron="false"
            :label="$t('profile.haptics.label')"
            :description="$t('profile.haptics.description')"
            icon="i-lucide-vibrate"
          >
            <template #trailing>
              <USwitch
                v-model="hapticsEnabled"
                :aria-label="$t('profile.haptics.label')"
                @update:model-value="onHapticsToggle"
              />
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

      <!-- Edit profile bottom sheet: avatar + name / display name + identity colour -->
      <SettingsEditDrawer
        :open="nameDrawerOpen"
        :title="$t('profile.editProfile.title')"
        icon="i-lucide-user"
        :loading="savingProfile"
        :save-disabled="!nameForm.name.trim()"
        @update:open="(v) => nameDrawerOpen = v"
        @save="onSaveProfile"
        @cancel="nameDrawerOpen = false"
      >
        <template #body>
          <div class="space-y-6">
            <!-- Avatar: the ring is UserAvatar's own member-identity ring, not a wrapper
                 (a wrapper around its inline-flex box painted a non-circular, hardcoded-colour
                 ring — see Item 1 of the fix branch). -->
            <div class="flex flex-col items-center gap-3">
              <UserAvatar
                :src="avatarSrc"
                :name="primaryName"
                :public-id="authStore.user?.publicId"
                :identity-color="authStore.user?.identityColor"
                :size="96"
              />
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

            <!-- Identity colour: eight palette swatches + "Automatic", draft selection ringed.
                 Local drawer state, seeded from the store each time this drawer opens (see
                 openEditProfile) and applied together with name/display name only on Save — a
                 tap here just changes the draft, it never fires a request on its own. Wrapped in
                 ClientOnly like the theme control on the Preferences group — a swatch's shade
                 follows the live colour mode, which is only known client-side. -->
            <ClientOnly>
              <div class="flex flex-col gap-3">
                <div class="flex items-center gap-3">
                  <UIcon name="i-lucide-swatch-book" class="h-5 w-5 shrink-0 text-primary-500" />
                  <div class="flex-1 min-w-0">
                    <p class="font-medium">{{ $t('profile.settings.identityColor.label') }}</p>
                    <p class="text-xs text-muted">{{ $t('profile.settings.identityColor.description') }}</p>
                  </div>
                </div>
                <div class="flex flex-wrap gap-2 pl-8">
                  <button
                    v-for="option in paletteOptions"
                    :key="option.value"
                    type="button"
                    class="h-7 w-7 rounded-full flex items-center justify-center transition-transform active:scale-95 disabled:opacity-50 disabled:pointer-events-none"
                    :class="draftIdentityColor === option.value
                      ? 'ring-2 ring-inset ring-primary-500'
                      : 'ring-1 ring-inset ring-black/10 dark:ring-white/10'"
                    :style="{ backgroundColor: option.swatch }"
                    :disabled="savingProfile"
                    :aria-label="option.value === 'auto' ? $t('profile.settings.identityColor.auto') : $t(`profile.settings.identityColor.colors.${option.value}`)"
                    :aria-pressed="draftIdentityColor === option.value"
                    @click="draftIdentityColor = option.value"
                  >
                    <UIcon v-if="option.value === 'auto'" name="i-lucide-shuffle" class="h-3.5 w-3.5 text-white" />
                  </button>
                </div>
              </div>
            </ClientOnly>
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
import { isMemberColorKey, type MemberColorKey } from '~/utils/memberColors'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const authStore = useAuthStore()
const { uploadProfilePictureWithProgress, deleteProfilePicture } = useUserApi()
const progressApi = useProgressApi()
const { getVersion } = useVersionApi()
const { t } = useI18n()
const colorMode = useColorMode()
const { isSupported: hapticsSupported, enabled: hapticsEnabled, select: hapticSelect } = useHaptics()
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
  saveProfile
} = useUserPreferences()
const { paletteOptions } = useMemberColor()

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
const hasAvatar = computed(() => !!authStore.user?.profilePictureUrl)
const avatarSrc = computed(() => authStore.user?.profilePictureUrl ?? undefined)
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
// Fire the pattern the moment it is switched on, so the setting demonstrates itself.
const onHapticsToggle = (value: boolean) => {
  if (value) hapticSelect()
}

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

// --- Edit profile drawer (avatar + name + identity colour) -----------------
const nameDrawerOpen = ref(false)
const savingProfile = ref(false)
const nameForm = ref({ name: '', displayName: '' })
// Draft only: seeded from the store below each time the drawer opens, applied together with
// name/display name on Save (see saveProfile), and discarded on cancel by virtue of never being
// read again before the next open reseeds it.
const draftIdentityColor = ref<MemberColorKey | 'auto'>('auto')

function openEditProfile() {
  nameForm.value = {
    name: authStore.user?.name || '',
    displayName: authStore.user?.displayName || ''
  }
  // Narrows the store's free-form string down to a known palette key, same fallback
  // `resolveMemberColor` itself uses for an unrecognised or absent value.
  const storedColor = authStore.user?.identityColor
  draftIdentityColor.value = isMemberColorKey(storedColor) ? storedColor : 'auto'
  nameDrawerOpen.value = true
}

async function onSaveProfile() {
  if (!nameForm.value.name.trim()) return
  savingProfile.value = true
  try {
    const ok = await saveProfile(
      nameForm.value.name.trim(),
      nameForm.value.displayName.trim(),
      draftIdentityColor.value
    )
    if (ok) nameDrawerOpen.value = false
  } finally {
    savingProfile.value = false
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
