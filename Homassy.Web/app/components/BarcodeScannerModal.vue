<template>
  <UModal
    :open="isScannerOpen"
    @update:open="handleUpdateOpen"
    :dismissible="false"
    :ui="{
      container: 'items-center justify-center',
      width: 'max-w-full sm:max-w-lg',
      height: 'h-full sm:h-auto'
    }"
  >
    <template #title>
      {{ t('barcodeScanner.title') }}
    </template>

    <template #description>
      {{ t('barcodeScanner.description') }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Camera Preview -->
        <div class="relative cursor-pointer" @click="handleCameraClick">
          <div
            id="barcode-scanner-reader"
            class="w-full rounded-lg overflow-hidden"
          />

          <!-- Frozen Image Overlay -->
          <div
            v-if="frozenImage"
            class="absolute inset-0 pointer-events-none"
          >
            <img
              :src="frozenImage"
              alt="Captured frame"
              class="w-full h-full object-cover"
            />
          </div>

          <!-- Scanning Animation Overlay (Primary Color) -->
          <div
            v-if="isScanning && !frozenImage"
            class="absolute inset-0 pointer-events-none flex items-center justify-center overflow-hidden"
          >
            <div class="scanning-line" />
          </div>

          <!-- Capture Flash Animation -->
          <div
            v-if="isCapturing"
            class="absolute inset-0 pointer-events-none bg-white capture-flash"
          />
        </div>

        <!-- Instructions -->
        <p class="text-sm text-center text-gray-600 dark:text-gray-400">
          {{ t('barcodeScanner.instructions') }}
        </p>

        <!-- Error Message -->
        <div
          v-if="scanError"
          class="p-3 bg-red-50 dark:bg-red-900/20 rounded-lg"
        >
          <p class="text-sm text-red-600 dark:text-red-400">
            {{ scanError }}
          </p>
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { watch, nextTick, ref } from 'vue'
import { useBarcodeScanner } from '~/composables/useBarcodeScanner'

interface Props {
  onBarcodeDetected: (barcode: string) => void
}

const props = defineProps<Props>()

const { t } = useI18n()
const {
  isScannerOpen,
  isScanning,
  scanError,
  startScanner,
  closeScanner,
  captureAndScan
} = useBarcodeScanner()

const isCapturing = ref(false)
const frozenImage = ref<string | null>(null)

// Watch for modal open and start scanner
watch(isScannerOpen, async (isOpen) => {
  if (isOpen) {
    // Clear frozen image when opening scanner
    frozenImage.value = null
    await nextTick()
    await startScanner('barcode-scanner-reader', props.onBarcodeDetected)
  }
})

const handleUpdateOpen = async (value: boolean) => {
  if (!value) {
    // Modal is being closed
    await closeScanner()
    frozenImage.value = null
  }
}

const handleCameraClick = () => {
  // Trigger capture animation
  isCapturing.value = true
  setTimeout(() => {
    isCapturing.value = false
  }, 300)

  captureAndScan(
    props.onBarcodeDetected,
    (imageUrl) => {
      // On freeze - show the captured image
      frozenImage.value = imageUrl
    },
    () => {
      // On unfreeze - resume live video
      frozenImage.value = null
    }
  )
}
</script>

<style scoped>
/* Animated scanning line in primary color (green) */
.scanning-line {
  position: absolute;
  width: 100%;
  height: 2px;
  background: linear-gradient(
    90deg,
    transparent,
    rgb(var(--color-primary-500)),
    transparent
  );
  animation: scan 2s linear infinite;
}

@keyframes scan {
  0% {
    transform: translateY(-100px);
  }
  100% {
    transform: translateY(100px);
  }
}

/* Camera capture flash animation */
.capture-flash {
  animation: flash 0.3s ease-out;
}

@keyframes flash {
  0% {
    opacity: 0;
  }
  50% {
    opacity: 0.8;
  }
  100% {
    opacity: 0;
  }
}
</style>
