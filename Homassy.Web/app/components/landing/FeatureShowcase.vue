<template>
  <section
    ref="sectionEl"
    class="py-14 sm:py-20 transition-[opacity,transform] duration-700 ease-out motion-reduce:transition-none"
    :class="revealed ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-6'"
  >
    <div class="mx-auto grid max-w-5xl items-center gap-10 px-4 sm:px-6 lg:grid-cols-2 lg:gap-16">
      <!-- `lg:order-2` flips the pairing every other section, so the page has a rhythm instead
           of a column of identical rows. -->
      <div :class="reversed ? 'lg:order-2' : ''">
        <p class="inline-flex items-center gap-2 rounded-full bg-primary-100 px-3 py-1 text-xs font-semibold text-primary-700 dark:bg-primary-500/15 dark:text-primary-300">
          <UIcon :name="icon" class="h-3.5 w-3.5" />
          {{ eyebrow }}
        </p>
        <h2 class="mt-4 text-2xl font-semibold tracking-tight sm:text-3xl">{{ title }}</h2>
        <p class="mt-3 text-muted">{{ description }}</p>

        <ul v-if="points?.length" class="mt-5 space-y-2.5">
          <li v-for="point in points" :key="point" class="flex items-start gap-2.5 text-sm">
            <UIcon name="i-lucide-check" class="mt-0.5 h-4 w-4 shrink-0 text-primary-500" />
            <span>{{ point }}</span>
          </li>
        </ul>
      </div>

      <div :class="reversed ? 'lg:order-1' : ''">
        <slot />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'

/**
 * One "here is the feature, here is what it looks like" band on the landing page (#123).
 *
 * The reveal is `opacity` + `translate` only — both compositor properties — on a section whose
 * space is already reserved, so nothing about it can shift the page or stall the main thread.
 */
defineProps<{
  eyebrow: string
  icon: string
  title: string
  description: string
  points?: string[]
  /** Put the device on the left and the copy on the right, from `lg` up. */
  reversed?: boolean
}>()

const sectionEl = ref<HTMLElement | null>(null)
const { revealed } = useScrollReveal(sectionEl)
</script>
