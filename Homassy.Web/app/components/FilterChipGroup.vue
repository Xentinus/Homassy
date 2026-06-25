<template>
  <div role="group" :aria-label="label">
    <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">{{ label }}</p>
    <div class="flex flex-wrap gap-2">
      <UButton
        v-for="opt in options"
        :key="opt.value"
        :label="opt.label"
        size="sm"
        class="rounded-full"
        :color="modelValue === opt.value ? 'primary' : 'neutral'"
        :variant="modelValue === opt.value ? 'solid' : 'outline'"
        :aria-pressed="modelValue === opt.value"
        @click="emit('update:modelValue', opt.value)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  label: string
  modelValue: string
  options: { label: string, value: string }[]
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>
