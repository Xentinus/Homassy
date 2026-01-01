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
  unit: string
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
  unit: Unit
  note?: string
  deadlineAt?: string
  dueAt?: string
}

export interface UpdateShoppingListItemRequest {
  productPublicId?: string
  shoppingLocationPublicId?: string
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
  unit: Unit
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
