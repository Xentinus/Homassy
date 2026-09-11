// https://nuxt.com/docs/api/configuration/nuxt-config

/**
 * The build-time environment, for the `process.env` reads further down.
 *
 * Declared here rather than pulled in with `@types/node`, and rather than the
 * `import process from 'node:process'` this file used to carry (which the typechecker rejected
 * with TS2307, having no Node types to resolve it against).
 *
 * Adding `@types/node` is what you would reach for, and it is the wrong trade here: this project
 * has no Node types in its tree, so npm has to re-resolve to add them — and npm 11 prunes the
 * `oxc-parser` platform bindings that the committed, npm-10-generated lockfile records, which
 * makes `npm ci` fail in CI with twenty "missing from lock file" errors. A four-line ambient
 * declaration for the two properties this file actually touches costs nothing and cannot churn
 * the lockfile.
 */
declare const process: {
  env: Record<string, string | undefined>
}

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },

  app: {
    // Cross-fade page content on route change. The bottom nav lives in the layout
    // (outside <NuxtPage/>), so it stays visible and uninterrupted during navigation.
    pageTransition: { name: 'page', mode: 'out-in' },
    head: {
      meta: [
        // Light-theme value only — SSR cannot know the preference (it lives in
        // localStorage), so this matches @nuxtjs/color-mode's `fallback: 'light'`.
        // plugins/theme-color.client.ts takes it over on the client and keeps it
        // equal to the app's own background in whichever theme is active.
        // `tagPriority: 'high'` so it wins the `meta[name]` dedupe against the
        // manifest-derived tag @vite-pwa/nuxt injects (brand tan, theme-blind).
        { name: 'theme-color', content: '#ffffff', tagPriority: 'high' },
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
      ],
      script: [
        // Flag standalone/PWA launches before first paint so the boot splash
        // (SplashScreen.vue) can show. iOS home-screen web apps do not reliably
        // match `@media (display-mode: standalone)`, so we also check the
        // iOS-only `navigator.standalone`. Runs synchronously in <head> — no flash.
        {
          innerHTML:
            '(function(){try{if(window.navigator.standalone===true||window.matchMedia("(display-mode: standalone)").matches){document.documentElement.classList.add("pwa-standalone")}}catch(e){}})()',
          tagPosition: 'head',
          tagPriority: 1
        }
      ]
    }
  },

  modules: [
    '@nuxt/content',
    '@nuxt/eslint',
    // Listed explicitly even though @nuxt/ui registers it internally: this pins
    // the version the font config below is written against, and makes Nuxt UI's
    // `hasNuxtModule` check short-circuit its own registration (its defaults are
    // still merged into `fonts`).
    //
    // The version matters. @nuxt/ui depends on ^0.12.1, and on 0.12.1 this exact
    // config resolves Public Sans through a different provider: nine static
    // `.woff` faces, one per weight, unsubsetted, alongside the variable pair.
    // package.json therefore asks for ^0.14.0 and carries an `overrides` entry
    // pinning @nuxt/ui to the same copy — without it npm installs both, and the
    // duplicate's own dependency subtree is what desynchronised the lockfile.
    '@nuxt/fonts',
    '@nuxt/image',
    '@nuxt/scripts',
    '@nuxt/ui',
    '@pinia/nuxt',
    'nuxt-api-party',
    '@nuxtjs/i18n',
    '@vite-pwa/nuxt'
  ],

  // `--font-sans` in app/assets/css/main.css declares 'Public Sans' for every
  // Tailwind/Nuxt UI text token, so the face has to actually be delivered —
  // before this it was not, and the whole app fell through to the platform
  // default (a different typeface on every OS).
  //
  // @nuxt/fonts downloads the face at build time and serves it from our own
  // origin: no third-party request, and it lands inside the service worker's
  // `static-assets` runtime cache, whose pattern already matches `woff2?`.
  //
  // The family name is picked up from the `--font-*` custom property by
  // @nuxt/fonts' default `processCSSVariables: 'font-prefixed-only'` — Tailwind 4
  // resolves the token to `var(--font-sans)` and never emits a literal
  // `font-family: 'Public Sans'` for the CSS scan to find.
  fonts: {
    defaults: {
      // No italic face on purpose. The four `italic` usages are muted notes and
      // hints, where the browser's synthetic oblique is worth the bytes saved.
      styles: ['normal'],
      // latin covers en/de (umlauts), latin-ext the Hungarian ő/ű.
      subsets: ['latin', 'latin-ext'],
      // Named explicitly so fontaine can emit the metric overrides
      // (size-adjust / ascent-override) for the fallback faces. That is what
      // keeps `font-display: swap` from reflowing the page when the real font
      // arrives.
      fallbacks: {
        'sans-serif': ['Segoe UI', 'Roboto', 'Helvetica Neue', 'Arial']
      }
    },
    families: [
      {
        name: 'Public Sans',
        // No `provider` on purpose. Pinning one takes fontless down the
        // "override provider" branch, which prefers the provider's own
        // `fallbacks` (Google reports the bare generic `sans-serif`) over
        // `defaults.fallbacks` below — and a `local('sans-serif')` fallback face
        // carries no metrics, so the size-adjust overrides come out as no-ops.
        // Letting the provider be auto-detected keeps our fallback list.
        //
        // A weight *range* asks unifont for the variable face, so the app gets
        // one file per subset covering 400/500/600/700 (the four weights the
        // markup uses) instead of four static files per subset. That is both
        // fewer bytes and — the point of it — few enough files that preloading
        // them is honest: two requests, both of which every hu/de page needs.
        // Four static weights × two subsets would have been eight preloads.
        weights: ['100 900'],
        // Preload is off by default for subsetted faces (they carry a
        // unicode-range), so it has to be asked for.
        preload: true
      }
    ]
  },

  image: {
    providers: {
      // Passthrough provider, used by UserAvatar / ProductImage for the API's image endpoints.
      // Those URLs must reach the browser verbatim: IPX would fetch them from the Nitro server,
      // which has no Kratos session cookie, and the API already serves a purpose-sized variant
      // per `?size=`. Registering it here is also what puts "none" in the `provider` prop's type.
      none: { provider: 'none' }
    }
  },

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
      // The manifest takes a single value and cannot be theme-aware, while the
      // in-app splash now follows the active theme (SplashScreen.vue uses
      // `--ui-bg`). Tuned to the light background, so a dark-theme launch can
      // show a brief light flash on Android's generated launch screen.
      background_color: '#ffffff',
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
        },
        // >=192px icons enable Android's branded generated splash screen.
        {
          src: '/android-chrome-192x192.png',
          sizes: '192x192',
          type: 'image/png',
          purpose: 'any'
        },
        {
          src: '/android-chrome-512x512.png',
          sizes: '512x512',
          type: 'image/png',
          purpose: 'any'
        },
        {
          src: '/android-chrome-512x512.png',
          sizes: '512x512',
          type: 'image/png',
          purpose: 'maskable'
        }
      ],
      // Long-press / right-click menu on the installed app's icon (#118). Each entry
      // deep-links to the view it names, and the two that are actions rather than
      // destinations carry `?action=`, which `useDeepLinkAction` turns into the
      // drawer/FAB action on arrival and then strips from the URL.
      //
      // None of these paths falls under `navigateFallbackDenylist` below (`/kratos`,
      // `/api`, `/hubs`), so a shortcut launch is an ordinary navigation the `pages`
      // runtime cache can serve when the network is down.
      //
      // Icons are generated by `scripts/generate-shortcut-icons.mjs` from the same
      // Lucide glyphs the in-app UI uses. 96×96 is the size Android's launcher asks
      // for; the 192×192 pair covers higher-density launchers.
      shortcuts: [
        {
          name: 'Shopping list',
          short_name: 'Shopping',
          url: '/shopping-lists',
          icons: [
            { src: '/shortcuts/shopping-list-96x96.png', sizes: '96x96', type: 'image/png' },
            { src: '/shortcuts/shopping-list-192x192.png', sizes: '192x192', type: 'image/png' }
          ]
        },
        {
          name: 'Add item',
          short_name: 'Add',
          url: '/products?action=add',
          icons: [
            { src: '/shortcuts/add-item-96x96.png', sizes: '96x96', type: 'image/png' },
            { src: '/shortcuts/add-item-192x192.png', sizes: '192x192', type: 'image/png' }
          ]
        },
        {
          name: 'Scan barcode',
          short_name: 'Scan',
          url: '/products?action=scan',
          icons: [
            { src: '/shortcuts/scan-barcode-96x96.png', sizes: '96x96', type: 'image/png' },
            { src: '/shortcuts/scan-barcode-192x192.png', sizes: '192x192', type: 'image/png' }
          ]
        },
        {
          name: 'Calendar',
          short_name: 'Calendar',
          url: '/calendar',
          icons: [
            { src: '/shortcuts/calendar-96x96.png', sizes: '96x96', type: 'image/png' },
            { src: '/shortcuts/calendar-192x192.png', sizes: '192x192', type: 'image/png' }
          ]
        }
      ],
      // Receive text and images shared from other apps (#118). POST + multipart is
      // the only enctype that can carry a file, so the navigation is answered by
      // `public/sw-share.js` (imported into the service worker below), which stashes
      // the payload and 303s to the plain `/share` route.
      share_target: {
        action: '/share',
        method: 'POST',
        enctype: 'multipart/form-data',
        params: {
          title: 'title',
          text: 'text',
          url: 'url',
          files: [
            {
              name: 'image',
              accept: ['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'image/*']
            }
          ]
        }
      }
    },
    workbox: {
      // Both run before Workbox registers its own listeners, which is what lets
      // sw-share.js answer the share target's POST navigation (Workbox's routes are
      // GET-only, so it would never handle it anyway).
      importScripts: ['/sw-push.js', '/sw-share.js'],
      // @vite-pwa/nuxt sets `globPatterns` itself (it pushes the payload and
      // app-manifest JSON onto it), which means vite-plugin-pwa's own default
      // never applies and nothing HTML is precached. The offline document has to
      // be, or `precacheFallback` below has nothing to answer with. Just that
      // one file: the rest of the app is runtime-cached by the routes below, and
      // precaching `_nuxt/**` would make every install download the bundle.
      globPatterns: ['offline/index.html'],
      // @vite-pwa/nuxt fills `navigateFallback` in with '/' when the key is
      // absent, which registers a NavigationRoute answering EVERY navigation
      // from the precache — the SPA-shell model. This app is server-rendered, so
      // that trades the SSR document for a cached shell on every page load, and
      // it is also what made a custom `request.mode === 'navigate'` runtime
      // route dead code (the old "not being used" warning). Declaring the key
      // as undefined opts out: the module only defaults it when it is missing.
      navigateFallback: undefined,
      // Server-handled paths behind the same-origin reverse proxy (Kratos flows,
      // REST API, SignalR) must never be answered from the cache. Kept in step
      // with the exclusion in the navigation route below.
      navigateFallbackDenylist: [/^\/kratos\//, /^\/api\//, /^\/hubs\//],
      runtimeCaching: [
        {
          // Documents. Fresh when the network is there, the last copy we saw
          // when it is not, and the branded /offline page when neither is
          // available — that last case is the one the browser would otherwise
          // answer with its own error screen, which in an installed PWA (no
          // address bar) is a dead end. app/error.vue can only cover failures
          // that happen once the app is already running.
          urlPattern: ({ request, url }) =>
            request.mode === 'navigate' && !/^\/(kratos|api|hubs)\//.test(url.pathname),
          handler: 'NetworkFirst',
          options: {
            cacheName: 'pages',
            expiration: {
              maxEntries: 50,
              maxAgeSeconds: 86400 // 1 day
            },
            // `PrecacheFallbackPlugin` looks this up in the precache by exact
            // key. workbox-build strips the `/index.html` off the globbed
            // `offline/index.html`, so `/offline` is the key it lands under.
            precacheFallback: {
              fallbackURL: '/offline'
            }
          }
        },
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
        },
        {
          // Avatars and product images. They are served from the API rather than as files, so
          // they have no extension for the `static-assets` rule above to match — and being under
          // `/api/` they would otherwise never be cached at all. CacheFirst is safe because each
          // URL carries the image's content hash as `?v=`: a changed picture is a different URL,
          // never a stale hit. Same-origin only, which is the production reverse-proxy setup; in
          // development the API is a different origin and the browser cache handles it instead.
          urlPattern: ({ url, sameOrigin }) =>
            sameOrigin === true
            && /^\/api\/v[\d.]+\/(User\/[^/]+\/profile-picture|Product\/[^/]+\/image)$/i.test(url.pathname),
          handler: 'CacheFirst',
          options: {
            cacheName: 'remote-images',
            expiration: {
              maxEntries: 300,
              maxAgeSeconds: 2592000 // 30 days
            },
            cacheableResponse: {
              statuses: [200]
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
    // /offline is the service worker's fallback document, so it has to exist as
    // a static file for the SW to precache (pwa.workbox.runtimeCaching above).
    prerender: {
      routes: ['/offline']
    },

    // Reduce Nitro build memory
    minify: true,
    sourceMap: false,
    rollupConfig: {
      maxParallelFileOps: 2
    },
    routeRules: {
      '/': {
        headers: {
          'Cache-Control': 'no-cache, must-revalidate'
        }
      },
      // Settings + security/notifications/family were folded into the /profile
      // grouped list + bottom-sheet drawers.
      '/profile/settings': { redirect: '/profile' },
      '/profile/security': { redirect: '/profile' },
      '/profile/notifications': { redirect: '/profile' },
      '/profile/family': { redirect: '/profile' }
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
  }
})