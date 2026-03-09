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
      {{ activeDescription }}
    </template>

    <template #body>
      <div class="space-y-4">
        <!-- Mode Tabs -->
        <UTabs
          :items="scanTabs"
          :default-value="'barcode'"
          :model-value="scanMode"
          @update:model-value="handleTabChange"
        />

        <!-- Camera Preview -->
        <div class="relative cursor-pointer" @click="handleCameraClick">
          <QrcodeStream
            v-if="isScannerOpen"
            :key="scanMode"
            :paused="isPaused"
            :constraints="{ facingMode: 'environment' }"
            :formats="activeFormats"
            :track="trackFunction"
            @detect="handleDetect"
            @camera-on="handleCameraOn"
            @error="handleCameraError"
          >
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

            <!-- Multi-detection candidate selection overlay -->
            <div
              v-if="detectedCandidates.length > 1"
              class="absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/70 p-4"
            >
              <p class="text-sm font-semibold text-white text-center">
                {{ t('barcodeScanner.multipleDetected') }}
              </p>
              <UButton
                v-for="candidate in detectedCandidates"
                :key="candidate"
                color="primary"
                variant="solid"
                class="w-full truncate"
                @click.stop="selectCandidate(candidate)"
              >
                {{ candidate }}
              </UButton>
              <UButton
                color="neutral"
                variant="outline"
                class="w-full mt-1"
                @click.stop="handleDismissCandidates"
              >
                {{ t('barcodeScanner.retry') }}
              </UButton>
            </div>

            <!-- Scanning Animation Overlay (Primary Color) -->
            <div
              v-if="isScanning && !isPaused && !frozenImage"
              class="absolute inset-0 pointer-events-none flex items-center justify-center overflow-hidden"
            >
              <div class="scanning-line" />
            </div>

            <!-- Capture Flash Animation -->
            <div
              v-if="isCapturing"
              class="absolute inset-0 pointer-events-none bg-white capture-flash"
            />
          </QrcodeStream>
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
import { watch, nextTick, ref, computed } from 'vue'
import { QrcodeStream } from 'vue-qrcode-reader'
import { useBarcodeScanner } from '../composables/useBarcodeScanner'

interface Props {
  onBarcodeDetected: (barcode: string) => void
}

const props = defineProps<Props>()

const { t } = useI18n()
const {
  isScannerOpen,
  isScanning,
  isPaused,
  scanError,
  startScanner,
  closeScanner,
  captureAndScan,
  handleDetect,
  handleCameraError,
  detectedCandidates,
  selectCandidate,
  dismissCandidates
} = useBarcodeScanner()

const isCapturing = ref(false)
const frozenImage = ref<string | null>(null)
const videoRef = ref<HTMLVideoElement | null>(null)
const scanMode = ref<'barcode' | 'qrcode'>('barcode')

const scanTabs = computed(() => [
  { value: 'barcode', label: t('barcodeScanner.tabs.barcode') },
  { value: 'qrcode', label: t('barcodeScanner.tabs.qrcode') }
])

const activeFormats = computed(() =>
  scanMode.value === 'barcode' ? ['linear_codes'] : ['matrix_codes']
)

const activeDescription = computed(() =>
  scanMode.value === 'barcode'
    ? t('barcodeScanner.description')
    : t('barcodeScanner.descriptionQr')
)

const handleTabChange = (value: string) => {
  if (value === scanMode.value) return
  scanMode.value = value as 'barcode' | 'qrcode'
  // Clear any frozen state when switching tabs
  frozenImage.value = null
  dismissCandidates()
}

// Watch for modal open and start scanner
watch(isScannerOpen, async (isOpen: boolean) => {
  if (isOpen) {
    // Clear frozen image when opening scanner
    frozenImage.value = null
    await nextTick()
    startScanner(props.onBarcodeDetected)
  }
})

const handleUpdateOpen = async (value: boolean) => {
  if (!value) {
    // Modal is being closed
    await closeScanner()
    frozenImage.value = null
    videoRef.value = null
    scanMode.value = 'barcode'
  }
}

const handleCameraOn = () => {
  // Camera successfully started
  scanError.value = null
}

const handleDismissCandidates = () => {
  frozenImage.value = null
  dismissCandidates()
}

const trackFunction = (detectedCodes: any[], ctx: CanvasRenderingContext2D) => {
  if (detectedCodes.length === 0) return

  detectedCodes.forEach((code) => {
    const { boundingBox, cornerPoints } = code

    // Draw green box around detected code
    ctx.strokeStyle = 'rgb(var(--color-primary-500))'
    ctx.lineWidth = 3

    if (boundingBox) {
      ctx.strokeRect(
        boundingBox.x,
        boundingBox.y,
        boundingBox.width,
        boundingBox.height
      )
    }

    // Draw corner points
    if (cornerPoints && cornerPoints.length === 4) {
      ctx.fillStyle = 'rgb(var(--color-primary-500))'
      cornerPoints.forEach((point: any) => {
        ctx.beginPath()
        ctx.arc(point.x, point.y, 5, 0, 2 * Math.PI)
        ctx.fill()
      })
    }
  })
}

const handleCameraClick = () => {
  if (!isScanning.value || isPaused.value) return

  // Trigger capture animation
  isCapturing.value = true
  setTimeout(() => {
    isCapturing.value = false
  }, 300)

  // Get video element from QrcodeStream
  const video = document.querySelector('.qrcode-stream-camera') as HTMLVideoElement
  if (!video) return

  videoRef.value = video

  captureAndScan(
    videoRef,
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
