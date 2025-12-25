/**
 * English error code translations
 * Based on ErrorCodeDescriptions.cs from Homassy.API
 */
export const errorCodeMessages: Record<string, string> = {
  // Authentication Errors (AUTH-0xxx)
  'AUTH-0001': 'Authentication is required but was not provided.',
  'AUTH-0002': 'Invalid email address or verification code.',
  'AUTH-0003': 'The verification code has expired.',
  'AUTH-0004': 'The account is temporarily locked due to too many failed login attempts.',
  'AUTH-0005': 'Access to the requested resource is denied.',
  'AUTH-0006': 'Invalid or expired refresh token.',
  'AUTH-0007': 'Possible token theft detected. All sessions have been invalidated.',
  'AUTH-0008': 'Registration is currently disabled.',

  // User Errors (USER-0xxx)
  'USER-0001': 'User not found.',
  'USER-0002': 'User profile not found.',
  'USER-0003': 'User credentials not found.',
  'USER-0004': 'This email address is already in use.',
  'USER-0005': 'The user has no profile picture.',

  // Family Errors (FAMILY-0xxx)
  'FAMILY-0001': 'Family not found.',
  'FAMILY-0002': 'You are already a member of this family.',
  'FAMILY-0003': 'You are not a member of this family.',
  'FAMILY-0004': 'Invalid share code.',

  // Product Errors (PRODUCT-0xxx)
  'PRODUCT-0001': 'Product not found.',
  'PRODUCT-0002': 'Access to this product is denied.',
  'PRODUCT-0003': 'Inventory item not found.',

  // Location Errors (LOCATION-0xxx)
  'LOCATION-0001': 'Location not found.',
  'LOCATION-0002': 'Shopping location not found.',
  'LOCATION-0003': 'Storage location not found.',
  'LOCATION-0004': 'Access to this location is denied.',

  // Shopping List Errors (SHOPPINGLIST-0xxx)
  'SHOPPINGLIST-0001': 'Shopping list not found.',
  'SHOPPINGLIST-0002': 'Shopping list item not found.',
  'SHOPPINGLIST-0003': 'Access to this shopping list is denied.',
  'SHOPPINGLIST-0004': 'Invalid shopping list data.',

  // Validation Errors (VALIDATION-0xxx)
  'VALIDATION-0001': 'Invalid request data.',
  'VALIDATION-0002': 'A required field is missing.',
  'VALIDATION-0003': 'Invalid email address format.',
  'VALIDATION-0004': 'Input contains potentially dangerous content.',
  'VALIDATION-0005': 'Invalid barcode format.',
  'VALIDATION-0006': 'Share code is required.',
  'VALIDATION-0007': 'Name is required.',
  'VALIDATION-0008': 'Email address cannot be empty.',
  'VALIDATION-0009': 'Verification code cannot be empty.',
  'VALIDATION-0010': 'The verification code must be 6 digits.',
  'VALIDATION-0011': 'Barcode is required.',

  // Image Processing Errors (IMAGE-0xxx)
  'IMAGE-0001': 'The image is empty.',
  'IMAGE-0002': 'Invalid Base64 image.',
  'IMAGE-0003': 'Invalid image format.',
  'IMAGE-0004': 'The image file is too large.',
  'IMAGE-0005': 'The image dimensions are too small.',
  'IMAGE-0006': 'The image dimensions are too large.',
  'IMAGE-0007': 'Unsupported image format.',
  'IMAGE-0008': 'The image appears corrupted or malicious.',
  'IMAGE-0009': 'Image processing failed.',

  // External API Errors (EXTERNAL-0xxx)
  'EXTERNAL-0001': 'The product was not found in the Open Food Facts database.',

  // Rate Limiting Errors (RATELIMIT-0xxx)
  'RATELIMIT-0001': 'Rate limit exceeded. Too many requests.',

  // System Errors (SYSTEM-0xxx)
  'SYSTEM-0001': 'An unexpected error occurred.',
  'SYSTEM-0002': 'The request timed out.',
  'SYSTEM-0003': 'The request was aborted by the client.',
  'SYSTEM-0004': 'Unauthorized access.',
}

/**
 * Get English error message for error code
 * @param errorCode - The error code (e.g., "AUTH-0001")
 * @returns English error message or generic message if code not found
 */
export function getErrorMessage(errorCode: string): string {
  return errorCodeMessages[errorCode] || `Unknown error: ${errorCode}`
}

/**
 * Get English error messages for multiple error codes
 * @param errorCodes - Array of error codes
 * @returns Array of English error messages
 */
export function getErrorMessages(errorCodes: string[]): string[] {
  return errorCodes.map(code => getErrorMessage(code))
}
