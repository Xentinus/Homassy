<template>
  <AppDrawer
    :open="open"
    :title="t('profile.notifications.title')"
    icon="i-lucide-bell-cog"
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
              <!-- Family chat (#149). Its own switch, subordinate to the push one above it: a
                   family that chats all evening has to be able to silence the chat without
                   losing expiration alerts, which turning push off entirely would cost them. -->
              <div class="flex items-center justify-between">
                <label class="text-sm font-medium">{{ t('profile.notifications.pushFamilyChat') }}</label>
                <USwitch v-model="form.pushFamilyChatEnabled" :disabled="isSaving || !form.pushNotificationsEnabled" />
              </div>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.pushFamilyChatDescription') }}</p>
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

        <!-- In-app (#116). The channel the notification centre reads; unlike the two above it
             does not interrupt anything, which is why it is the one that defaults to on. -->
        <div class="space-y-4">
          <div class="flex items-center gap-3">
            <UIcon name="i-lucide-inbox" class="text-2xl text-primary" />
            <div>
              <h3 class="text-md font-semibold text-gray-900 dark:text-gray-100">{{ t('profile.notifications.inAppSection') }}</h3>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.inAppDescription') }}</p>
            </div>
          </div>

          <template v-if="loadingData">
            <USkeleton class="h-12 w-full rounded-lg" />
          </template>
          <template v-else>
            <div class="flex items-center justify-between">
              <label class="text-sm font-medium">{{ t('profile.notifications.inAppNotifications') }}</label>
              <USwitch v-model="form.inAppNotificationsEnabled" :disabled="isSaving" />
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400">{{ t('profile.notifications.inAppNotificationsDescription') }}</p>
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
/**
 * Notification **settings** — email, push and the in-app notification centre's channel.
 *
 * Renamed from `NotificationsDrawer` in #116, which is when the name became actively misleading:
 * this panel has only ever held preferences and test buttons, and the thing a user reasonably
 * expects behind "notifications" — a list of what was actually sent — now exists as
 * `NotificationCenterDrawer`, behind the bell in the header.
 */
import { ref, computed, watch } from 'vue'
import { useUserApi } from '~/composables/api/useUserApi'
import { usePushNotifications } from '~/composables/usePushNotifications'

interface NotificationForm {
  emailNotificationsEnabled: boolean
  emailWeeklySummaryEnabled: boolean
  pushNotificationsEnabled: boolean
  pushWeeklySummaryEnabled: boolean
  pushFamilyChatEnabled: boolean
  inAppNotificationsEnabled: boolean
}

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const { t } = useI18n()
const toast = useToast()
const eventBus = useEventBus()
const { getNotificationPreferences, updateNotificationPreferences, sendTestPushNotification, sendTestEmailSummary } = useUserApi()
const { isSupported, permissionStatus, subscribe, unsubscribe, isSubscribed } = usePushNotifications()

const emptyForm = (): NotificationForm => ({
  emailNotificationsEnabled: false,
  emailWeeklySummaryEnabled: false,
  pushNotificationsEnabled: false,
  pushWeeklySummaryEnabled: false,
  pushFamilyChatEnabled: true,
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
  if (!enabled) form.value.pushFamilyChatEnabled = false
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
          pushFamilyChatEnabled: res.data.pushFamilyChatEnabled !== false,
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
    // The app-icon badge is gated on the expiration-reminder switch (#130), so the
    // gate has to be re-read now rather than at the next app launch.
    eventBus.emit('notification-preferences:updated')
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
