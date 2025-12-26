/**
 * Location related types (Storage and Shopping)
 */

export interface StorageLocationInfo {
  publicId: string
  name: string
  description?: string
  color?: string
  isFreezer: boolean
  isSharedWithFamily: boolean
}

export interface ShoppingLocationInfo {
  publicId: string
  name: string
  description?: string
  color?: string
  address?: string
  city?: string
  postalCode?: string
  country?: string
  website?: string
  googleMaps?: string
  isSharedWithFamily: boolean
}

export interface StorageLocationRequest {
  name?: string
  description?: string
  color?: string
  isFreezer?: boolean
  isSharedWithFamily?: boolean
}

export interface ShoppingLocationRequest {
  name?: string
  description?: string
  color?: string
  address?: string
  city?: string
  postalCode?: string
  country?: string
  website?: string
  googleMaps?: string
  isSharedWithFamily?: boolean
}

export interface CreateMultipleStorageLocationsRequest {
  locations: StorageLocationRequest[]
}

export interface CreateMultipleShoppingLocationsRequest {
  locations: ShoppingLocationRequest[]
}

export interface DeleteMultipleStorageLocationsRequest {
  locationPublicIds: string[]
}

export interface DeleteMultipleShoppingLocationsRequest {
  locationPublicIds: string[]
}
