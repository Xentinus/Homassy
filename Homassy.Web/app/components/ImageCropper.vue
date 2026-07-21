<template>
  <!-- Single drawer for the whole crop→upload lifecycle: the cropper shows while
       resizing, then the SAME drawer switches to the upload progress once the
       parent starts uploading (uploadStatus leaves 'idle'). `elevated` keeps it
       above the drawer it opens from (nested vaul drawers stack by declaration
       order, not open order). -->
  <AppDrawer
    :open="isOpen"
    :title="isUploading ? $t('imageUpload.progress.title') : $t('imageCropper.title')"
    :description="isUploading ? currentStageText : $t('imageCropper.description')"
    :icon="isUploading ? 'i-lucide-upload' : 'i-lucide-crop'"
    elevated
    :closable="!(uploadStatus === 'inprogress')"
    @update:open="onUpdateOpen"
  >
    <!-- Crop phase -->
    <div v-if="!isUploading" class="space-y-4">
      <!-- Aspect ratio selector buttons -->
      <div class="flex flex-wrap gap-2">
        <UButton
          v-for="(ratio, index) in aspectRatios"
          :key="index"
          :variant="selectedRatio === ratio.value ? 'solid' : 'outline'"
          size="sm"
          @click="changeAspectRatio(ratio.value)"
        >
          {{ ratio.label }}
        </UButton>
      </div>

      <!-- Cropper instance -->
      <div class="cropper-wrapper" style="height: 400px; width: 100%;">
        <Cropper
          v-if="showCropper"
          ref="cropperRef"
          :src="imageSrc"
          :stencil-props="{
            aspectRatio: currentAspectRatio
          }"
          class="cropper"
        />
      </div>

      <!-- Instructions -->
      <div class="text-sm text-gray-600 dark:text-gray-400 text-center">
        {{ $t('imageCropper.instructions') }}
      </div>
    </div>

    <!-- Upload phase (same drawer) -->
    <div v-else class="space-y-4">
      <div>
        <UProgress :value="uploadProgress" :color="progressColor" size="md" />
        <p class="mt-2 text-center text-sm text-gray-600 dark:text-gray-400">
          {{ uploadProgress }}%
        </p>
      </div>
      <div class="text-center">
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
          {{ currentStageText }}
        </p>
      </div>
      <div v-if="uploadStatus === 'failed' && uploadErrorMessage" class="rounded-md bg-red-50 dark:bg-red-900/20 p-3">
        <p class="text-sm text-red-800 dark:text-red-200">
          {{ uploadErrorMessage }}
        </p>
      </div>
    </div>

    <template #footer>
      <!-- Crop phase -->
      <template v-if="!isUploading">
        <UButton
          color="neutral"
          variant="outline"
          @click="handleClose"
        >
          {{ $t('common.cancel') }}
        </UButton>
        <UButton
          color="primary"
          :loading="isProcessing"
          @click="handleCrop"
        >
          {{ $t('common.apply') }}
        </UButton>
      </template>
      <!-- Upload phase -->
      <template v-else>
        <UButton
          v-if="uploadStatus === 'inprogress'"
          :label="$t('imageUpload.progress.cancelButton')"
          color="neutral"
          variant="outline"
          @click="emit('cancel-upload')"
        />
        <UButton
          v-if="uploadStatus === 'failed' || uploadStatus === 'cancelled'"
          :label="$t('common.confirm')"
          color="primary"
          @click="emit('close-upload')"
        />
      </template>
    </template>
  </AppDrawer>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { Cropper } from 'vue-advanced-cropper'
import { useI18n } from 'vue-i18n'

interface Props {
  isOpen: boolean
  imageSrc: string
  defaultAspectRatio?: number
  /** Upload lifecycle — 'idle' shows the cropper; anything else switches this drawer to the progress view. */
  uploadStatus?: 'idle' | 'inprogress' | 'completed' | 'failed' | 'cancelled'
  uploadProgress?: number
  uploadStage?: string
  uploadErrorMessage?: string
}

const props = withDefaults(defineProps<Props>(), {
  defaultAspectRatio: 1 / 1,
  uploadStatus: 'idle',
  uploadProgress: 0,
  uploadStage: 'validating',
  uploadErrorMessage: undefined
})

const emit = defineEmits<{
  close: []
  cropped: [base64: string]
  'cancel-upload': []
  'close-upload': []
}>()

const { t } = useI18n()

// Refs
const cropperRef = ref()
const selectedRatio = ref<number>(props.defaultAspectRatio)
const isProcessing = ref(false)
const showCropper = ref(false)

const isUploading = computed(() => props.uploadStatus !== 'idle')

const progressColor = computed(() => {
  switch (props.uploadStatus) {
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
  if (props.uploadStatus === 'completed') return t('imageUpload.progress.completed')
  if (props.uploadStatus === 'failed') return t('imageUpload.progress.failed')
  if (props.uploadStatus === 'cancelled') return t('imageUpload.progress.cancelled')
  return t(`imageUpload.progress.${props.uploadStage.toLowerCase()}`)
})

// Aspect ratios
const aspectRatios = computed(() => [
  { label: t('imageCropper.aspectRatio.square'), value: 1 / 1 },
  { label: t('imageCropper.aspectRatio.portrait'), value: 4 / 3 },
  { label: t('imageCropper.aspectRatio.landscape'), value: 16 / 9 },
  { label: t('imageCropper.aspectRatio.free'), value: 0 } // 0 means free
])

const currentAspectRatio = computed(() => selectedRatio.value)

// Methods
const changeAspectRatio = (ratio: number) => {
  selectedRatio.value = ratio
}

const handleClose = () => {
  emit('close')
}

const onUpdateOpen = (value: boolean) => {
  if (value) return
  // ✕ / drag dismiss — route to the right handler for the active phase.
  if (isUploading.value) emit('close-upload')
  else emit('close')
}

const handleCrop = async () => {
  isProcessing.value = true
  try {
    if (!cropperRef.value) return

    const { canvas } = cropperRef.value.getResult()
    if (!canvas) return

    // Convert to base64 with good quality
    const base64 = canvas.toDataURL('image/jpeg', 0.9)
    emit('cropped', base64)
  } finally {
    isProcessing.value = false
  }
}

// Reset aspect ratio when the drawer opens, then reveal the cropper.
watch(() => props.isOpen, async (isOpen: boolean) => {
  if (isOpen) {
    selectedRatio.value = props.defaultAspectRatio
    showCropper.value = false

    await nextTick()
    setTimeout(() => {
      showCropper.value = true
    }, 100)
  } else {
    showCropper.value = false
  }
})
</script>

<style scoped>
.cropper {
  height: 100%;
  width: 100%;
  background: #DDD;
}
</style>
