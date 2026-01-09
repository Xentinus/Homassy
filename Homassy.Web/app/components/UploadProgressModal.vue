<template>
  <UModal :open="isOpen" :ui="{ width: 'sm:max-w-md' }" @update:open="$emit('update:isOpen', $event)" :dismissible="false">
    <template #title>
      {{ $t('imageUpload.progress.title') }}
    </template>

    <template #description>
      {{ currentStageText }}
    </template>

    <template #body>
      <div class="space-y-4">

        <!-- Progress Bar -->
        <div>
          <UProgress 
            :value="progress" 
            :color="progressColor"
            size="md"
          />
          <p class="mt-2 text-center text-sm text-gray-600 dark:text-gray-400">
            {{ progress }}%
          </p>
        </div>

        <!-- Current Stage -->
        <div class="text-center">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
            {{ currentStageText }}
          </p>
        </div>

        <!-- Error Message -->
        <div v-if="status === 'failed' && errorMessage" class="rounded-md bg-red-50 dark:bg-red-900/20 p-3">
          <p class="text-sm text-red-800 dark:text-red-200">
            {{ errorMessage }}
          </p>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton
          v-if="status === 'inprogress'"
          :label="$t('imageUpload.progress.cancelButton')"
          color="neutral"
          variant="outline"
          @click="handleCancel"
        />
        <UButton
          v-if="status === 'failed' || status === 'cancelled'"
          :label="$t('common.confirm')"
          color="primary"
          @click="handleClose"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'

const props = withDefaults(defineProps<{
  isOpen?: boolean
  progress?: number
  stage?: string
  status?: 'inprogress' | 'completed' | 'failed' | 'cancelled'
  errorMessage?: string
}>(), {
  isOpen: false,
  progress: 0,
  stage: 'validating',
  status: 'inprogress',
  errorMessage: undefined
})

const emit = defineEmits<{
  'update:isOpen': [value: boolean]
  'cancel': []
  'close': []
}>()

const { t } = useI18n()

const progressColor = computed(() => {
  switch (props.status) {
    case 'completed':
      return 'success'
    case 'failed':
      return 'error'
    case 'cancelled':
      return 'neutral'
    default:
      return 'primary'
  }
})

const currentStageText = computed(() => {
  if (props.status === 'completed') {
    return t('imageUpload.progress.completed')
  }
  if (props.status === 'failed') {
    return t('imageUpload.progress.failed')
  }
  if (props.status === 'cancelled') {
    return t('imageUpload.progress.cancelled')
  }

  // Map stage to translation key
  const stageKey = props.stage.toLowerCase()
  return t(`imageUpload.progress.${stageKey}`)
})

const handleCancel = () => {
  emit('cancel')
}

const handleClose = () => {
  emit('close')
  emit('update:isOpen', false)
}
</script>
