<template>
  <AppDrawer
    :open="open"
    :title="title"
    :icon="isEdit ? 'i-lucide-pencil' : 'i-lucide-plus'"
    :loading="saving"
    @update:open="(v) => emit('update:open', v)"
  >
    <p class="text-sm text-muted mb-4">{{ t(`pages.shoppingLists.${keyPrefix}.description`) }}</p>

    <UForm ref="formRef" :schema="schema" :state="form" class="space-y-4" @submit="onSubmit">
      <UFormField :label="t(`pages.shoppingLists.${keyPrefix}.nameLabel`)" name="name" required>
        <UInput
          v-model="form.name"
          :placeholder="t(`pages.shoppingLists.${keyPrefix}.namePlaceholder`)"
          :disabled="saving"
          class="w-full"
        />
      </UFormField>

      <UFormField :label="t(`pages.shoppingLists.${keyPrefix}.descriptionLabel`)" name="description">
        <UTextarea
          v-model="form.description"
          :placeholder="t(`pages.shoppingLists.${keyPrefix}.descriptionPlaceholder`)"
          :disabled="saving"
          :rows="3"
          class="w-full"
        />
      </UFormField>

      <UFormField :label="t(`pages.shoppingLists.${keyPrefix}.colorLabel`)" name="color">
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
        <UCheckbox
          v-model="form.isSharedWithFamily"
          :label="t(`pages.shoppingLists.${keyPrefix}.isSharedWithFamilyLabel`)"
          :disabled="saving"
        />
      </UFormField>
    </UForm>

    <template #footer>
      <UButton :label="t(`pages.shoppingLists.${keyPrefix}.cancel`)" color="neutral" variant="ghost" @click="emit('update:open', false)" />
      <UButton
        :label="t(`pages.shoppingLists.${keyPrefix}.confirm`)"
        color="primary"
        icon="i-lucide-save"
        :loading="saving"
        @click="formRef?.submit()"
      />
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { z } from 'zod'
import type { FormSubmitEvent } from '@nuxt/ui'
import { useShoppingListApi } from '~/composables/api/useShoppingListApi'
import type { ShoppingListInfo } from '~/types/shoppingList'

/**
 * Create/edit a shopping list in a bottom-sheet drawer (UForm + Zod + UColorPicker).
 * Owns the API call and emits `saved` with the resulting DTO so the parent can patch instantly.
 * Shared by /shopping-lists and /profile/shopping-lists, so the copy stays under `pages.shoppingLists.*`.
 */
const props = withDefaults(defineProps<{
  open: boolean
  list?: ShoppingListInfo | null
}>(), {
  list: null
})

const emit = defineEmits<{
  'update:open': [value: boolean]
  saved: [list: ShoppingListInfo]
}>()

const { t } = useI18n()
const toast = useToast()
const { createShoppingList, updateShoppingList } = useShoppingListApi()

const isEdit = computed(() => !!props.list)
// The two modals have identical field labels but distinct titles/buttons; keep both key sets.
const keyPrefix = computed(() => isEdit.value ? 'editModal' : 'createModal')
const title = computed(() => t(`pages.shoppingLists.${keyPrefix.value}.title`))

const schema = z.object({
  name: z.string({ required_error: t('pages.shoppingLists.nameRequired') })
    .min(2, t('pages.shoppingLists.nameRequired'))
    .max(128),
  description: z.string().max(500).optional(),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/i).optional().or(z.literal('')),
  isSharedWithFamily: z.boolean().optional().default(false)
})
type Schema = z.output<typeof schema>

const emptyForm = () => ({
  name: '',
  description: '',
  color: '',
  isSharedWithFamily: false
})

const form = ref(emptyForm())
const saving = ref(false)
const formRef = ref()

function clearColor() {
  form.value.color = ''
}

// Seed the form each time the drawer opens (create = blank, edit = the list).
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  if (props.list) {
    form.value = {
      name: props.list.name,
      description: props.list.description || '',
      color: props.list.color || '',
      isSharedWithFamily: props.list.isSharedWithFamily
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
      // '' clears the colour on edit; omitted entirely on create.
      color: data.color || (isEdit.value ? '' : undefined),
      isSharedWithFamily: data.isSharedWithFamily
    }

    const res = props.list
      ? await updateShoppingList(props.list.publicId, payload)
      : await createShoppingList(payload)

    if (res.success && res.data) {
      emit('saved', res.data)
      emit('update:open', false)
    } else {
      toast.add({ title: t('common.error'), description: t('pages.shoppingLists.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
    }
  } catch (error) {
    console.error('Failed to save shopping list:', error)
    toast.add({ title: t('common.error'), description: t('pages.shoppingLists.saveFailed'), color: 'error', icon: 'i-lucide-alert-circle' })
  } finally {
    saving.value = false
  }
}
</script>
