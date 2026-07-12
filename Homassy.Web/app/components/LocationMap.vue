<template>
  <div v-if="hasAddress">
    <USkeleton v-if="isLoading" class="w-full h-48 rounded-xl" />
    <iframe
      v-else-if="mapUrl"
      :src="mapUrl"
      :title="$t('profile.shoppingLocations.mapTitle')"
      class="w-full h-48 rounded-xl border border-default"
      loading="lazy"
      referrerpolicy="no-referrer"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'

/**
 * Shows a location on an embedded OpenStreetMap map, derived from its text address.
 * There are no stored coordinates, so the address is geocoded at runtime via Nominatim
 * (free, keyless), then embedded through OSM's `export/embed.html`. Renders nothing when
 * there is no address or the address can't be geocoded (the caller keeps the address text
 * and any "open in maps" link).
 */
const props = defineProps<{
  address?: string
  city?: string
  postalCode?: string
  country?: string
}>()

const query = computed(() =>
  [props.address, props.postalCode, props.city, props.country]
    .map(part => part?.trim())
    .filter(Boolean)
    .join(', ')
)
const hasAddress = computed(() => query.value.length > 0)

const coords = ref<{ lat: number, lon: number } | null>(null)
const isLoading = ref(false)

// A small bounding box around the point so the embed shows a street-level view with a marker.
const DELTA = 0.004
const mapUrl = computed(() => {
  if (!coords.value) return null
  const { lat, lon } = coords.value
  const bbox = `${lon - DELTA},${lat - DELTA},${lon + DELTA},${lat + DELTA}`
  return `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${lat},${lon}`
})

async function geocode() {
  coords.value = null
  if (!import.meta.client || !hasAddress.value) return

  const current = query.value
  isLoading.value = true
  try {
    const url = `https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&addressdetails=0&q=${encodeURIComponent(current)}`
    const results = await $fetch<Array<{ lat: string, lon: string }>>(url)
    // Ignore a stale response if the address changed while this request was in flight.
    if (current !== query.value) return
    const hit = results?.[0]
    coords.value = hit ? { lat: parseFloat(hit.lat), lon: parseFloat(hit.lon) } : null
  } catch {
    if (current === query.value) coords.value = null
  } finally {
    if (current === query.value) isLoading.value = false
  }
}

watch(query, geocode, { immediate: true })
</script>
