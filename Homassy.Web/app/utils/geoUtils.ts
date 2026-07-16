/**
 * Geo helpers for proximity features (shopping-list "you are here" highlight & notification).
 */

/** Default radius (metres) within which a shopping location counts as "you are here". */
export const NEARBY_RADIUS_METERS = 150

const EARTH_RADIUS_METERS = 6_371_000

const toRadians = (degrees: number): number => (degrees * Math.PI) / 180

/**
 * Great-circle distance between two coordinates in metres (haversine formula).
 * Accurate enough for the short, city-scale distances this feature deals with.
 */
export const distanceMeters = (
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number
): number => {
  const dLat = toRadians(lat2 - lat1)
  const dLon = toRadians(lon2 - lon1)
  const a
    = Math.sin(dLat / 2) ** 2
    + Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.sin(dLon / 2) ** 2
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  return EARTH_RADIUS_METERS * c
}
