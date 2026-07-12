/**
 * Location related types (Storage and Shopping)
 */
import type { Unit, Currency } from './enums'

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

/** A product currently in stock at a given storage location (from the storage inventory endpoint). */
export interface StorageLocationInventoryItemInfo {
  publicId: string
  productPublicId: string
  productName: string
  productBrand: string
  currentQuantity: number
  unit: Unit
  expirationAt?: string
  isSharedWithFamily: boolean
}

/** A past purchase made at a given shopping location (from the shopping purchases endpoint). */
export interface ShoppingLocationPurchaseInfo {
  publicId: string
  productPublicId: string
  productName: string
  productBrand: string
  quantity: number
  unit?: Unit
  price?: number
  currency?: Currency
  purchasedAt: string
}
