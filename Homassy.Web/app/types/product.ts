/**
 * Product and Inventory related types
 */
import type { Unit, Currency, ImageFormat } from './enums'

// ===================
// Products
// ===================

export interface ProductInfo {
  publicId: string
  name: string
  brand: string
  category?: string
  barcode?: string
  productPictureBase64?: string
  isEatable: boolean
  isFavorite: boolean
}

export interface DetailedProductInfo extends ProductInfo {
  inventoryItems: InventoryItemInfo[]
}

export interface CreateProductRequest {
  name: string
  brand: string
  category?: string | null
  barcode?: string | null
  isEatable?: boolean
  notes?: string | null
  isFavorite?: boolean
}

export interface UpdateProductRequest {
  name?: string
  brand?: string
  category?: string
  barcode?: string
  isEatable?: boolean
  notes?: string
}

export interface CreateMultipleProductsRequest {
  products: CreateProductRequest[]
}

export interface UploadProductImageRequest {
  productPublicId: string
  imageBase64: string
}

export interface ProductImageInfo {
  productPublicId: string
  imageBase64: string
  format: ImageFormat
  width: number
  height: number
  fileSizeBytes: number
}

// ===================
// Inventory
// ===================

export interface LocationInfo {
  publicId: string
  name: string
}

export interface InventoryItemInfo {
  publicId: string
  currentQuantity: number
  unit: Unit
  expirationAt?: string
  storageLocation?: LocationInfo
  purchaseInfo?: PurchaseInfo
  consumptionLogs: ConsumptionLogInfo[]
}

export interface PurchaseInfo {
  publicId: string
  purchasedAt: string
  originalQuantity: number
  price?: number
  currency?: Currency
  shoppingLocation?: LocationInfo
}

export interface ConsumptionLogInfo {
  publicId: string
  userName: string
  consumedQuantity: number
  remainingQuantity: number
  consumedAt: string
}

export interface CreateInventoryItemRequest {
  productPublicId: string
  isSharedWithFamily?: boolean
  storageLocationPublicId?: string
  quantity: number
  unit?: Unit
  expirationAt?: string
  price?: number
  currency?: Currency
  shoppingLocationPublicId?: string
  receiptNumber?: string
}

export interface UpdateInventoryItemRequest {
  isSharedWithFamily?: boolean
  storageLocationPublicId?: string
  quantity?: number
  unit?: Unit
  expirationAt?: string
  price?: number
  currency?: Currency
  shoppingLocationPublicId?: string
  receiptNumber?: string
}

export interface QuickAddInventoryItemRequest {
  productPublicId: string
  quantity: number
  unit?: Unit
  isSharedWithFamily?: boolean
}

export interface QuickAddMultipleInventoryItemEntry {
  productPublicId: string
  quantity: number
  unit?: Unit
}

export interface QuickAddMultipleInventoryItemsRequest {
  items: QuickAddMultipleInventoryItemEntry[]
  storageLocationPublicId?: string
  isSharedWithFamily?: boolean
}

export interface ConsumeInventoryItemRequest {
  quantity: number
}

export interface SplitInventoryItemRequest {
  quantity: number
}

export interface SplitInventoryItemResponse {
  originalItem: InventoryItemInfo
  newItem: InventoryItemInfo
}

export interface ExpirationCountResponse {
  totalCount: number
}

export interface ConsumeInventoryItemEntry {
  inventoryItemPublicId: string
  quantity: number
}

export interface ConsumeMultipleInventoryItemsRequest {
  items: ConsumeInventoryItemEntry[]
}

export interface MoveInventoryItemsRequest {
  inventoryItemPublicIds: string[]
  storageLocationPublicId: string
}

export interface DeleteMultipleInventoryItemsRequest {
  itemPublicIds: string[]
}
