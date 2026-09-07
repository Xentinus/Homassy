<template>
  <svg
    viewBox="0 0 96 96"
    fill="none"
    aria-hidden="true"
    focusable="false"
  >
    <!-- Ground shadow. Repeated in every illustration so the set reads as one
         family however the shapes above it differ. -->
    <ellipse cx="48" cy="84" rx="26" ry="4" fill="currentColor" :opacity="GROUND_OPACITY" />

    <g
      stroke="currentColor"
      stroke-width="2.5"
      stroke-linecap="round"
      stroke-linejoin="round"
      :opacity="LINE_OPACITY"
    >
      <template v-if="name === 'products'">
        <path d="M48 20 78 33v34L48 80 18 67V33Z" />
        <path d="M18 33l30 13 30-13M48 46v34" />
      </template>

      <template v-else-if="name === 'shoppingList'">
        <path d="M28 22h40a4 4 0 0 1 4 4v50a4 4 0 0 1-4 4H28a4 4 0 0 1-4-4V26a4 4 0 0 1 4-4Z" />
        <path d="M38 16h20v8H38z" />
        <path d="M46 38h16M46 52h16M46 66h10" />
      </template>

      <template v-else-if="name === 'shoppingLocation'">
        <path d="M22 34h52v44a2 2 0 0 1-2 2H24a2 2 0 0 1-2-2V34Z" />
        <path d="M28 22h40l6 12H22l6-12Z" />
        <path d="M30 44h14v12H30z" />
      </template>

      <template v-else-if="name === 'storageLocation'">
        <path d="M22 20h52a2 2 0 0 1 2 2v56a2 2 0 0 1-2 2H22a2 2 0 0 1-2-2V22a2 2 0 0 1 2-2Z" />
        <path d="M20 40h56M20 60h56" />
      </template>

      <template v-else-if="name === 'automation'">
        <circle cx="42" cy="54" r="20" />
        <path d="M42 34v4M62 54h-4M42 74v-4M22 54h4" />
      </template>

      <template v-else-if="name === 'calendar'">
        <path d="M22 28h52a2 2 0 0 1 2 2v48a2 2 0 0 1-2 2H22a2 2 0 0 1-2-2V30a2 2 0 0 1 2-2Z" />
        <path d="M20 44h56M34 20v10M62 20v10" />
      </template>

      <template v-else-if="name === 'notifications'">
        <path d="M32 64c0-5 4-6 4-16a12 12 0 0 1 24 0c0 10 4 11 4 16H32Z" />
        <path d="M41 64a7 7 0 0 0 14 0" />
      </template>

      <template v-else-if="name === 'search'">
        <circle cx="43" cy="43" r="19" />
        <path d="M57 57l17 17" />
      </template>

      <template v-else-if="name === 'notFound'">
        <path d="M48 80s18-21 18-35a18 18 0 1 0-36 0c0 14 18 35 18 35Z" />
      </template>

      <template v-else-if="name === 'serverError'">
        <path d="M48 20l32 56H16l32-56Z" />
      </template>

      <template v-else-if="name === 'offline'">
        <path d="M33 66a13 13 0 0 1 1-26 17 17 0 0 1 32 4 12 12 0 0 1-1 22H33Z" />
      </template>
    </g>

    <!-- The accent group inherits `color` from `.text-primary`, so every
         `currentColor` inside it resolves to the theme's primary — no baked-in
         hex, and it flips with light/dark like the rest of the UI. -->
    <g
      class="text-primary"
      stroke="currentColor"
      stroke-width="2.5"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <template v-if="name === 'products'">
        <path d="M33 26.5 63 39.5" />
      </template>

      <template v-else-if="name === 'shoppingList'">
        <path d="M32 37l3 3 6-7M32 51l3 3 6-7" />
      </template>

      <template v-else-if="name === 'shoppingLocation'">
        <path d="M52 60h14v20H52z" />
      </template>

      <template v-else-if="name === 'storageLocation'">
        <path d="M28 26h14v12H28zM50 46h16v12H50z" />
      </template>

      <template v-else-if="name === 'automation'">
        <path d="M42 54V42M42 54l9 6" />
        <path d="M72 18l3 7 7 3-7 3-3 7-3-7-7-3 7-3 3-7Z" />
      </template>

      <template v-else-if="name === 'calendar'">
        <path d="M48 54v14M41 61h14" />
      </template>

      <template v-else-if="name === 'notifications'">
        <circle cx="66" cy="30" r="6" />
      </template>

      <template v-else-if="name === 'search'">
        <path d="M37 37l12 12M49 37L37 49" />
      </template>

      <template v-else-if="name === 'notFound'">
        <path d="M43 39a5 5 0 1 1 7 7c-1.5 1.5-2 2.5-2 4" />
        <path d="M48 57h.01" stroke-width="4" />
      </template>

      <template v-else-if="name === 'serverError'">
        <path d="M48 38v18M48 66h.01" stroke-width="4" stroke-linecap="round" />
      </template>

      <template v-else-if="name === 'offline'">
        <path d="M24 22l48 48" />
      </template>
    </g>
  </svg>
</template>

<script setup lang="ts">
/**
 * The illustration set behind EmptyState.vue (and app/error.vue).
 *
 * Inline SVG on purpose: no extra network request, nothing to cache-bust, and
 * — the reason it matters here — every stroke is `currentColor`, so a single
 * `text-*` class on the element themes the whole drawing. Never put a literal
 * hex in here: it cannot follow light/dark like the Nuxt UI tokens do.
 *
 * The drawing is in two groups. The outer one is the muted line work and takes
 * its colour from whatever the caller sets. The second carries `.text-primary`,
 * which re-anchors `currentColor` for its children onto the theme's primary —
 * that is the one accent each illustration is allowed.
 *
 * `viewBox` is a 96×96 square for all of them, so they are interchangeable at
 * any size the caller picks. Sizing and the muted colour are the caller's job —
 * a `class` lands on the <svg> through Vue's normal attribute fallthrough.
 */
import type { EmptyStateIllustrationName } from '~/types/emptyState'

/** Faint enough to read as a shadow in both themes. */
const GROUND_OPACITY = 0.1
/** The line work sits below full strength so the primary accent leads the eye. */
const LINE_OPACITY = 0.7

defineProps<{
  name: EmptyStateIllustrationName
}>()
</script>
