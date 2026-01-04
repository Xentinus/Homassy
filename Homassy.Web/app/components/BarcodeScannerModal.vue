<template>
  <UModal
    :open="isScannerOpen"
    @update:open="handleClose"
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
        <div class="relative">
          <div
            id="barcode-scanner-reader"
            class="w-full rounded-lg overflow-hidden"
          />

          <!-- Scanning Animation Overlay (Primary Color) -->
          <div
            v-if="isScanning"
            class="absolute inset-0 pointer-events-none flex items-center justify-center overflow-hidden"
          >
            <div class="scanning-line" />
          </div>
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

    <template #footer>
      <div class="flex justify-end">
        <UButton
          :label="t('common.cancel')"
          color="neutral"
          variant="outline"
          @click="handleClose"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { watch, nextTick } from 'vue'
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
  closeScanner
} = useBarcodeScanner()

// Watch for modal open and start scanner
watch(isScannerOpen, async (isOpen) => {
  if (isOpen) {
    await nextTick()
    await startScanner('barcode-scanner-reader', props.onBarcodeDetected)
  }
})

const handleClose = async () => {
  await closeScanner()
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
</style>
