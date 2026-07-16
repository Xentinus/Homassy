<template>
  <UDrawer
    :open="open"
    :dismissible="false"
    :ui="{
      content: 'max-h-[90dvh] rounded-t-2xl overflow-hidden',
      container: 'flex flex-col min-h-0 gap-0 p-0 overflow-hidden',
      header: 'shrink-0 border-b border-default p-4 sm:px-6',
      body: 'min-h-0 overflow-y-auto p-4 sm:p-6',
      footer: 'shrink-0 flex flex-row items-center justify-end gap-2 border-t border-default p-4 sm:px-6'
    }"
    @update:open="(v) => emit('update:open', v)"
  >
    <template #header>
      <div ref="headerEl" class="flex items-center gap-3 w-full" style="touch-action: none">
        <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 shrink-0 text-primary-500" />
        <DrawerTitle class="text-xl sm:text-2xl font-semibold">{{ title }}</DrawerTitle>
        <DrawerDescription class="sr-only">{{ title }}</DrawerDescription>
        <UButton
          class="ml-auto"
          icon="i-lucide-x"
          color="neutral"
          variant="ghost"
          :aria-label="t('common.close')"
          @click="emit('update:open', false)"
        />
      </div>
    </template>

    <template #body>
      <UForm ref="formRef" :schema="schema" :state="form" class="space-y-4" @submit="onSubmit">
        <UFormField :label="t('common.name')" name="name" required>
          <UInput v-model="form.name" :placeholder="t('common.name')" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('common.description')" name="description">
          <UTextarea v-model="form.description" :placeholder="t('common.description')" :disabled="saving" :rows="3" class="w-full" />
        </UFormField>

        <UFormField :label="t('common.address')" name="address">
          <UInput v-model="form.address" :placeholder="t('common.address')" :disabled="saving" class="w-full" />
        </UFormField>

        <div class="grid grid-cols-2 gap-4">
          <UFormField :label="t('common.city')" name="city">
            <UInput v-model="form.city" :placeholder="t('common.city')" :disabled="saving" class="w-full" />
          </UFormField>

          <UFormField :label="t('common.postalCode')" name="postalCode">
            <UInput v-model="form.postalCode" :placeholder="t('common.postalCode')" :disabled="saving" class="w-full" />
          </UFormField>
        </div>

        <UFormField :label="t('common.country')" name="country">
          <UInput v-model="form.country" :placeholder="t('common.country')" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('common.website')" name="website">
          <UInput v-model="form.website" type="url" :placeholder="t('common.website')" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('profile.shoppingLocations.googleMaps')" name="googleMaps">
          <UInput v-model="form.googleMaps" type="url" :placeholder="t('profile.shoppingLocations.googleMaps')" :disabled="saving" class="w-full" />
        </UFormField>

        <UFormField :label="t('common.color')" name="color">
          <div class="flex items-center gap-3">
            <UPopover>
              <UButton color="neutral" variant="outline" :disabled="saving">
                <div class="flex items-center gap-2">
                  <div v-if="form.color" class="w-4 h-4 rounded" :style="{ backgroundColor: form.color }" />
                  <span>{{ form.color || t('common.chooseColor') }}</span>
                </div>
              </UButton>
              <template #content>
                <UColorPicker v-model="form.color" class="p-2" />
              </template>
            </UPopover>
            <UButton v-if="form.color" icon="i-lucide-x" color="neutral" variant="ghost" size="sm" :disabled="saving" @click="clearColor" />
          </div>
        </UFormField>

        <UFormField name="isSharedWithFamily">
          <UCheckbox v-model="form.isSharedWithFamily" :label="t('profile.shoppingLocations.isSharedWithFamily')" :disabled="saving" />
        </UFormField>
      </UForm>
    </template>

    <template #footer>
      <UButton :label="t('common.cancel')" color="neutral" variant="ghost" @click="emit('update:open', false)" />
      <UButton
        :label="t('common.save')"
        color="primary"
        icon="i-lucide-save"
        :loading="saving"
        @click="formRef?.submit()"
      />
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { z } from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import { DrawerTitle, DrawerDescription } from 'vaul-vue'
import { useLocationsApi } from '~/composables/api/useLocationsApi'
import type { ShoppingLocationInfo, ShoppingLocationRequest } from '~/types/location'

/**
 * Create/edit a shopping location in a modern bottom-sheet drawer (UForm + Zod + UColorPicker).
 * Owns the API call and emits `saved` with the resulting DTO for an instant local patch; the realtime
 * socket delivers the same change to other family members.
 */
const props = withDefaults(defineProps<{
  open: boolean
  location?: ShoppingLocationInfo | null
}>(), {
  location: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [location: ShoppingLocationInfo]
}>()

const { t } = useI18n()
const toast = useToast()
const { createShoppingLocation, updateShoppingLocation } = useLocationsApi()
const { geocode, buildAddressQuery } = useGeocoding()

const isEdit = computed(() => !!props.location)
const title = computed(() => isEdit.value
  ? t('profile.shoppingLocations.editLocation')
  : t('profile.shoppingLocations.createLocation'))

const schema = z.object({
  name: z.string({ required_error: t('profile.shoppingLocations.nameRequired') })
    .min(2, t('profile.shoppingLocations.nameRequired'))
    .max(128),
  description: z.string().max(500).optional(),
  address: z.string().max(128).optional(),
  city: z.string().max(64).optional(),
  postalCode: z.string().max(20).optional(),
  country: z.string().max(64).optional(),
  website: z.string().url(t('profile.shoppingLocations.invalidWebsite')).max(255).optional().or(z.literal('')),
  googleMaps: z.string().url(t('profile.shoppingLocations.invalidGoogleMaps')).max(255).optional().or(z.literal('')),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/i).optional().or(z.literal('')),
  isSharedWithFamily: z.boolean().optional().default(false)
})
type Schema = z.output<typeof schema>

const emptyForm = () => ({
  name: '',
  description: '',
  address: '',
  city: '',
  postalCode: '',
  country: '',
  website: '',
  googleMaps: '',
  color: '',
  isSharedWithFamily: false
})

const form = ref(emptyForm())
const saving = ref(false)
const formRef = ref()
const headerEl = ref<HTMLElement | null>(null)

useDrawerDragToClose(headerEl, {
  onClose: () => emit('update:open', false),
  disabled: () => saving.value
})

function clearColor() {
  form.value.color = ''
}

watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  if (props.location) {
    form.value = {
      name: props.location.name,
      description: props.location.description || '',
      address: props.location.address || '',
      city: props.location.city || '',
      postalCode: props.location.postalCode || '',
      country: props.location.country || '',
      website: props.location.website || '',
      googleMaps: props.location.googleMaps || '',
      color: props.location.color || '',
      isSharedWithFamily: props.location.isSharedWithFamily
    }
  } else {
    form.value = emptyForm()
  }
})

async function onSubmit(event: FormSubmitEvent<Schema>) {
  const data = event.data
  saving.value = true
  try {
    const payload: ShoppingLocationRequest = {
      name: data.name.trim(),
      description: data.description?.trim() || undefined,
      address: data.address?.trim() || undefined,
      city: data.city?.trim() || undefined,
      postalCode: data.postalCode?.trim() || undefined,
      country: data.country?.trim() || undefined,
      website: data.website?.trim() || undefined,
      googleMaps: data.googleMaps?.trim() || undefined,
      color: data.color || (isEdit.value ? '' : undefined),
      isSharedWithFamily: data.isSharedWithFamily
    }

    // Geocode the address once on save so proximity features have coordinates. Only when
    // there is an address and it changed (or this is a new location); a failed geocode is
    // non-fatal and simply leaves the coordinates unset.
    const newQuery = buildAddressQuery([payload.address, payload.postalCode, payload.city, payload.country])
    const oldQuery = buildAddressQuery([props.location?.address, props.location?.postalCode, props.location?.city, props.location?.country])
    if (newQuery && (!props.location || newQuery !== oldQuery)) {
      const coords = await geocode(newQuery)
      if (coords) {
        payload.latitude = coords.lat
        payload.longitude = coords.lon
      }
    }

    const res = props.location
      ? await updateShoppingLocation(props.location.publicId, payload)
      : await createShoppingLocation(payload)

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
    } else {
      toast.add({ title: t('common.error'), description: t('profile.shoppingLocations.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
    }
  } catch (error) {
    console.error('Failed to save shopping location:', error)
    toast.add({ title: t('common.error'), description: t('profile.shoppingLocations.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    saving.value = false
  }
}
</script>
