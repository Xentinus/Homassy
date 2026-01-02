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
      <!-- Name with color indicator -->
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

        <!-- Family Shared Icon -->
        <UIcon
          v-if="location.isSharedWithFamily"
          name="i-lucide-users"
          class="h-4 w-4 text-primary-500 flex-shrink-0"
          :title="$t('profile.shoppingLocations.sharedWithFamily')"
        />
      </div>

      <!-- Address (only if not empty) -->
      <p
        v-if="location.address && location.address.trim() !== ''"
        class="text-xs text-gray-600 dark:text-gray-400 flex items-center gap-1"
      >
        <UIcon name="i-lucide-map-pin" class="h-3 w-3 flex-shrink-0" />
        <span class="line-clamp-1">{{ location.address }}</span>
      </p>

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
import type { ShoppingLocationInfo } from '~/types/location'

interface Props {
  location: ShoppingLocationInfo
  isActive?: boolean
}

withDefaults(defineProps<Props>(), {
  isActive: false
})

const emit = defineEmits<{
  click: []
}>()
</script>
