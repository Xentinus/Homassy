/**
 * Shopping List related types
 */
import type { Unit, Currency } from './enums'
import type { ProductInfo } from './product'
import type { ShoppingLocationInfo } from './location'

export interface ShoppingListInfo {
  publicId: string
  name: string
  description?: string
  color?: string
  isSharedWithFamily: boolean
}

export interface DetailedShoppingListInfo extends ShoppingListInfo {
  items: ShoppingListItemInfo[]
}

export interface ShoppingListItemInfo {
  publicId: string
  shoppingListPublicId: string
  productPublicId?: string
  shoppingLocationPublicId?: string
  product?: ProductInfo
  shoppingLocation?: ShoppingLocationInfo
  customName?: string
  quantity: number
  unit: Unit
  note?: string
  purchasedAt?: string
  deadlineAt?: string
  dueAt?: string
}

export interface CreateShoppingListRequest {
  name: string
  description?: string
  color?: string
  isSharedWithFamily?: boolean
}

export interface UpdateShoppingListRequest {
  name?: string
  description?: string
  color?: string
  isSharedWithFamily?: boolean
}

export interface CreateShoppingListItemRequest {
  shoppingListPublicId: string
  productPublicId?: string
  shoppingLocationPublicId?: string
  customName?: string
  quantity: number
  // Only used for standalone/custom items; product-linked items inherit the product's unit.
  unit?: Unit
  note?: string
  deadlineAt?: string
  dueAt?: string
}

export interface UpdateShoppingListItemRequest {
  productPublicId?: string
  shoppingLocationPublicId?: string
  // Set true to remove the item's shopping location (null publicId means "no change").
  clearShoppingLocation?: boolean
  customName?: string
  quantity?: number
  unit?: Unit
  note?: string
  purchasedAt?: string
  deadlineAt?: string
  dueAt?: string
}

export interface CreateShoppingListItemEntry {
  productPublicId?: string
  shoppingLocationPublicId?: string
  customName?: string
  quantity: number
  // Only used for standalone/custom items; product-linked items inherit the product's unit.
  unit?: Unit
  note?: string
  deadlineAt?: string
  dueAt?: string
}

export interface CreateMultipleShoppingListItemsRequest {
  shoppingListPublicId: string
  items: CreateShoppingListItemEntry[]
}

export interface DeleteMultipleShoppingListItemsRequest {
  itemPublicIds: string[]
}

export interface QuickPurchaseFromShoppingListItemRequest {
  shoppingListItemPublicId: string
  purchasedAt: string
  quantity: number
  price?: number
  currency?: Currency
  storageLocationPublicId?: string
  expirationAt?: string
  isSharedWithFamily?: boolean
}

export interface QuickPurchaseMultipleShoppingListItemsRequest {
  items: QuickPurchaseFromShoppingListItemRequest[]
}

export interface PurchaseShoppingListItemRequest {
  shoppingListItemPublicId: string
  purchasedAt: string
  // How much of the item's quantity was bought. Omit for the whole quantity.
  purchasedQuantity?: number
  // When only part was bought, whether the remainder stays on the list (default true).
  keepRemainder?: boolean
  // Where the item was bought. Omit for "no change".
  shoppingLocationPublicId?: string
  // Set true to remove the item's shopping location (a null publicId means "no change").
  clearShoppingLocation?: boolean
}

export interface DeadlineCountResponse {
  totalCount: number
}
