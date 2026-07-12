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
        <UIcon name="i-lucide-warehouse" class="h-7 w-7 shrink-0 text-primary-500" />
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

        <UFormField name="isFreezer">
          <UCheckbox v-model="form.isFreezer" :label="t('profile.storageLocations.isFreezer')" :disabled="saving" />
        </UFormField>

        <UFormField name="isSharedWithFamily">
          <UCheckbox v-model="form.isSharedWithFamily" :label="t('profile.storageLocations.isSharedWithFamily')" :disabled="saving" />
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
import type { StorageLocationInfo } from '~/types/location'

/**
 * Create/edit a storage location in a modern bottom-sheet drawer (UForm + Zod + UColorPicker).
 * Owns the API call and emits `saved` with the resulting DTO so the parent can patch instantly;
 * the realtime socket delivers the same change to other family members.
 */
const props = withDefaults(defineProps<{
  open: boolean
  location?: StorageLocationInfo | null
}>(), {
  location: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [location: StorageLocationInfo]
}>()

const { t } = useI18n()
const toast = useToast()
const { createStorageLocation, updateStorageLocation } = useLocationsApi()

const isEdit = computed(() => !!props.location)
const title = computed(() => isEdit.value
  ? t('profile.storageLocations.editLocation')
  : t('profile.storageLocations.createLocation'))

const schema = z.object({
  name: z.string({ required_error: t('profile.storageLocations.nameRequired') })
    .min(2, t('profile.storageLocations.nameRequired'))
    .max(128),
  description: z.string().max(500).optional(),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/i).optional().or(z.literal('')),
  isFreezer: z.boolean().optional().default(false),
  isSharedWithFamily: z.boolean().optional().default(false)
})
type Schema = z.output<typeof schema>

const emptyForm = () => ({
  name: '',
  description: '',
  color: '',
  isFreezer: false,
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

// Seed the form each time the drawer opens (create = blank, edit = the entity).
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  if (props.location) {
    form.value = {
      name: props.location.name,
      description: props.location.description || '',
      color: props.location.color || '',
      isFreezer: props.location.isFreezer,
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
    const payload = {
      name: data.name.trim(),
      description: data.description?.trim() || undefined,
      color: data.color || (isEdit.value ? '' : undefined),
      isFreezer: data.isFreezer,
      isSharedWithFamily: data.isSharedWithFamily
    }

    const res = props.location
      ? await updateStorageLocation(props.location.publicId, payload)
      : await createStorageLocation(payload)

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
    } else {
      toast.add({ title: t('common.error'), description: t('profile.storageLocations.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
    }
  } catch (error) {
    console.error('Failed to save storage location:', error)
    toast.add({ title: t('common.error'), description: t('profile.storageLocations.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    saving.value = false
  }
}
</script>
