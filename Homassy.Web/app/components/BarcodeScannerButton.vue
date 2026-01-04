<template>
  <UButton
    v-if="isMobile"
    icon="i-lucide-camera"
    color="primary"
    size="sm"
    :disabled="disabled"
    aria-label="Scan barcode with camera"
    @click="handleClick"
  />
</template>

<script setup lang="ts">
interface Props {
  disabled?: boolean
}

defineProps<Props>()

const { isMobile } = useDeviceDetection()
const { openScanner } = useBarcodeScanner()

const handleClick = () => {
  openScanner()
}
</script>
