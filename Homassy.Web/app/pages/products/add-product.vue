<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6">
    <!-- Header with back button -->
    <div class="flex items-center gap-3">
      <NuxtLink to="/products">
        <UButton
          icon="i-lucide-arrow-left"
          color="neutral"
          variant="ghost"
        />
      </NuxtLink>
      <UIcon name="i-lucide-package-plus" class="text-xl text-primary" />
      <div>
        <h1 class="text-2xl font-semibold">Termék hozzáadása</h1>
      </div>
    </div>

    <!-- Üres oldal, később kitölthető -->
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})
</script>
