// https://nuxt.com/docs/api/configuration/nuxt-config
import process from 'node:process'
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  app: {
    head: {
      meta: [
        { name: 'theme-color', content: '#c9b8a0' },
        { name: 'mobile-web-app-capable', content: 'yes' },
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
        { rel: 'mask-icon', href: '/safari-pinned-tab.svg', color: '#c9b8a0' }
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
    '@nuxtjs/i18n',
    '@vite-pwa/nuxt'
  ],

  imports: {
    presets: [
      {
        from: 'vue-i18n',
        imports: ['useI18n']
      }
    ]
  },

  pwa: {
    registerType: 'autoUpdate',
    scope: '/',
    devOptions: {
      enabled: true,
      type: 'module'
    },
    manifest: {
      name: 'Homassy',
      short_name: 'Homassy',
      theme_color: '#c9b8a0',
      display: 'standalone',
      start_url: '/',
      icons: [
        {
          src: '/favicon-16x16.png',
          sizes: '16x16',
          type: 'image/png'
        },
        {
          src: '/favicon-32x32.png',
          sizes: '32x32',
          type: 'image/png'
        },
        {
          src: '/apple-touch-icon-180x180.png',
          sizes: '180x180',
          type: 'image/png'
        }
      ]
    },
    workbox: {
      importScripts: ['/sw-push.js'],
      // Let @vite-pwa/nuxt handle navigation routes via its built-in allowlist.
      // A custom 'navigate' mode handler here conflicts with the PWA navigation
      // route allowlist and causes the "not being used" warning.
      runtimeCaching: [
        {
          urlPattern: /^https:\/\/.*\.(js|css|woff2?|png|jpg|jpeg|svg|gif|webp|ico)$/,
          handler: 'CacheFirst',
          options: {
            cacheName: 'static-assets',
            expiration: {
              maxEntries: 200,
              maxAgeSeconds: 2592000 // 30 days
            }
          }
        }
      ]
    }
  },

  css: [
    '~/assets/css/main.css',
    'vue-advanced-cropper/dist/style.css'
  ],

  runtimeConfig: {
    public: {
      apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226',
      kratosPublicUrl: process.env.NUXT_PUBLIC_KRATOS_URL || 'http://localhost:4433'
    }
  },

  nitro: {
    // devProxy: {
    //   '/api': {
    //     target: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5226',
    //     changeOrigin: true
    //   }
    // }
    routeRules: {
      '/': {
        headers: {
          'Cache-Control': 'no-cache, must-revalidate'
        }
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
  },

  vite: {
    esbuild: {
      // Only drop console/debugger in production builds
      drop: process.env.NODE_ENV === 'production' ? ['console', 'debugger'] : []
    },
    build: {
      sourcemap: false,
      // Reduce memory usage during build
      minify: 'esbuild',
      rollupOptions: {
        maxParallelFileOps: 2,
        output: {
          manualChunks: undefined
        }
      }
    }
  },

  // Reduce Nitro build memory
  nitro: {
    minify: true,
    sourceMap: false,
    rollupConfig: {
      maxParallelFileOps: 2
    }
  }
})