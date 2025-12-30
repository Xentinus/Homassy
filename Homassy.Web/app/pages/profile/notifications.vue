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
      <UIcon name="i-lucide-bell" class="text-xl text-primary" />
      <div>
        <h1 class="text-2xl font-semibold">{{ $t('profile.notifications.title') }}</h1>
      </div>
    </div>

    <template v-if="loading">
      <USkeleton class="h-8 w-1/2 rounded mb-2 mt-4" />
      <USkeleton class="h-4 w-2/3 rounded mb-4" />
      <USkeleton class="h-12 w-full rounded-lg mb-2" />
      <USkeleton class="h-12 w-full rounded-lg mb-2" />
      <USkeleton class="h-12 w-full rounded-lg mb-2" />
      <USkeleton class="h-12 w-full rounded-lg mb-2" />
      <USkeleton class="h-12 w-full rounded-lg mb-2" />
      <USkeleton class="h-10 w-full rounded-lg mt-6" />
      <USkeleton class="h-10 w-full rounded-lg" />
    </template>
    <UForm v-else @submit.prevent="onSave" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6 mt-4 space-y-6">
      <h2 class="text-lg font-semibold mb-2">{{ $t('profile.notifications.title') }}</h2>
      <p class="text-gray-600 dark:text-gray-400 mb-4">{{ $t('profile.notifications.cardDescription') }}</p>
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium">{{ $t('profile.notifications.emailNotifications') }}</label>
          <USwitch v-model="form.emailNotificationsEnabled" />
        </div>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium">{{ $t('profile.notifications.emailWeeklySummary') }}</label>
          <USwitch v-model="form.emailWeeklySummaryEnabled" />
        </div>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium">{{ $t('profile.notifications.pushNotifications') }}</label>
          <USwitch v-model="form.pushNotificationsEnabled" />
        </div>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium">{{ $t('profile.notifications.pushWeeklySummary') }}</label>
          <USwitch v-model="form.pushWeeklySummaryEnabled" />
        </div>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium">{{ $t('profile.notifications.inAppNotifications') }}</label>
          <USwitch v-model="form.inAppNotificationsEnabled" />
        </div>
      </div>
      <div class="space-y-3 mt-6">
        <UButton type="submit" color="primary" class="w-full" icon="i-lucide-save">
          {{ $t('common.save') }}
        </UButton>
        <NuxtLink to="/profile" class="block">
          <UButton color="neutral" variant="soft" class="w-full" icon="i-lucide-x">
            {{ $t('common.cancel') }}
          </UButton>
        </NuxtLink>
      </div>
    </UForm>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'auth', middleware: 'auth' })
import { ref, onMounted } from 'vue'

import { useUserApi } from '~/composables/api/useUserApi'
import { useRouter } from 'vue-router'

const form = ref({
  emailNotificationsEnabled: false,
  emailWeeklySummaryEnabled: false,
  pushNotificationsEnabled: false,
  pushWeeklySummaryEnabled: false,
  inAppNotificationsEnabled: false
})
const loading = ref(true)
const { getNotificationPreferences, updateNotificationPreferences } = useUserApi()

onMounted(async () => {
  loading.value = true
  const res = await getNotificationPreferences()
  if (res?.data) {
    form.value = {
      emailNotificationsEnabled: !!res.data.emailNotificationsEnabled,
      emailWeeklySummaryEnabled: !!res.data.emailWeeklySummaryEnabled,
      pushNotificationsEnabled: !!res.data.pushNotificationsEnabled,
      pushWeeklySummaryEnabled: !!res.data.pushWeeklySummaryEnabled,
      inAppNotificationsEnabled: !!res.data.inAppNotificationsEnabled
    }
  }
  loading.value = false
})

const router = useRouter()
async function onSave() {
  await updateNotificationPreferences({ ...form.value })
  router.push('/profile')
}
</script>
