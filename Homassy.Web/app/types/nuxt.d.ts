/**
 * Nuxt App type definitions
 */
import type { ApiFetch } from '~/types/api'

declare module '#app' {
  interface NuxtApp {
    $api: ApiFetch
  }
}

export {}
