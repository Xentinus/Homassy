<template>
  <UButton
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

const { openScanner } = useBarcodeScanner()

const handleClick = () => {
  openScanner()
}
</script>
