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
  unit: Unit
  barcode?: string
  productPictureBase64?: string
  isEatable: boolean
  isFavorite: boolean
}

export interface DetailedProductInfo extends ProductInfo {
  inventoryItems: InventoryItemInfo[]
}

// ===================
// Realtime (SignalR) — lightweight grid projections + event payloads
// ===================

/** Lightweight inventory item carrying only what a Készletek grid card needs. */
export interface InventoryGridItemInfo {
  publicId: string
  productPublicId: string
  currentQuantity: number
  unit: Unit
  expirationAt?: string
  isSharedWithFamily: boolean
}

/** Lightweight product for the grid: card fields + its in-scope inventory items. */
export interface InventoryGridProductInfo {
  publicId: string
  name: string
  brand: string
  barcode?: string
  isEatable: boolean
  isFavorite: boolean
  inventoryItems: InventoryGridItemInfo[]
}

/** `InventoryUpserted` — the product carrier (items usually empty) plus the affected item. */
export interface InventoryUpsertedEvent {
  product: InventoryGridProductInfo
  item: InventoryGridItemInfo
}

/** `InventoryDeleted` — an item was removed / fully consumed. */
export interface InventoryDeletedEvent {
  productPublicId: string
  itemPublicId: string
}

/** `ProductDeleted` — the product itself was removed. */
export interface ProductDeletedEvent {
  publicId: string
}

/** `ProductFavoriteChanged` — per-user favorite toggle. */
export interface ProductFavoriteChangedEvent {
  publicId: string
  isFavorite: boolean
}

export interface CreateProductRequest {
  name: string
  brand: string
  category?: string | null
  unit: Unit
  barcode?: string | null
  isEatable?: boolean
  notes?: string | null
  isFavorite?: boolean
}

export interface UpdateProductRequest {
  name?: string
  brand?: string
  category?: string
  unit?: Unit
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
  isSharedWithFamily: boolean
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
  expirationAt?: string
  price?: number
  currency?: Currency
  shoppingLocationPublicId?: string
  receiptNumber?: string
}

export interface QuickAddInventoryItemRequest {
  productPublicId: string
  quantity: number
  isSharedWithFamily?: boolean
}

export interface QuickAddMultipleInventoryItemEntry {
  productPublicId: string
  quantity: number
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

// ===================
// Product history (global timeline across all inventory items)
// ===================

export enum ProductHistoryEventType {
  Purchased = 0,
  Added = 1,
  Consumed = 2,
  Updated = 3,
  Deleted = 4
}

export interface ProductHistoryEventInfo {
  eventId: string
  type: ProductHistoryEventType
  date: string
  quantity?: number
  remainingQuantity?: number
  unit?: Unit
  price?: number
  currency?: Currency
  userName?: string
  location?: LocationInfo
  inventoryItemPublicId: string
}
