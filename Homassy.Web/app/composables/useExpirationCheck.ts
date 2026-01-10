/**
 * Expiration date checking utilities
 * Provides functions to check if dates are expired or expiring soon
 */

/**
 * Check if a date is expired (today or earlier)
 * @param expirationDate - The expiration date to check (ISO string or Date object)
 * @returns true if the date is today or in the past
 */
export const isExpired = (expirationDate: string | Date | null | undefined): boolean => {
  if (!expirationDate) return false
  
  const today = new Date()
  today.setHours(0, 0, 0, 0) // Reset to start of day
  
  const expDate = new Date(expirationDate)
  expDate.setHours(0, 0, 0, 0) // Reset to start of day
  
  return expDate < today
}

/**
 * Check if a date is within 14 days (including expired dates)
 * @param expirationDate - The expiration date to check (ISO string or Date object)
 * @returns true if the date is within 14 days from today (including past dates)
 */
export const isExpiringWithinTwoWeeks = (expirationDate: string | Date | null | undefined): boolean => {
  if (!expirationDate) return false
  
  const today = new Date()
  today.setHours(0, 0, 0, 0) // Reset to start of day
  
  const twoWeeksFromNow = new Date(today)
  twoWeeksFromNow.setDate(today.getDate() + 14)
  
  const expDate = new Date(expirationDate)
  expDate.setHours(0, 0, 0, 0) // Reset to start of day
  
  return expDate <= twoWeeksFromNow
}

/**
 * Check if a date is expiring soon (within 14 days but not yet expired)
 * @param expirationDate - The expiration date to check (ISO string or Date object)
 * @returns true if the date is between today and 14 days from now
 */
export const isExpiringSoon = (expirationDate: string | Date | null | undefined): boolean => {
  if (!expirationDate) return false
  
  return !isExpired(expirationDate) && isExpiringWithinTwoWeeks(expirationDate)
}

/**
 * Composable that provides expiration checking functions
 * @returns Object with expiration checking functions
 */
export const useExpirationCheck = () => {
  return {
    isExpired,
    isExpiringWithinTwoWeeks,
    isExpiringSoon
  }
}
