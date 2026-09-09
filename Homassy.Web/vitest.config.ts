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
  test: {
    environment: 'node',
    include: ['tests/unit/**/*.spec.ts']
  }
})
