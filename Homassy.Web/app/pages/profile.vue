<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <div class="flex items-center gap-3">
      <UIcon name="i-lucide-user" class="h-7 w-7 text-primary-500" />
      <h1 class="text-2xl font-semibold">Profile</h1>
    </div>

    <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
      <div class="flex items-center gap-4">
        <UAvatar :src="avatarSrc" :alt="authStore.user?.displayName || authStore.user?.name || 'User'" size="xl" class="ring-2 ring-primary-200/50" />
        <div>
          <p class="text-lg font-medium">{{ authStore.user?.displayName || authStore.user?.name }}</p>
          <p class="text-sm text-gray-600 dark:text-gray-400">{{ authStore.user?.language }}  {{ authStore.user?.currency }}  {{ authStore.user?.timeZone }}</p>
        </div>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UColorModeButton />
        <UButton color="primary" variant="soft" @click="triggerFileSelect">
          <UIcon name="i-lucide-upload" class="h-4 w-4 mr-1" />
          Upload Avatar
        </UButton>
        <input ref="fileInput" type="file" accept="image/*" class="hidden" @change="onFileSelected">
        <UButton v-if="hasAvatar" color="error" variant="soft" @click="onDeleteAvatar">
          <UIcon name="i-lucide-trash-2" class="h-4 w-4 mr-1" />
          Delete Avatar
        </UButton>
        <UButton color="neutral" variant="soft" @click="onLogout">
          <UIcon name="i-lucide-log-out" class="h-4 w-4 mr-1" />
          Logout
        </UButton>
      </div>
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <div class="rounded-lg border border-gray-200/70 dark:border-gray-800/60 p-4">
        <h2 class="text-base font-semibold mb-2">Account</h2>
        <div class="space-y-1 text-sm">
          <div class="flex justify-between"><span>Name</span><span class="text-gray-700 dark:text-gray-300">{{ authStore.user?.name }}</span></div>
          <div class="flex justify-between"><span>Display Name</span><span class="text-gray-700 dark:text-gray-300">{{ authStore.user?.displayName || '-' }}</span></div>
          <div class="flex justify-between"><span>Language</span><span class="text-gray-700 dark:text-gray-300">{{ authStore.user?.language }}</span></div>
          <div class="flex justify-between"><span>Currency</span><span class="text-gray-700 dark:text-gray-300">{{ authStore.user?.currency }}</span></div>
          <div class="flex justify-between"><span>Time Zone</span><span class="text-gray-700 dark:text-gray-300">{{ authStore.user?.timeZone }}</span></div>
        </div>
      </div>
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
</script>
