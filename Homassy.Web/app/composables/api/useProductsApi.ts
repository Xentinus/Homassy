/**
 * Products API composable
 * Provides product-related API calls
 */
import { isExpiringWithinTwoWeeks } from '../useExpirationCheck'
import type { PagedResult } from '~/types/common'
import type {
  ProductInfo,
  DetailedProductInfo,
  CreateProductRequest,
  UpdateProductRequest,
  CreateMultipleProductsRequest,
  UploadProductImageRequest,
  InventoryItemInfo,
  CreateInventoryItemRequest,
  UpdateInventoryItemRequest,
  QuickAddInventoryItemRequest,
  QuickAddMultipleInventoryItemsRequest,
  ConsumeInventoryItemRequest,
  ConsumeMultipleInventoryItemsRequest,
  MoveInventoryItemsRequest,
  DeleteMultipleInventoryItemsRequest,
  SplitInventoryItemRequest,
  SplitInventoryItemResponse,
  ExpirationCountResponse
} from '~/types/product'

export const useProductsApi = () => {
  const client = useApiClient()
  const $i18n = useI18n()
  const eventBus = useEventBus()

  /**
   * Get all products with pagination
   */
  const getProducts = async (params?: {
    pageNumber?: number
    pageSize?: number
    returnAll?: boolean
    searchText?: string
    sortBy?: string
  }) => {
    const queryParams = new URLSearchParams()
    if (params?.pageNumber) queryParams.append('PageNumber', params.pageNumber.toString())
    if (params?.pageSize) queryParams.append('PageSize', params.pageSize.toString())
    if (params?.returnAll) queryParams.append('ReturnAll', params.returnAll.toString())
    if (params?.searchText) queryParams.append('SearchText', params.searchText)
    if (params?.sortBy) queryParams.append('SortBy', params.sortBy)

    return await client.get<PagedResult<ProductInfo>>(
      `/api/v1/Product?${queryParams.toString()}`
    )
  }

  /**
   * Get detailed product information with inventory
   */
  const getDetailedProducts = async (params?: {
    pageNumber?: number
    pageSize?: number
    searchText?: string
    sortBy?: string
    returnAll?: boolean
  }) => {
    const queryParams = new URLSearchParams()
    if (params?.pageNumber) queryParams.append('PageNumber', params.pageNumber.toString())
    if (params?.pageSize) queryParams.append('PageSize', params.pageSize.toString())
    if (params?.searchText) queryParams.append('SearchText', params.searchText)
    if (params?.sortBy) queryParams.append('SortBy', params.sortBy)
    if (params?.returnAll) queryParams.append('ReturnAll', 'true')

    return await client.get<PagedResult<DetailedProductInfo>>(
      `/api/v1/Product/detailed?${queryParams.toString()}`
    )
  }

  /**
   * Get single product details with inventory
   */
  const getProductDetails = async (productPublicId: string) => {
    return await client.get<DetailedProductInfo>(
      `/api/v1/Product/${productPublicId}/detailed`
    )
  }

  /**
   * Create new product
   */
  const createProduct = async (product: CreateProductRequest) => {
    return await client.post<ProductInfo>(
      '/api/v1/Product',
      product
    )
  }

  /**
   * Update existing product
   */
  const updateProduct = async (productPublicId: string, product: UpdateProductRequest) => {
    return await client.put<ProductInfo>(
      `/api/v1/Product/${productPublicId}`,
      product
    )
  }

  /**
   * Delete product
   */
  const deleteProduct = async (productPublicId: string) => {
    const result = await client.delete(
      `/api/v1/Product/${productPublicId}`
    )
    if (result.success) {
      eventBus.emit('product:deleted')
    }
    return result
  }

  /**
   * Toggle product favorite status
   */
  const toggleFavorite = async (productPublicId: string) => {
    return await client.post<ProductInfo>(
      `/api/v1/Product/${productPublicId}/favorite`,
      undefined
    )
  }

  /**
   * Create multiple products
   */
  const createMultipleProducts = async (request: CreateMultipleProductsRequest) => {
    return await client.post<ProductInfo[]>(
      '/api/v1/Product/multiple',
      request
    )
  }

  /**
   * Upload product image (synchronous - legacy)
   */
  const uploadProductImage = async (productPublicId: string, request: UploadProductImageRequest) => {
    return await client.post(
      `/api/v1/Product/${productPublicId}/image`,
      request
    )
  }

  /**
   * Upload product image with progress tracking
   */
  const uploadProductImageWithProgress = async (productPublicId: string, request: UploadProductImageRequest) => {
    return await client.post<{ jobId: string }>(
      `/api/v1/Product/${productPublicId}/image/upload-async`,
      request,
      {
        showSuccessToast: false,
        showErrorToast: false
      }
    )
  }

  /**
   * Delete product image
   */
  const deleteProductImage = async (productPublicId: string) => {
    return await client.delete(
      `/api/v1/Product/${productPublicId}/image`
    )
  }

  // ==================
  // Inventory Methods
  // ==================

  /**
   * Create inventory item
   */
  const createInventoryItem = async (request: CreateInventoryItemRequest) => {
    const result = await client.post<InventoryItemInfo>(
      '/api/v1/Product/inventory',
      request
    )
    if (result.success && request.expirationAt) {
      if (isExpiringWithinTwoWeeks(request.expirationAt)) {
        eventBus.emit('inventory:created')
      }
    }
    return result
  }

  /**
   * Quick add inventory item (minimal info)
   */
  const quickAddInventoryItem = async (request: QuickAddInventoryItemRequest) => {
    const result = await client.post<InventoryItemInfo>(
      '/api/v1/Product/inventory/quick',
      request
    )
    if (result.success) {
      eventBus.emit('inventory:created')
    }
    return result
  }

  /**
   * Update inventory item
   */
  const updateInventoryItem = async (inventoryItemPublicId: string, request: UpdateInventoryItemRequest) => {
    const result = await client.put<InventoryItemInfo>(
      `/api/v1/Product/inventory/${inventoryItemPublicId}`,
      request
    )
    if (result.success && request.expirationAt) {
      if (isExpiringWithinTwoWeeks(request.expirationAt)) {
        eventBus.emit('inventory:updated')
      }
    }
    return result
  }

  /**
   * Delete inventory item
   */
  const deleteInventoryItem = async (inventoryItemPublicId: string) => {
    const result = await client.delete(
      `/api/v1/Product/inventory/${inventoryItemPublicId}`
    )
    if (result.success) {
      eventBus.emit('inventory:deleted')
    }
    return result
  }

  /**
   * Consume inventory item
   */
  const consumeInventoryItem = async (inventoryItemPublicId: string, request: ConsumeInventoryItemRequest) => {
    const result = await client.post<InventoryItemInfo>(
      `/api/v1/Product/inventory/${inventoryItemPublicId}/consume`,
      request
    )
    if (result.success) {
      eventBus.emit('inventory:consumed')
    }
    return result
  }

  /**
   * Split inventory item into two separate items
   */
  const splitInventoryItem = async (inventoryItemPublicId: string, request: SplitInventoryItemRequest) => {
    const result = await client.post<SplitInventoryItemResponse>(
      `/api/v1/Product/inventory/${inventoryItemPublicId}/split`,
      request
    )
    if (result.success) {
      eventBus.emit('inventory:split')
    }
    return result
  }

  /**
   * Get count of expiring and expired inventory items
   */
  const getExpirationCount = async () => {
    return await client.get<ExpirationCountResponse>(
      '/api/v1/Product/inventory/expiration-count'
    )
  }

  /**
   * Quick add multiple inventory items
   */
  const quickAddMultipleInventoryItems = async (request: QuickAddMultipleInventoryItemsRequest) => {
    const result = await client.post<InventoryItemInfo[]>(
      '/api/v1/Product/inventory/quick/multiple',
      request
    )
    if (result.success) {
      eventBus.emit('inventory:created')
    }
    return result
  }

  /**
   * Move inventory items to different storage location
   */
  const moveInventoryItems = async (request: MoveInventoryItemsRequest) => {
    const result = await client.post<InventoryItemInfo[]>(
      '/api/v1/Product/inventory/move',
      request
    )
    if (result.success) {
      eventBus.emit('inventory:moved')
    }
    return result
  }

  /**
   * Delete multiple inventory items
   */
  const deleteMultipleInventoryItems = async (request: DeleteMultipleInventoryItemsRequest) => {
    const result = await client.delete(
      '/api/v1/Product/inventory/multiple',
      request
    )
    if (result.success) {
      eventBus.emit('inventory:deleted')
    }
    return result
  }

  /**
   * Consume multiple inventory items
   */
  const consumeMultipleInventoryItems = async (request: ConsumeMultipleInventoryItemsRequest) => {
    const result = await client.post<InventoryItemInfo[]>(
      '/api/v1/Product/inventory/consume/multiple',
      request
    )
    if (result.success) {
      eventBus.emit('inventory:consumed')
    }
    return result
  }

  return {
    getProducts,
    getDetailedProducts,
    getProductDetails,
    createProduct,
    updateProduct,
    deleteProduct,
    toggleFavorite,
    createMultipleProducts,
    uploadProductImage,
    uploadProductImageWithProgress,
    deleteProductImage,
    // Inventory methods
    createInventoryItem,
    quickAddInventoryItem,
    updateInventoryItem,
    deleteInventoryItem,
    consumeInventoryItem,
    splitInventoryItem,
    getExpirationCount,
    quickAddMultipleInventoryItems,
    moveInventoryItems,
    deleteMultipleInventoryItems,
    consumeMultipleInventoryItems
  }
}
