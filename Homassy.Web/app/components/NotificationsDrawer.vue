<template>
  <AppDrawer
    :open="open"
    :title="t('profile.notifications.title')"
    icon="i-lucide-bell"
    :loading="isSaving"
    @update:open="(value) => emit('update:open', value)"
  >
    <div class="space-y-8">
        <!-- Email -->
        <div class="space-y-4">
          <div class="flex items-center gap-3">
            <UIcon name="i-lucide-mail" class="text-2xl text-primary" />
            <div>
              <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ t('profile.notifications.emailSection') }}</h3>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.emailDescription') }}</p>
            </div>
          </div>

          <template v-if="loadingData">
            <USkeleton class="h-12 w-full rounded-lg" />
          </template>
          <template v-else>
            <div class="flex items-center justify-between">
              <label class="text-sm font-medium">{{ t('profile.notifications.emailWeeklySummary') }}</label>
              <USwitch v-model="form.emailWeeklySummaryEnabled" :disabled="isSaving" />
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.emailWeeklyDescription') }}</p>
            <UButton
              v-if="original.emailWeeklySummaryEnabled"
              color="primary"
              variant="solid"
              class="w-full font-semibold"
              icon="i-lucide-mail"
              :disabled="isSaving || testingEmail"
              :loading="testingEmail"
              @click="sendTestEmailNotification"
            >
              {{ t('profile.notifications.testEmail') }}
            </UButton>
          </template>
        </div>

        <!-- Push -->
        <div class="space-y-4">
          <div class="flex items-center gap-3">
            <UIcon name="i-lucide-bell-ring" class="text-2xl text-primary" />
            <div>
              <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ t('profile.notifications.pushSection') }}</h3>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.pushDescription') }}</p>
            </div>
          </div>

          <template v-if="loadingPush">
            <USkeleton class="h-12 w-full rounded-lg" />
          </template>
          <template v-else>
            <div v-if="!pushSupported" class="text-sm text-amber-600 dark:text-amber-400 p-3 rounded-lg bg-amber-50 dark:bg-amber-900/20 flex items-center gap-2">
              <UIcon name="i-lucide-alert-circle" class="text-base flex-shrink-0" />
              {{ t('profile.notifications.pushNotSupported') }}
            </div>
            <div v-else-if="pushPermission === 'denied'" class="text-sm text-red-600 dark:text-red-400 p-3 rounded-lg bg-red-50 dark:bg-red-900/20 flex items-center gap-2">
              <UIcon name="i-lucide-x-circle" class="text-base flex-shrink-0" />
              {{ t('profile.notifications.pushDenied') }}
            </div>
            <div v-else class="space-y-4">
              <div class="flex items-center justify-between">
                <label class="text-sm font-medium">{{ t('profile.notifications.pushNotifications') }}</label>
                <USwitch v-model="form.pushNotificationsEnabled" :disabled="isSaving" />
              </div>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.pushDailyDescription') }}</p>
              <div class="flex items-center justify-between">
                <label class="text-sm font-medium">{{ t('profile.notifications.pushWeeklySummary') }}</label>
                <USwitch v-model="form.pushWeeklySummaryEnabled" :disabled="isSaving || !form.pushNotificationsEnabled" />
              </div>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.pushWeeklyDescription') }}</p>
              <UButton
                v-if="pushSubscribed && original.pushNotificationsEnabled"
                color="primary"
                variant="solid"
                class="w-full font-semibold"
                icon="i-lucide-send"
                :disabled="isSaving || testingNotification"
                :loading="testingNotification"
                @click="sendTestNotification"
              >
                {{ t('profile.notifications.testNotification') }}
              </UButton>
            </div>
          </template>
        </div>
      </div>

    <template #footer>
      <UButton :label="t('common.cancel')" color="neutral" variant="ghost" :disabled="isSaving" @click="emit('update:open', false)" />
      <UButton
        :label="t('common.save')"
        color="primary"
        icon="i-lucide-save"
        :loading="isSaving"
        :disabled="!hasChanges"
        @click="onSave"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useUserApi } from '~/composables/api/useUserApi'
import { usePushNotifications } from '~/composables/usePushNotifications'

interface NotificationForm {
  emailNotificationsEnabled: boolean
  emailWeeklySummaryEnabled: boolean
  pushNotificationsEnabled: boolean
  pushWeeklySummaryEnabled: boolean
  inAppNotificationsEnabled: boolean
}

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { t } = useI18n()
const toast = useToast()
const { getNotificationPreferences, updateNotificationPreferences, sendTestPushNotification, sendTestEmailSummary } = useUserApi()
const { isSupported, permissionStatus, subscribe, unsubscribe, isSubscribed } = usePushNotifications()

const emptyForm = (): NotificationForm => ({
  emailNotificationsEnabled: false,
  emailWeeklySummaryEnabled: false,
  pushNotificationsEnabled: false,
  pushWeeklySummaryEnabled: false,
  inAppNotificationsEnabled: false
})

const form = ref<NotificationForm>(emptyForm())
const original = ref<NotificationForm>(emptyForm())
const loadingData = ref(true)
const loadingPush = ref(true)
const isSaving = ref(false)
const testingNotification = ref(false)
const testingEmail = ref(false)
const pushSubscribed = ref(false)

const pushSupported = computed(() => isSupported.value)
const pushPermission = computed(() => permissionStatus.value)
const hasChanges = computed(() => JSON.stringify(form.value) !== JSON.stringify(original.value))

// Turning push off cascades to the weekly push summary (local buffer only).
watch(() => form.value.pushNotificationsEnabled, (enabled) => {
  if (!enabled) form.value.pushWeeklySummaryEnabled = false
})

watch(() => props.open, async (isOpen) => {
  if (!isOpen) return
  loadingData.value = true
  loadingPush.value = true
  try {
    const res = await getNotificationPreferences()
    const seed: NotificationForm = res?.data
      ? {
          emailNotificationsEnabled: !!res.data.emailNotificationsEnabled,
          emailWeeklySummaryEnabled: !!res.data.emailWeeklySummaryEnabled,
          pushNotificationsEnabled: !!res.data.pushNotificationsEnabled,
          pushWeeklySummaryEnabled: !!res.data.pushWeeklySummaryEnabled,
          inAppNotificationsEnabled: !!res.data.inAppNotificationsEnabled
        }
      : emptyForm()
    form.value = { ...seed }
    original.value = { ...seed }
    loadingData.value = false

    if (isSupported.value) {
      try {
        const timeout = new Promise<boolean>((_, reject) => setTimeout(() => reject(new Error('isSubscribed timeout')), 3000))
        pushSubscribed.value = await Promise.race([isSubscribed(), timeout])
      } catch {
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

async function onSave() {
  isSaving.value = true
  try {
    const pushDesired = form.value.pushNotificationsEnabled || form.value.pushWeeklySummaryEnabled

    if (pushDesired && !pushSubscribed.value) {
      const success = await subscribe()
      if (!success) {
        form.value.pushNotificationsEnabled = false
        form.value.pushWeeklySummaryEnabled = false
        const isPermissionIssue = Notification.permission !== 'granted'
        toast.add({
          title: t('toast.error'),
          description: t(isPermissionIssue ? 'profile.notifications.pushPermissionRequired' : 'profile.notifications.pushSubscriptionFailed'),
          color: 'error',
          icon: 'i-heroicons-x-circle'
        })
        return
      }
      pushSubscribed.value = true
    }

    if (!pushDesired && pushSubscribed.value) {
      await unsubscribe()
      pushSubscribed.value = false
    }

    await updateNotificationPreferences({ ...form.value })
    original.value = { ...form.value }
    emit('update:open', false)
  } catch (error) {
    console.error('Failed to save notification preferences:', error)
    toast.add({
      title: t('toast.error'),
      description: t('profile.notifications.saveFailed'),
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
    if (!pushSubscribed.value) {
      toast.add({ title: t('toast.error'), description: t('profile.notifications.testNotificationNotSubscribed'), color: 'error', icon: 'i-heroicons-x-circle' })
      return
    }
    const result = await sendTestPushNotification()
    if (!result?.success) {
      toast.add({ title: t('toast.error'), description: t('profile.notifications.testNotificationFailed'), color: 'error', icon: 'i-heroicons-x-circle' })
    }
  } catch (error) {
    console.error('Failed to send test notification:', error)
    toast.add({ title: t('toast.error'), description: t('profile.notifications.testNotificationFailed'), color: 'error', icon: 'i-heroicons-x-circle' })
  } finally {
    testingNotification.value = false
  }
}

async function sendTestEmailNotification() {
  testingEmail.value = true
  try {
    const result = await sendTestEmailSummary()
    if (result?.success) {
      toast.add({ title: t('toast.success'), description: t('profile.notifications.testEmailSent'), color: 'success', icon: 'i-heroicons-check-circle' })
    } else {
      toast.add({ title: t('toast.error'), description: t('profile.notifications.testEmailFailed'), color: 'error', icon: 'i-heroicons-x-circle' })
    }
  } catch (error) {
    console.error('Failed to send test email:', error)
    toast.add({ title: t('toast.error'), description: t('profile.notifications.testEmailFailed'), color: 'error', icon: 'i-heroicons-x-circle' })
  } finally {
    testingEmail.value = false
  }
}
</script>
