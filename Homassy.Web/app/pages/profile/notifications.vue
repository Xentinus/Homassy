<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-8">
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

    <!-- Email Notifications Section -->
    <div class="space-y-4">
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-mail" class="text-2xl text-primary" />
        <div>
          <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.notifications.emailSection') }}</h3>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.emailDescription') }}</p>
        </div>
      </div>
      
      <template v-if="loadingData">
        <USkeleton class="h-12 w-full rounded-lg" />
        <USkeleton class="h-12 w-full rounded-lg" />
      </template>
      <template v-else>
        <div class="space-y-4">
          <div class="flex items-center justify-between">
            <label class="text-sm font-medium">{{ $t('profile.notifications.emailNotifications') }}</label>
            <USwitch 
              v-model="form.emailNotificationsEnabled" 
              @update:modelValue="onSave"
              :disabled="isSaving"
              :loading="isSaving"
            />
          </div>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.emailDailyDescription') }}</p>
          <div class="flex items-center justify-between">
            <label class="text-sm font-medium">{{ $t('profile.notifications.emailWeeklySummary') }}</label>
            <USwitch 
              v-model="form.emailWeeklySummaryEnabled"
              @update:modelValue="onSave"
              :disabled="isSaving"
              :loading="isSaving"
            />
          </div>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.emailWeeklyDescription') }}</p>
        </div>
      </template>
    </div>

    <!-- Push Notifications Section -->
    <div class="space-y-4">
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-bell-ring" class="text-2xl text-primary" />
        <div>
          <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.notifications.pushSection') }}</h3>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.pushDescription') }}</p>
        </div>
      </div>

      <template v-if="loadingPush">
        <USkeleton class="h-12 w-full rounded-lg" />
        <USkeleton class="h-12 w-full rounded-lg" />
      </template>
      <template v-else>
        <div v-if="!pushSupported" class="text-sm text-amber-600 dark:text-amber-400 p-3 rounded-lg bg-amber-50 dark:bg-amber-900/20 flex items-center gap-2">
          <UIcon name="i-lucide-alert-circle" class="text-base flex-shrink-0" />
          {{ $t('profile.notifications.pushNotSupported') }}
        </div>
        <div v-else-if="pushPermission === 'denied'" class="text-sm text-red-600 dark:text-red-400 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 flex items-center gap-2">
          <UIcon name="i-lucide-x-circle" class="text-base flex-shrink-0" />
          {{ $t('profile.notifications.pushDenied') }}
        </div>
        <div class="space-y-4" v-if="pushSupported && pushPermission !== 'denied'">
          <div class="flex items-center justify-between">
            <label class="text-sm font-medium">{{ $t('profile.notifications.pushNotifications') }}</label>
            <USwitch 
              v-model="form.pushNotificationsEnabled"
              @update:modelValue="onSave"
              :disabled="isSaving"
              :loading="isSaving"
            />
          </div>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.pushDailyDescription') }}</p>
          <div class="flex items-center justify-between">
            <label class="text-sm font-medium">{{ $t('profile.notifications.pushWeeklySummary') }}</label>
            <USwitch 
              v-model="form.pushWeeklySummaryEnabled"
              @update:modelValue="onSave"
              :disabled="isSaving"
              :loading="isSaving"
            />
          </div>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.pushWeeklyDescription') }}</p>
          <UButton 
            color="primary" 
            variant="solid" 
            class="w-full font-semibold mt-4 mb-3" 
            icon="i-lucide-send"
            @click="sendTestNotification"
            :disabled="isSaving || testingNotification || pushPermission !== 'granted'"
            :loading="testingNotification"
          >
            {{ $t('profile.notifications.testNotification') }}
          </UButton>
        </div>
      </template>
    </div>

    <!-- In-App Notifications Section -->
    <div class="space-y-4">
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-inbox" class="text-2xl text-primary" />
        <div>
          <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ $t('profile.notifications.inAppSection') }}</h3>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ $t('profile.notifications.inAppDescription') }}</p>
        </div>
      </div>

      <template v-if="loadingData">
        <USkeleton class="h-12 w-full rounded-lg" />
      </template>
      <template v-else>
        <div class="flex items-center justify-between">
          <label class="text-sm font-medium">{{ $t('profile.notifications.inAppNotifications') }}</label>
          <USwitch 
            v-model="form.inAppNotificationsEnabled"
            @update:modelValue="onSave"
            :disabled="isSaving"
            :loading="isSaving"
          />
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'auth', middleware: 'auth' })
import { ref, onMounted } from 'vue'

import { useUserApi } from '~/composables/api/useUserApi'
import { usePushNotifications } from '~/composables/usePushNotifications'
import { useRouter } from 'vue-router'

const form = ref({
  emailNotificationsEnabled: false,
  emailWeeklySummaryEnabled: false,
  pushNotificationsEnabled: false,
  pushWeeklySummaryEnabled: false,
  inAppNotificationsEnabled: false
})
const loadingData = ref(true)
const loadingPush = ref(true)
const isSaving = ref(false)
const testingNotification = ref(false)
const pushSubscribed = ref(false)
const { getNotificationPreferences, updateNotificationPreferences, sendTestPushNotification } = useUserApi()
const { isSupported, permissionStatus, subscribe, unsubscribe, isSubscribed } = usePushNotifications()

const pushSupported = computed(() => isSupported.value)
const pushPermission = computed(() => permissionStatus.value)

onMounted(async () => {
  loadingData.value = true
  loadingPush.value = true
  
  try {
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
    loadingData.value = false
    
    if (isSupported.value) {
      try {
        const timeoutPromise = new Promise<boolean>((_, reject) => 
          setTimeout(() => reject(new Error('isSubscribed timeout')), 3000)
        )
        pushSubscribed.value = await Promise.race([isSubscribed(), timeoutPromise])
      } catch (error) {
        pushSubscribed.value = false
      }
    }
  } catch (error) {
    console.error('Failed to load notification preferences:', error)
    loadingData.value = false
  } finally {
    loadingPush.value = false
  }
})

const toast = useToast()
const { $i18n } = useNuxtApp()
const router = useRouter()

async function onSave() {
  isSaving.value = true
  try {
    const pushEnabled = form.value.pushNotificationsEnabled || form.value.pushWeeklySummaryEnabled

    // Subscribe if push is being enabled and not yet subscribed
    if (pushEnabled && !pushSubscribed.value) {
      const success = await subscribe()
      if (!success) {
        form.value.pushNotificationsEnabled = false
        form.value.pushWeeklySummaryEnabled = false
        toast.add({
          title: $i18n.t('toast.error'),
          description: $i18n.t('profile.notifications.pushPermissionRequired'),
          color: 'error',
          icon: 'i-heroicons-x-circle'
        })
        return
      }
      pushSubscribed.value = true
    }

    // Unsubscribe if both push options are disabled
    if (!pushEnabled && pushSubscribed.value) {
      await unsubscribe()
      pushSubscribed.value = false
    }

    await updateNotificationPreferences({ ...form.value })
  } catch (error) {
    console.error('Failed to save notification preferences:', error)
    toast.add({
      title: $i18n.t('toast.error'),
      description: $i18n.t('profile.notifications.saveFailed'),
      color: 'error',
      icon: 'i-heroicons-x-circle'
    })
  } finally {
    isSaving.value = false
  }
}

async function sendTestNotification() {
  testingNotification.value = true
  try {
    // Make sure push is subscribed
    if (!pushSubscribed.value) {
      toast.add({
        title: $i18n.t('toast.error'),
        description: $i18n.t('profile.notifications.testNotificationNotSubscribed'),
        color: 'error',
        icon: 'i-heroicons-x-circle'
      })
      return
    }

    // Call backend to send test notification
    const result = await sendTestPushNotification()
    
    if (!result?.success) {
      toast.add({
        title: $i18n.t('toast.error'),
        description: $i18n.t('profile.notifications.testNotificationFailed'),
        color: 'error',
        icon: 'i-heroicons-x-circle'
      })
    }
  } catch (error) {
    console.error('Failed to send test notification:', error)
    toast.add({
      title: $i18n.t('toast.error'),
      description: $i18n.t('profile.notifications.testNotificationFailed'),
      color: 'error',
      icon: 'i-heroicons-x-circle'
    })
  } finally {
    testingNotification.value = false
  }
}
</script>
