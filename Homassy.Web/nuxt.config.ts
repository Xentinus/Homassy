// https://nuxt.com/docs/api/configuration/nuxt-config
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
    'nuxt-api-party'
  ],

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226'
    }
  },

  nitro: {
    devProxy: {
      '/api': {
        target: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226',
        changeOrigin: true
      }
    }
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
  }
})