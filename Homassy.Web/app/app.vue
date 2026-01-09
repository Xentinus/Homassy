<script setup>
const { locale, t } = useI18n()

useHead({
  meta: [
    { name: 'viewport', content: 'width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no' }
  ],
  link: [
    { rel: 'icon', href: '/favicon.ico' }
  ],
  htmlAttrs: {
    lang: computed(() => locale.value)
  }
})

const title = computed(() => t('meta.default.title'))
const description = computed(() => t('meta.default.description'))
const ogTitle = computed(() => t('meta.default.ogTitle'))
const ogDescription = computed(() => t('meta.default.ogDescription'))
const toaster = { position: 'top-right' }

useSeoMeta({
  title,
  description,
  ogTitle,
  ogDescription,
  twitterCard: 'summary_large_image',
  twitterTitle: title,
  twitterDescription: description
})
</script>

<template>
  <NuxtLayout :toaster="toaster">
    <NuxtPage />
  </NuxtLayout>
</template>
