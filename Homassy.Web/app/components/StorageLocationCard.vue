<template>
  <div
    class="p-4 rounded-lg border-2 transition-all duration-200 cursor-pointer hover:shadow-md"
    :class="[
      isActive
        ? 'border-primary-500 bg-primary-50 dark:bg-primary-900/10'
        : 'border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800'
    ]"
    @click="emit('click')"
  >
    <!-- Location Info -->
    <div class="space-y-2">
      <!-- Name with color indicator and freezer icon -->
      <div class="flex items-center gap-2">
        <!-- Color circle (8px diameter) -->
        <div
          v-if="location.color"
          class="w-2 h-2 rounded-full flex-shrink-0"
          :style="{ backgroundColor: location.color }"
        />

        <!-- Name -->
        <h3 class="font-semibold text-sm line-clamp-2 text-gray-900 dark:text-white flex-1">
          {{ location.name }}
        </h3>

        <!-- Freezer icon -->
        <UIcon
          v-if="location.isFreezer"
          name="i-lucide-snowflake"
          class="h-4 w-4 text-blue-500 flex-shrink-0"
        />
      </div>

      <!-- Description (only if not empty) -->
      <p
        v-if="location.description && location.description.trim() !== ''"
        class="text-xs text-gray-600 dark:text-gray-400 line-clamp-2"
      >
        {{ location.description }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { StorageLocationInfo } from '~/types/location'

interface Props {
  location: StorageLocationInfo
  isActive?: boolean
}

withDefaults(defineProps<Props>(), {
  isActive: false
})

const emit = defineEmits<{
  click: []
}>()
</script>
