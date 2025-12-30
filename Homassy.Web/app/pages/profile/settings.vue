
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
      <UIcon name="i-lucide-settings" class="text-xl text-primary" />
      <div>
        <h1 class="text-2xl font-semibold">{{ $t('profile.settings') }}</h1>
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
    <UForm v-else @submit.prevent="saveSettings" class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-6 mt-4 space-y-6">
      <h2 class="text-lg font-semibold mb-2">{{ $t('profile.settings') }}</h2>
      <p class="text-gray-600 dark:text-gray-400 mb-4">{{ $t('profile.settingsCardDescription') }}</p>
      <div class="space-y-4">
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1.5 sm:gap-0">
          <label class="text-sm font-medium mb-1.5 sm:mb-0">{{ $t('profile.name') }}</label>
          <UInput
            v-model="formData.name"
            :placeholder="$t('profile.name')"
            :disabled="isSubmitting"
            class="w-full sm:w-64"
          />
        </div>
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1.5 sm:gap-0">
          <label class="text-sm font-medium mb-1.5 sm:mb-0">{{ $t('profile.displayName') }}</label>
          <UInput
            v-model="formData.displayName"
            :placeholder="$t('profile.displayName')"
            :disabled="isSubmitting"
            class="w-full sm:w-64"
          />
        </div>
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1.5 sm:gap-0">
          <label class="text-sm font-medium mb-1.5 sm:mb-0">{{ $t('profile.language') }}</label>
          <USelect
            v-model="formData.language"
            :items="languageSelectOptions"
            :placeholder="$t('profile.language')"
            :disabled="isSubmitting || isLoadingOptions"
            class="w-full sm:w-64"
          />
        </div>
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1.5 sm:gap-0">
          <label class="text-sm font-medium mb-1.5 sm:mb-0">{{ $t('profile.currency') }}</label>
          <USelect
            v-model="formData.currency"
            :items="currencySelectOptions"
            :placeholder="$t('profile.currency')"
            :disabled="isSubmitting || isLoadingOptions"
            class="w-full sm:w-64"
          />
        </div>
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1.5 sm:gap-0">
          <label class="text-sm font-medium mb-1.5 sm:mb-0">{{ $t('profile.timeZone') }}</label>
          <USelect
            v-model="formData.timeZone"
            :items="timeZoneSelectOptions"
            :placeholder="$t('profile.timeZone')"
            :disabled="isSubmitting || isLoadingOptions"
            class="w-full sm:w-64"
          />
        </div>
      </div>
      <div class="space-y-3 mt-6">
        <UButton type="submit" color="primary" class="w-full" icon="i-lucide-save" :loading="isSubmitting">
          {{ $t('settings.saveChanges') }}
        </UButton>
        <NuxtLink to="/profile" class="block">
          <UButton color="neutral" variant="soft" class="w-full" icon="i-lucide-x" :disabled="isSubmitting">
            {{ $t('common.cancel') }}
          </UButton>
        </NuxtLink>
      </div>
    </UForm>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'
import { useSelectValueApi } from '~/composables/api/useSelectValueApi'
import { SelectValueType } from '~/types/enums'
import type { SelectValue } from '~/types/selectValue'
import type { UpdateUserSettingsRequest } from '~/types/user'
import { languageCodeToEnum, currencyCodeToEnum, timeZoneIdToEnum } from '~/utils/enumMappers'

const { setLocale } = useI18n()

definePageMeta({ layout: 'auth', middleware: 'auth' })

const authStore = useAuthStore()
const { updateUserSettings } = useUserApi()
const { getSelectValues } = useSelectValueApi()

const isSubmitting = ref(false)
const isLoadingOptions = ref(false)

const formData = ref({
  name: '',
  displayName: '',
  language: '',
  currency: '',
  timeZone: ''
})

const languageOptions = ref<SelectValue[]>([])
const currencyOptions = ref<SelectValue[]>([])
const timeZoneOptions = ref<SelectValue[]>([])

const languageSelectOptions = computed(() =>
  languageOptions.value.map(o => ({ label: o.text, value: o.text }))
)
const currencySelectOptions = computed(() =>
  currencyOptions.value.map(o => ({ label: o.text, value: o.text }))
)
const timeZoneSelectOptions = computed(() =>
  timeZoneOptions.value.map(o => ({ label: o.text, value: o.text }))
)

onMounted(async () => {
  // Pre-populate form with current values
  formData.value = {
    name: authStore.user?.name || '',
    displayName: authStore.user?.displayName || '',
    language: authStore.user?.language || '',
    currency: authStore.user?.currency || '',
    timeZone: authStore.user?.timeZone || ''
  }

  // Load dropdown options
  await loadSelectOptions()
})

async function loadSelectOptions() {
  isLoadingOptions.value = true
  try {
    const [langRes, currRes, tzRes] = await Promise.all([
      getSelectValues(SelectValueType.Languages),
      getSelectValues(SelectValueType.Currencies),
      getSelectValues(SelectValueType.TimeZones)
    ])

    languageOptions.value = langRes.data || []
    currencyOptions.value = currRes.data || []
    timeZoneOptions.value = tzRes.data || []
  } catch (error) {
    console.error('Failed to load options:', error)
  } finally {
    isLoadingOptions.value = false
  }
}

async function saveSettings() {
  isSubmitting.value = true
  try {
    const payload: UpdateUserSettingsRequest = {
      name: formData.value.name,
      displayName: formData.value.displayName || undefined,
      defaultLanguage: languageCodeToEnum(formData.value.language),
      defaultCurrency: currencyCodeToEnum(formData.value.currency),
      defaultTimeZone: timeZoneIdToEnum(formData.value.timeZone)
    }

    await updateUserSettings(payload)
    await authStore.fetchCurrentUser()

    // Update locale immediately for instant UI feedback
    if (authStore.user?.language) {
      await setLocale(authStore.user.language)
    }

    useRouter().push('/profile')
  } catch (error) {
    console.error('Failed to update settings:', error)
  } finally {
    isSubmitting.value = false
  }
}
</script>
