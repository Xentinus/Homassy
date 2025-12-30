<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header -->
    <div class="flex items-center gap-3">
      <UIcon name="i-lucide-user" class="h-7 w-7 text-primary-500" />
      <h1 class="text-2xl font-semibold">{{ $t('profile.title') }}</h1>
    </div>

    <!-- Avatar Section -->
    <div class="flex flex-col items-center gap-4">
      <!-- Avatar with delete icon overlay -->
      <div class="relative inline-block">
        <UAvatar
          :src="avatarSrc"
          :alt="primaryName || 'User'"
          :text="avatarInitial"
          class="h-40 w-40 ring-4 ring-primary-200/50 text-6xl"
        />
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
      <input ref="fileInput" type="file" accept="image/*" class="hidden" @change="onFileSelected">
    </div>

    <!-- Settings Card -->
    <NuxtLink to="/profile/settings">
      <div class="rounded-lg border border-gray-200/70 dark:border-gray-800/60 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer">
        <UIcon
          name="i-lucide-chevron-right"
          class="absolute top-3 right-3 text-gray-400"
        />

        <h2 class="text-base font-semibold mb-3">{{ $t('profile.settings') }}</h2>

        <div class="space-y-2 text-sm">
          <div class="flex justify-between">
            <span class="text-gray-600 dark:text-gray-400">{{ $t('profile.language') }}</span>
            <span class="font-medium">{{ authStore.user?.language }}</span>
          </div>
          <div class="flex justify-between">
            <span class="text-gray-600 dark:text-gray-400">{{ $t('profile.currency') }}</span>
            <span class="font-medium">{{ authStore.user?.currency }}</span>
          </div>
          <div class="flex justify-between">
            <span class="text-gray-600 dark:text-gray-400">{{ $t('profile.timeZone') }}</span>
            <span class="font-medium">{{ authStore.user?.timeZone }}</span>
          </div>
        </div>
      </div>
    </NuxtLink>

    <!-- Action Buttons -->
    <div class="space-y-3 mt-8">
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

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const authStore = useAuthStore()
const { uploadProfilePicture, deleteProfilePicture } = useUserApi()

const fileInput = ref<HTMLInputElement | null>(null)
const hasAvatar = computed(() => !!authStore.user?.profilePictureBase64)
const avatarSrc = computed(() => {
  const b64 = authStore.user?.profilePictureBase64
  return b64 ? `data:image/jpeg;base64,${b64}` : undefined
})

const avatarInitial = computed(() => {
  const name = authStore.user?.displayName || authStore.user?.name
  if (!name) return '?'

  const words = name.trim().split(/\s+/)
  return words.map((word: string) => word.charAt(0).toUpperCase()).join('')
})

// Name display
const hasDisplayName = computed(() =>
  !!(authStore.user?.displayName && authStore.user.displayName.trim())
)
const primaryName = computed(() =>
  hasDisplayName.value ? authStore.user?.displayName : authStore.user?.name
)
const secondaryName = computed(() =>
  hasDisplayName.value ? authStore.user?.name : null
)

// Color mode
const { t } = useI18n()
const colorMode = useColorMode()
const colorModeText = computed(() =>
  colorMode.value === 'dark' ? t('profile.lightMode') : t('profile.darkMode')
)

onMounted(() => {
  // Auth middleware has already restored user via refreshAccessToken
})

function triggerFileSelect() {
  fileInput.value?.click()
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  const base64 = await readFileAsBase64(file)
  const pureBase64 = base64.includes(',') ? base64.split(',')[1] : base64

  await uploadProfilePicture({ imageBase64: pureBase64 })
  await authStore.fetchCurrentUser()
  input.value = ''
}

async function onDeleteAvatar() {
  await deleteProfilePicture()
  await authStore.fetchCurrentUser()
}

async function onLogout() {
  await authStore.logout()
}

function readFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(file)
  })
}

function toggleColorMode() {
  colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'
}
</script>
