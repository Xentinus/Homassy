// https://nuxt.com/docs/api/configuration/nuxt-config
import process from 'node:process'
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  modules: [
    '@nuxt/content',
    '@nuxt/eslint',
    '@nuxt/image',
    '@nuxt/scripts',
    '@nuxt/ui',
    '@pinia/nuxt',
    'nuxt-api-party',
    '@nuxtjs/i18n'
  ],

  css: [
    '~/assets/css/main.css',
    'vue-advanced-cropper/dist/style.css'
  ],

  runtimeConfig: {
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226'
    }
  },

  nitro: {
    // devProxy: {
    //   '/api': {
    //     target: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226',
    //     changeOrigin: true
    //   }
    // }
  },

  devServer: {
    host: '0.0.0.0',
    port: 3000
  },

  apiParty: {
    endpoints: {
      homassyApi: {
        url: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226',
        headers: {
          'Content-Type': 'application/json'
        }
      }
    }
  },

  i18n: {
    locales: [
      {
        code: 'en',
        language: 'en-US',
        file: 'en.json',
        name: 'English'
      },
      {
        code: 'hu',
        language: 'hu-HU',
        file: 'hu.json',
        name: 'Magyar'
      },
      {
        code: 'de',
        language: 'de-DE',
        file: 'de.json',
        name: 'Deutsch'
      }
    ],
    defaultLocale: 'en',
    strategy: 'no_prefix',
    langDir: 'locales',
    lazy: true,
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'homassy_locale',
      redirectOn: 'root',
      alwaysRedirect: false,
      fallbackLocale: 'en'
    },
    vueI18n: './i18n.config.ts'
  }
})