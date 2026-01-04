<template>
  <UModal :open="isOpen" @update:open="handleClose" :dismissible="false">
    <template #title>
      {{ $t('imageCropper.title') }}
    </template>

    <template #description>
      <span class="sr-only">{{ $t('imageCropper.description') }}</span>
    </template>

    <template #body>
      <div v-if="showCropper" class="space-y-4">
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
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
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
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { Cropper } from 'vue-advanced-cropper'
import { useI18n } from 'vue-i18n'

interface Props {
  isOpen: boolean
  imageSrc: string
  defaultAspectRatio?: number
}

const props = withDefaults(defineProps<Props>(), {
  defaultAspectRatio: 1 / 1
})

const emit = defineEmits<{
  close: []
  cropped: [base64: string]
}>()

const { t } = useI18n()

// Refs
const cropperRef = ref()
const selectedRatio = ref<number>(props.defaultAspectRatio)
const isProcessing = ref(false)
const showCropper = ref(false)

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

// Reset aspect ratio when modal opens
watch(() => props.isOpen, async (isOpen: boolean) => {
  if (isOpen) {
    selectedRatio.value = props.defaultAspectRatio
    showCropper.value = false

    console.log('Modal opened with image:', props.imageSrc?.substring(0, 50))

    // Wait for modal to fully render, then show cropper
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
