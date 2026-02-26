<template>
  <div
    v-if="isPulling || isRefreshing"
    class="flex items-center justify-center overflow-hidden transition-all duration-150 ease-out"
    :style="{ height: containerHeight }"
  >
    <div class="flex flex-col items-center justify-center gap-1">
      <UIcon
        v-if="isRefreshing"
        name="i-lucide-loader-2"
        class="h-6 w-6 text-primary-500 animate-spin"
      />
      <UIcon
        v-else
        name="i-lucide-arrow-down"
        class="h-6 w-6 text-primary-500 transition-transform duration-200 ease-in-out"
        :style="{ transform: isReady ? 'rotate(180deg)' : 'rotate(0deg)' }"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  pullDistance: number
  isPulling: boolean
  isRefreshing: boolean
  isReady: boolean
}>()

const containerHeight = computed(() => {
  if (props.isRefreshing) return '64px'
  if (props.isPulling) return `${Math.min(props.pullDistance, 64)}px`
  return '0px'
})
</script>
