// https://nuxt.com/docs/api/configuration/nuxt-config
import process from 'node:process'
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  app: {
    head: {
      meta: [
        { name: 'theme-color', content: '#c9b8a0' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
        { name: 'apple-mobile-web-app-status-bar-style', content: 'default' }
      ],
      link: [
        { rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' },
        { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' },
        { rel: 'icon', type: 'image/png', sizes: '16x16', href: '/favicon-16x16.png' },
        { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/favicon-32x32.png' },
        { rel: 'icon', type: 'image/png', sizes: '48x48', href: '/favicon-48x48.png' },
        { rel: 'apple-touch-icon', sizes: '180x180', href: '/apple-touch-icon-180x180.png' },
        { rel: 'apple-touch-icon', sizes: '167x167', href: '/apple-touch-icon-167x167.png' },
        { rel: 'apple-touch-icon', sizes: '152x152', href: '/apple-touch-icon-152x152.png' },
        { rel: 'apple-touch-icon', href: '/apple-touch-icon.png' },
        { rel: 'mask-icon', href: '/safari-pinned-tab.svg', color: '#c9b8a0' },
        { rel: 'manifest', href: '/site.webmanifest' }
      ]
    }
  },

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