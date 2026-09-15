import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

/**
 * Unit tests for the pure logic behind the composables — colour mapping, the undo queue,
 * realtime-status derivation, activity day grouping. Deliberately no Nuxt/Vue runtime: these
 * modules are plain TypeScript so they can be tested without booting the app. Anything that
 * needs a rendered component is verified in the browser instead.
 */
export default defineConfig({
  resolve: {
    alias: {
      '~': fileURLToPath(new URL('./app', import.meta.url)),
      '@': fileURLToPath(new URL('./app', import.meta.url))
    }
  },
  // Nuxt injects `import.meta.client`; outside the app it is simply undefined, which would make
  // every browser-only branch unreachable from a test. The tests run in Node with the browser
  // globals they need stubbed, so the client branch is the one to compile.
  define: {
    'import.meta.client': 'true',
    'import.meta.server': 'false'
  },
  test: {
    environment: 'node',
    include: ['tests/unit/**/*.spec.ts']
  }
})
