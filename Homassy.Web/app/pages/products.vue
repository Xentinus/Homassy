<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-3">
    <div class="flex items-center gap-3">
      <UIcon name="i-lucide-package" class="h-7 w-7 text-primary-500" />
      <h1 class="text-2xl font-semibold">Products</h1>
    </div>
    <p class="text-gray-600 dark:text-gray-400">Manage your household products and inventory.</p>
  </div>
</template>

<script setup>
definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})
</script>
