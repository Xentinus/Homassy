<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header with back button -->
    <div class="flex items-center gap-3">
      <UButton
        icon="i-lucide-arrow-left"
        color="neutral"
        variant="ghost"
        @click="navigateTo('/profile')"
      />
      <div>
        <h1 class="text-2xl font-semibold">{{ $t('profile.settings') }}</h1>
      </div>
    </div>

    <!-- Settings Form -->
    <div class="space-y-4">
      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.name') }}</label>
        <UInput
          v-model="formData.name"
          :placeholder="$t('profile.name')"
          :disabled="isSubmitting"
        />
      </div>

      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.displayName') }}</label>
        <UInput
          v-model="formData.displayName"
          :placeholder="$t('profile.displayName')"
          :disabled="isSubmitting"
        />
      </div>

      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.language') }}</label>
        <USelect
          v-model="formData.language"
          :items="languageSelectOptions"
          :placeholder="$t('profile.language')"
          :disabled="isSubmitting || isLoadingOptions"
        />
      </div>

      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.currency') }}</label>
        <USelect
          v-model="formData.currency"
          :items="currencySelectOptions"
          :placeholder="$t('profile.currency')"
          :disabled="isSubmitting || isLoadingOptions"
        />
      </div>

      <div>
        <label class="block text-sm font-medium mb-1.5">{{ $t('profile.timeZone') }}</label>
        <USelect
          v-model="formData.timeZone"
          :items="timeZoneSelectOptions"
          :placeholder="$t('profile.timeZone')"
          :disabled="isSubmitting || isLoadingOptions"
        />
      </div>
    </div>

    <!-- Action Buttons -->
    <div class="flex gap-3">
      <UButton
        color="neutral"
        variant="soft"
        class="flex-1"
        :disabled="isSubmitting"
        @click="navigateTo('/profile')"
      >
        {{ $t('common.cancel') }}
      </UButton>
      <UButton
        color="primary"
        class="flex-1"
        :loading="isSubmitting"
        @click="saveSettings"
      >
        {{ $t('settings.saveChanges') }}
      </UButton>
    </div>
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

    navigateTo('/profile')
  } catch (error) {
    console.error('Failed to update settings:', error)
  } finally {
    isSubmitting.value = false
  }
}
</script>
