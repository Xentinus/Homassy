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

    <div v-if="loading" class="flex justify-center items-center py-10">
      <span>{{ $t('common.loading') }}</span>
    </div>

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
    <div v-else class="text-center text-gray-500 dark:text-gray-400 py-10">
      {{ $t('profile.family.noFamily') }}
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'auth', middleware: 'auth' })
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useFamilyApi } from '~/composables/api/useFamilyApi'

const { getFamily, leaveFamily } = useFamilyApi()
const router = useRouter()

const family = ref<any>(null)
const loading = ref(true)

async function fetchFamily() {
  loading.value = true
  try {
    const res = await getFamily()
    family.value = res.data
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
