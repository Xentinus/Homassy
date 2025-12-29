<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-3">
    <div class="flex items-center gap-3">
      <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 text-primary-500" />
      <h1 class="text-2xl font-semibold">Shopping Lists</h1>
    </div>
    <p class="text-gray-600 dark:text-gray-400">Plan and track your shared shopping lists.</p>
  </div>
</template>

<script setup>
definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})
</script>
