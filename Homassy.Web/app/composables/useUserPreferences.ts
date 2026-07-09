/**
 * User preferences composable
 *
 * Single home for the profile preference-save logic that used to live inline in
 * `pages/profile/settings.vue`. Keeps the select-value contract identical
 * (option `value` is the select's `text`, mapped to enums via the *CodeToEnum
 * helpers on save) so the backend keeps accepting the payloads, and patches the
 * auth store optimistically so the profile header + navbar + i18n react
 * instantly. On failure it reconciles the store from the backend.
 */
import { ref, computed } from 'vue'
import { useAuthStore } from '~/stores/auth'
import { useUserApi } from '~/composables/api/useUserApi'
import { useSelectValueApi } from '~/composables/api/useSelectValueApi'
import { SelectValueType } from '~/types/enums'
import type { SelectValue } from '~/types/selectValue'
import type { UpdateUserSettingsRequest } from '~/types/user'
import { languageCodeToEnum, currencyCodeToEnum, timeZoneIdToEnum } from '~/utils/enumMappers'

export type PreferenceField = 'language' | 'currency' | 'timeZone'

export const useUserPreferences = () => {
  const authStore = useAuthStore()
  const { updateUserSettings } = useUserApi()
  const { getSelectValues } = useSelectValueApi()
  const { setLocale } = useI18n()

  const isLoadingOptions = ref(false)
  const optionsLoaded = ref(false)
  const isSaving = ref(false)

  const languageOptions = ref<SelectValue[]>([])
  const currencyOptions = ref<SelectValue[]>([])
  const timeZoneOptions = ref<SelectValue[]>([])

  // value === text keeps parity with the old settings page (the current value
  // stored on the user is compared against these to preselect the option).
  const languageSelectOptions = computed(() =>
    languageOptions.value.map(o => ({ label: o.text, value: o.text }))
  )
  const currencySelectOptions = computed(() =>
    currencyOptions.value.map(o => ({ label: o.text, value: o.text }))
  )
  const timeZoneSelectOptions = computed(() =>
    timeZoneOptions.value.map(o => ({ label: o.text, value: o.text }))
  )

  const optionsByField = {
    language: languageSelectOptions,
    currency: currencySelectOptions,
    timeZone: timeZoneSelectOptions
  } as const

  /**
   * Lazily load the language/currency/time-zone option lists. Safe to call
   * repeatedly — only fetches once unless `force` is passed.
   */
  async function loadSelectOptions(force = false) {
    if (optionsLoaded.value && !force) return
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
      optionsLoaded.value = true
    } catch (error) {
      console.error('Failed to load preference options:', error)
    } finally {
      isLoadingOptions.value = false
    }
  }

  // Build the full settings payload from the (already patched) store user, so a
  // single-field change still sends the same shape the old page sent.
  function buildPayload(): UpdateUserSettingsRequest {
    const u = authStore.user
    return {
      name: u?.name || '',
      displayName: u?.displayName || undefined,
      defaultLanguage: languageCodeToEnum(u?.language || ''),
      defaultCurrency: currencyCodeToEnum(u?.currency || ''),
      defaultTimeZone: timeZoneIdToEnum(u?.timeZone || '')
    }
  }

  async function persist(): Promise<boolean> {
    isSaving.value = true
    try {
      const res = await updateUserSettings(buildPayload())
      if (res?.success === false) {
        await authStore.fetchUserFromBackend()
        return false
      }
      return true
    } catch (error) {
      console.error('Failed to update settings:', error)
      // Reconcile the optimistic patch with the backend truth.
      await authStore.fetchUserFromBackend()
      return false
    } finally {
      isSaving.value = false
    }
  }

  /**
   * Save a single preference. Patches the store first (instant UI), then
   * persists; for language it also flips the i18n locale immediately.
   */
  async function savePreference(field: PreferenceField, value: string): Promise<boolean> {
    if (authStore.user?.[field] === value) return true
    authStore.applyUserPatch({ [field]: value })

    if (field === 'language') {
      const localeCode = authStore.syncLanguageLocale(value)
      await setLocale(localeCode as 'en' | 'hu' | 'de')
    }

    return persist()
  }

  /** Save name + display name together (mirrors the old Save-name button). */
  async function saveName(name: string, displayName: string): Promise<boolean> {
    authStore.applyUserPatch({ name, displayName })
    return persist()
  }

  return {
    isLoadingOptions,
    isSaving,
    languageSelectOptions,
    currencySelectOptions,
    timeZoneSelectOptions,
    optionsByField,
    loadSelectOptions,
    savePreference,
    saveName
  }
}
