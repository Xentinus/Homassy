<template>
  <div>
    <InventoryOverviewDrawer
      v-model:open="isOpen"
      :product-public-id="productPublicId"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

definePageMeta({
  layout: 'auth',
  middleware: 'auth'
})

const route = useRoute()
const router = useRouter()

const productPublicId = ref<string | null>((route.params.publicId as string) || null)
const isOpen = ref(false)

// Open the overview drawer once mounted (the drawer loads on the closed→open transition).
onMounted(() => {
  if (productPublicId.value) isOpen.value = true
  else router.replace('/products')
})

// Closing the drawer leaves this route: go back if we can, otherwise to the grid.
watch(isOpen, (open) => {
  if (open) return
  if (import.meta.client && window.history.length > 1) router.back()
  else router.replace('/products')
})
</script>
