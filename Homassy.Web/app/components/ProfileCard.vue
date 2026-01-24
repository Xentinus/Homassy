<template>
  <NuxtLink :to="to">
    <div class="rounded-lg border border-primary-200/50 dark:border-primary-700/50 p-4 relative hover:border-primary-300 dark:hover:border-primary-700 transition-colors cursor-pointer flex items-start gap-3 h-full">
      <UIcon :name="icon" class="h-7 w-7 text-primary-500 mr-2 flex-shrink-0 self-center" />
      <div class="flex-1">
        <h2 class="text-base font-semibold mb-1">{{ title }}</h2>
        <div v-if="description" class="text-xs text-gray-500 dark:text-gray-400">{{ description }}</div>
      </div>
      <UIcon name="i-lucide-chevron-right" class="absolute top-3 right-3 text-gray-400" />
    </div>
  </NuxtLink>
</template>

<script setup lang="ts">
defineProps<{
  icon: string
  title: string
  description?: string
  to: string
}>()
</script>
