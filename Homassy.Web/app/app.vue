<script setup>
const { locale } = useI18n()

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

const title = 'Homassy'
const description = 'A modern, elegant application built with Nuxt UI.'
const toaster = { position: 'top-right' }

useSeoMeta({
  title,
  description,
  ogTitle: title,
  ogDescription: description,
  twitterCard: 'summary_large_image'
})
</script>

<template>
  <NuxtLayout :toaster="toaster">
    <NuxtPage />
  </NuxtLayout>
</template>
