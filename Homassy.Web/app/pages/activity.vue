<template>
  <div>
    <UPageHero
      title="Homassy"
      description="Manage your household inventory, shopping lists, and share everything with your family."
    />

    <UPageSection
      id="features"
      title="Key Features"
      :features="[
        {
          icon: 'i-lucide-user-check',
          title: 'User Authentication',
          description: 'Passwordless email-code login. Secure token-based authentication for signing in and out.'
        },
        {
          icon: 'i-lucide-users',
          title: 'Family Management',
          description: 'Create families, invite members with a code, and manage shared data together.'
        },
        {
          icon: 'i-lucide-package-check',
          title: 'Products and Inventory',
          description: 'Track products and inventory: add, move, consume, and mark favorites.'
        },
        {
          icon: 'i-lucide-shopping-cart',
          title: 'Shopping Lists',
          description: 'Shared shopping lists with items, quick purchase feature that adds directly to inventory.'
        },
        {
          icon: 'i-lucide-map-pin',
          title: 'Locations',
          description: 'Store shops and home storage locations; modify multiple items at once.'
        },
        {
          icon: 'i-lucide-barcode',
          title: 'Barcode and Data Quality',
          description: 'Barcode scanning and validation, Open Food Facts data, secure image uploads.'
        }
      ]"
    />
  </div>
</template>

<script setup>
definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})
</script>
