<template>
  <div class="px-4 sm:px-6 lg:px-8 py-6 space-y-6 max-w-2xl mx-auto">
    <!-- Header with back button -->
    <div class="flex items-center gap-3">
      <NuxtLink to="/profile">
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" />
      </NuxtLink>
      <UIcon name="i-lucide-database" class="text-xl text-primary" />
      <h1 class="text-2xl font-semibold">{{ $t('profile.masterData.title') }}</h1>
    </div>

    <SettingsGroup :hint="$t('profile.masterData.description')">
      <SettingsRow
        :label="$t('profile.allProducts.title')"
        :description="$t('profile.allProducts.cardDescription')"
        icon="i-lucide-package"
        to="/profile/products"
      />
      <SettingsRow
        :label="$t('profile.storageLocations.title')"
        :description="$t('profile.storageLocations.cardDescription')"
        icon="i-lucide-warehouse"
        to="/profile/storage-locations"
      />
      <SettingsRow
        :label="$t('profile.shoppingLocations.title')"
        :description="$t('profile.shoppingLocations.cardDescription')"
        icon="i-lucide-shopping-cart"
        to="/profile/shopping-locations"
      />
      <SettingsRow
        :label="$t('profile.automation.title')"
        :description="$t('profile.automation.cardDescription')"
        icon="i-lucide-timer"
        to="/profile/automation"
      />
      <SettingsRow
        :label="$t('profile.family.externalCalendars.title')"
        :description="$t('profile.family.externalCalendars.description')"
        icon="i-lucide-calendar-plus"
        to="/profile/external-calendars"
      />
    </SettingsGroup>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: 'auth', middleware: 'auth' })
</script>
