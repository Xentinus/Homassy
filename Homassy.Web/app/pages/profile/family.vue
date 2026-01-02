<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header with back button -->
    <div class="flex items-center gap-3">
      <NuxtLink to="/profile">
        <UButton
          icon="i-lucide-arrow-left"
          color="neutral"
          variant="ghost"
        />
      </NuxtLink>
      <UIcon name="i-lucide-users" class="text-xl text-primary" />
      <div>
        <h1 class="text-2xl font-semibold">{{ $t('profile.family.title') }}</h1>
      </div>
    </div>

    <template v-if="loading">
      <USkeleton class="h-8 w-1/2 rounded mb-2 mt-4" />
      <USkeleton class="h-4 w-2/3 rounded mb-4" />
      <USkeleton class="h-16 w-16 rounded-full mb-2" />
      <USkeleton class="h-6 w-32 mb-2" />
      <USkeleton class="h-4 w-40 mb-2" />
      <USkeleton class="h-4 w-24 mb-2" />
      <USkeleton class="h-8 w-1/3 mb-2" />
      <USkeleton class="h-10 w-full rounded-lg mt-6" />
    </template>
    <div v-else-if="family" class="space-y-6">
      <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 flex flex-col items-center">
        <div v-if="family.familyPictureBase64" class="mb-2">
          <UAvatar :src="`data:image/jpeg;base64,${family.familyPictureBase64}`" :alt="family.name" class="h-16 w-16" />
        </div>
        <div class="text-lg font-semibold">{{ family.name }}</div>
        <div v-if="family.description" class="text-sm text-gray-500 dark:text-gray-400 mb-1">
          {{ family.description }}
        </div>
        <div class="text-xs text-gray-400 mt-2">
          <span>{{ $t('profile.family.codeLabel') }}</span>
        </div>
        <div class="text-2xl font-extrabold text-primary-600 tracking-widest mt-1" style="text-transform: uppercase;">
          {{ family.shareCode }}
        </div>
        <UButton color="error" variant="soft" class="w-full mt-4 flex items-center justify-start gap-2" @click="onLeaveFamily">
          <UIcon name="i-lucide-log-out" class="h-4 w-4" />
          {{ $t('profile.family.leave') }}
        </UButton>
      </div>
    </div>
    <div v-else class="space-y-6">
      <!-- Create Family Card -->
      <NuxtLink to="/profile/create-family">
        <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full">
          <UIcon name="i-lucide-plus-circle" class="h-7 w-7 text-primary-500 mr-2" />
          <div>
            <h2 class="text-base font-semibold mb-1">{{ $t('profile.family.create') }}</h2>
            <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.createFamilyCard') }}</div>
          </div>
          <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
        </div>
      </NuxtLink>

      <!-- Join Family Card -->
      <NuxtLink to="/profile/join-family">
        <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-center gap-3 h-full mt-6">
          <UIcon name="i-lucide-user-plus" class="h-7 w-7 text-primary-500 mr-2" />
          <div>
            <h2 class="text-base font-semibold mb-1">{{ $t('profile.family.join') }}</h2>
            <div class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.family.joinFamilyCard') }}</div>
          </div>
          <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
        </div>
      </NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useFamilyApi } from '~/composables/api/useFamilyApi'
import type { FamilyDetailsResponse } from '~/types/family'

definePageMeta({ layout: 'auth', middleware: 'auth' })

const { getFamily, leaveFamily } = useFamilyApi()
const router = useRouter()

const family = ref<FamilyDetailsResponse | null>(null)
const loading = ref(true)

async function fetchFamily() {
  loading.value = true
  try {
    const res = await getFamily()
    family.value = res.data ?? null
  } catch {
    family.value = null
  } finally {
    loading.value = false
  }
}

onMounted(fetchFamily)

async function onLeaveFamily() {
  await leaveFamily()
  family.value = null
  router.push('/profile')
}
</script>
