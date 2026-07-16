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
 * Shows a location on an embedded OpenStreetMap map.
 * Prefers stored coordinates (`latitude`/`longitude`) when provided and skips any network
 * call; otherwise falls back to geocoding the text address at runtime via Nominatim
 * (free, keyless) — for older locations saved before coordinates were stored. Embedded
 * through OSM's `export/embed.html`. Renders nothing when there is neither coordinates nor
 * a resolvable address (the caller keeps the address text and any "open in maps" link).
 */
const props = defineProps<{
  address?: string
  city?: string
  postalCode?: string
  country?: string
  latitude?: number
  longitude?: number
}>()

const { geocode: geocodeAddress, buildAddressQuery } = useGeocoding()

const query = computed(() =>
  buildAddressQuery([props.address, props.postalCode, props.city, props.country])
)
const hasStoredCoords = computed(() =>
  typeof props.latitude === 'number' && typeof props.longitude === 'number'
)
const hasAddress = computed(() => query.value.length > 0 || hasStoredCoords.value)

const geocodedCoords = ref<{ lat: number, lon: number } | null>(null)
const isLoading = ref(false)

const coords = computed<{ lat: number, lon: number } | null>(() =>
  hasStoredCoords.value
    ? { lat: props.latitude as number, lon: props.longitude as number }
    : geocodedCoords.value
)

// A small bounding box around the point so the embed shows a street-level view with a marker.
const DELTA = 0.004
const mapUrl = computed(() => {
  if (!coords.value) return null
  const { lat, lon } = coords.value
  const bbox = `${lon - DELTA},${lat - DELTA},${lon + DELTA},${lat + DELTA}`
  return `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${lat},${lon}`
})

async function resolveCoords() {
  geocodedCoords.value = null

  // Stored coordinates win — no network call needed.
  if (hasStoredCoords.value) return
  if (!import.meta.client || query.value.length === 0) return

  const current = query.value
  isLoading.value = true
  try {
    const result = await geocodeAddress(current)
    // Ignore a stale response if the address changed while this request was in flight.
    if (current !== query.value) return
    geocodedCoords.value = result
  } finally {
    if (current === query.value) isLoading.value = false
  }
}

watch(
  () => [query.value, hasStoredCoords.value] as const,
  resolveCoords,
  { immediate: true }
)
</script>
