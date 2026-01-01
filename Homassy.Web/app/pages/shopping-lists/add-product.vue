<template>
  <div>
    <!-- Fixed Header -->
    <div class="fixed top-0 left-0 right-0 z-10 bg-white dark:bg-gray-900 px-6 sm:px-10 lg:px-16 py-6 border-b border-gray-200 dark:border-gray-800 space-y-3">
      <!-- Title Row -->
      <div class="flex items-center gap-3">
        <NuxtLink to="/shopping-lists">
          <UButton
            icon="i-lucide-arrow-left"
            variant="ghost"
            size="sm"
          />
        </NuxtLink>
        <UIcon name="i-lucide-shopping-cart" class="h-7 w-7 text-primary-500" />
        <h1 class="text-2xl font-semibold">{{ $t('pages.shoppingLists.addProduct.title') }}</h1>
      </div>

      <!-- Description -->
      <p class="text-gray-600 dark:text-gray-400">
        {{ $t('pages.shoppingLists.addProduct.description') }}
      </p>
    </div>

    <!-- Content Section (with padding for fixed header) -->
    <div class="pt-48 px-4 sm:px-8 lg:px-14 pb-6">
      <div class="max-w-2xl mx-auto">
        <div class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-6">
          <p class="text-gray-500 dark:text-gray-400 text-center">
            {{ $t('pages.shoppingLists.addProduct.placeholder') }}
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const { t: $t } = useI18n()
</script>
