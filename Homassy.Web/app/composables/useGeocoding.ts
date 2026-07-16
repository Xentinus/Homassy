/**
 * Geocoding helper built on Nominatim (OpenStreetMap, free & keyless).
 *
 * Turns a free-text address into `{ lat, lon }` coordinates. Used both when saving a
 * shopping location (to persist coordinates once) and as a runtime fallback for older
 * locations that have an address but no stored coordinates.
 *
 * Nominatim's usage policy asks for a low request rate; callers should geocode sparingly
 * (once on save, or once per distinct address per session) and cache the result.
 */
export interface GeoCoordinates {
  lat: number
  lon: number
}

/** Builds a single-line query string from address parts, skipping empty ones. */
export const buildAddressQuery = (parts: Array<string | undefined | null>): string =>
  parts
    .map(part => part?.trim())
    .filter(Boolean)
    .join(', ')

export const useGeocoding = () => {
  /**
   * Geocode a free-text address query. Returns null when the query is empty, the code runs
   * on the server, or the address can't be resolved (never throws).
   */
  const geocode = async (query: string): Promise<GeoCoordinates | null> => {
    if (!import.meta.client) return null

    const trimmed = query?.trim()
    if (!trimmed) return null

    try {
      const url = `https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&addressdetails=0&q=${encodeURIComponent(trimmed)}`
      const results = await $fetch<Array<{ lat: string, lon: string }>>(url)
      const hit = results?.[0]
      if (!hit) return null

      const lat = parseFloat(hit.lat)
      const lon = parseFloat(hit.lon)
      if (Number.isNaN(lat) || Number.isNaN(lon)) return null

      return { lat, lon }
    } catch {
      return null
    }
  }

  return { geocode, buildAddressQuery }
}
