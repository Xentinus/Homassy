/**
 * TypeScript type definitions for Homassy API
 * Generated from OpenAPI specification
 */

// ===================
// Common Types
// ===================

export interface ApiResponse<T = unknown> {
  success: boolean
  data?: T
  errorCodes?: string[]
  timestamp: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
  isUnpaginated: boolean
}

export interface SelectValue {
  publicId: string
  text: string
}

export interface ErrorCodeInfo {
  code: string
  description: string
}

// ===================
// Enums
// ===================

export enum Unit {
  Piece = 0,
  Gram = 1,
  Kilogram = 2,
  Milligram = 3,
  Milliliter = 10,
  Centiliter = 11,
  Deciliter = 12,
  Liter = 13,
  Meter = 20,
  Centimeter = 21,
  Millimeter = 22,
  SquareMeter = 30,
  CubicMeter = 31,
  Teaspoon = 40,
  Tablespoon = 41,
  Cup = 42,
  Pack = 50,
  Box = 51,
  Bottle = 52,
  Can = 53,
  Jar = 54,
  Bag = 55
}

export enum Currency {
  Huf = 135, // Hungarian Forint
  Eur = 105, // Euro
  Usd = 279 // US Dollar
}

export enum Language {
  Hungarian = 0,
  German = 1,
  English = 2
}

export enum UserTimeZone {
  CentralEuropean = 0
}

export enum ImageFormat {
  Jpeg = 0,
  Png = 1,
  WebP = 2
}

export enum SelectValueType {
  Units = 0,
  Currencies = 1,
  TimeZones = 2,
  Languages = 3
}

// ===================
// Authentication
// ===================

export interface LoginRequest {
  email: string
}

export interface VerifyLoginRequest {
  email: string
  code: string
}

export interface CreateUserRequest {
  email: string
  name: string
  displayName?: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
  user: UserInfo
}

export interface RefreshTokenRequest {
  accessToken: string
  refreshToken: string
}

export interface RefreshTokenResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  refreshTokenExpiresAt: string
}

export interface UserInfo {
  publicId: string
  email: string
  name: string
  displayName?: string
  profilePictureBase64?: string
  language: string
  currency: string
  timeZone: string
  createdAt: string
}

// ===================
// User
// ===================

export interface UserProfileResponse {
  publicId: string
  email: string
  name: string
  displayName?: string
  profilePictureBase64?: string
  language: string
  currency: string
  timeZone: string
  emailVerifiedAt?: string
  createdAt: string
  updatedAt: string
  family?: FamilyInfo
}

export interface UpdateUserSettingsRequest {
  name?: string
  displayName?: string
  defaultLanguage?: Language
  defaultCurrency?: Currency
  defaultTimeZone?: UserTimeZone
}

export interface UploadUserProfileImageRequest {
  imageBase64: string
}

export interface UserProfileImageInfo {
  userPublicId: string
  imageBase64: string
  format: ImageFormat
  width: number
  height: number
  fileSizeBytes: number
}

// ===================
// Family
// ===================

export interface FamilyInfo {
  name: string
  shareCode: string
}

export interface FamilyDetailsResponse {
  name: string
  description?: string
  shareCode: string
  familyPictureBase64?: string
}

export interface CreateFamilyRequest {
  name: string
  description?: string
  familyPictureBase64?: string
}

export interface UpdateFamilyRequest {
  name?: string
  description?: string
}

export interface JoinFamilyRequest {
  shareCode: string
}

export interface UploadFamilyPictureRequest {
  familyPictureBase64: string
}

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
  category?: string
  barcode?: string
  isEatable?: boolean
  notes?: string
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

export interface InventoryItemInfo {
  publicId: string
  currentQuantity: number
  unit: Unit
  expirationAt?: string
  purchaseInfo?: PurchaseInfo
  consumptionLogs: ConsumptionLogInfo[]
}

export interface PurchaseInfo {
  publicId: string
  purchasedAt: string
  originalQuantity: number
  price?: number
  currency?: Currency
  shoppingLocationId?: string
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
// Shopping Lists
// ===================

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

// ===================
// Locations
// ===================

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

// ===================
// Open Food Facts
// ===================

export interface OpenFoodFactsProduct {
  code: string
  product_name: string
  product_name_hu: string
  brands: string
  categories: string
  categories_tags: string[]
  quantity: string
  image_url: string
  image_small_url: string
  image_front_url: string
  nutrition_grades: string
  nutriscore_grade: string
  ecoscore_grade: string
  nova_group: number
  ingredients_text: string
  ingredients_text_hu: string
  allergens: string
  allergens_tags: string[]
  countries: string
  countries_tags: string[]
  stores: string
  nutriments: OpenFoodFactsNutriments
  image_base64?: string
}

export interface OpenFoodFactsNutriments {
  'energy-kcal_100g': number
  'energy-kj_100g': number
  proteins_100g: number
  carbohydrates_100g: number
  sugars_100g: number
  fat_100g: number
  'saturated-fat_100g': number
  fiber_100g: number
  salt_100g: number
  sodium_100g: number
}

// ===================
// Health
// ===================

export interface HealthCheckResponse {
  status: string
  duration: string
  dependencies: Record<string, DependencyHealth>
}

export interface DependencyHealth {
  status: string
  duration: string
  description?: string
  data?: Record<string, unknown>
}

// ===================
// Version
// ===================

export interface VersionInfo {
  version: string
  environment: string
  buildDate: string
}
