/**
 * Products API composable
 * Provides product-related API calls
 */
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
  DeleteMultipleInventoryItemsRequest
} from '~/types/product'

export const useProductsApi = () => {
  const client = useApiClient()

  /**
   * Get all products with pagination
   */
  const getProducts = async (params?: {
    pageNumber?: number
    pageSize?: number
    searchText?: string
    sortBy?: string
  }) => {
    const queryParams = new URLSearchParams()
    if (params?.pageNumber) queryParams.append('PageNumber', params.pageNumber.toString())
    if (params?.pageSize) queryParams.append('PageSize', params.pageSize.toString())
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
      product,
      {
        showSuccessToast: true,
        successMessage: 'Product created successfully'
      }
    )
  }

  /**
   * Update existing product
   */
  const updateProduct = async (productPublicId: string, product: UpdateProductRequest) => {
    return await client.put<ProductInfo>(
      `/api/v1/Product/${productPublicId}`,
      product,
      {
        showSuccessToast: true,
        successMessage: 'Product updated successfully'
      }
    )
  }

  /**
   * Delete product
   */
  const deleteProduct = async (productPublicId: string) => {
    return await client.delete(
      `/api/v1/Product/${productPublicId}`,
      {
        showSuccessToast: true,
        successMessage: 'Product deleted successfully'
      }
    )
  }

  /**
   * Toggle product favorite status
   */
  const toggleFavorite = async (productPublicId: string) => {
    return await client.post<ProductInfo>(
      `/api/v1/Product/${productPublicId}/favorite`,
      undefined,
      {
        showSuccessToast: true,
        successMessage: 'Favorite state updated'
      }
    )
  }

  /**
   * Create multiple products
   */
  const createMultipleProducts = async (request: CreateMultipleProductsRequest) => {
    return await client.post<ProductInfo[]>(
      '/api/v1/Product/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Products created successfully'
      }
    )
  }

  /**
   * Upload product image
   */
  const uploadProductImage = async (productPublicId: string, request: UploadProductImageRequest) => {
    return await client.post(
      `/api/v1/Product/${productPublicId}/image`,
      request,
      {
        showSuccessToast: true,
        successMessage: 'Image uploaded successfully'
      }
    )
  }

  /**
   * Delete product image
   */
  const deleteProductImage = async (productPublicId: string) => {
    return await client.delete(
      `/api/v1/Product/${productPublicId}/image`,
      {
        showSuccessToast: true,
        successMessage: 'Image deleted successfully'
      }
    )
  }

  // ==================
  // Inventory Methods
  // ==================

  /**
   * Create inventory item
   */
  const createInventoryItem = async (request: CreateInventoryItemRequest) => {
    return await client.post<InventoryItemInfo>(
      '/api/v1/Product/inventory',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Inventory item created successfully'
      }
    )
  }

  /**
   * Quick add inventory item (minimal info)
   */
  const quickAddInventoryItem = async (request: QuickAddInventoryItemRequest) => {
    return await client.post<InventoryItemInfo>(
      '/api/v1/Product/inventory/quick',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Item added to inventory'
      }
    )
  }

  /**
   * Update inventory item
   */
  const updateInventoryItem = async (inventoryItemPublicId: string, request: UpdateInventoryItemRequest) => {
    return await client.put<InventoryItemInfo>(
      `/api/v1/Product/inventory/${inventoryItemPublicId}`,
      request,
      {
        showSuccessToast: true,
        successMessage: 'Inventory item updated successfully'
      }
    )
  }

  /**
   * Delete inventory item
   */
  const deleteInventoryItem = async (inventoryItemPublicId: string) => {
    return await client.delete(
      `/api/v1/Product/inventory/${inventoryItemPublicId}`,
      {
        showSuccessToast: true,
        successMessage: 'Inventory item deleted successfully'
      }
    )
  }

  /**
   * Consume inventory item
   */
  const consumeInventoryItem = async (inventoryItemPublicId: string, request: ConsumeInventoryItemRequest) => {
    return await client.post<InventoryItemInfo>(
      `/api/v1/Product/inventory/${inventoryItemPublicId}/consume`,
      request,
      {
        showSuccessToast: true,
        successMessage: 'Consumption recorded successfully'
      }
    )
  }

  /**
   * Quick add multiple inventory items
   */
  const quickAddMultipleInventoryItems = async (request: QuickAddMultipleInventoryItemsRequest) => {
    return await client.post<InventoryItemInfo[]>(
      '/api/v1/Product/inventory/quick/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Items added to inventory'
      }
    )
  }

  /**
   * Move inventory items to different storage location
   */
  const moveInventoryItems = async (request: MoveInventoryItemsRequest) => {
    return await client.post<InventoryItemInfo[]>(
      '/api/v1/Product/inventory/move',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Items moved successfully'
      }
    )
  }

  /**
   * Delete multiple inventory items
   */
  const deleteMultipleInventoryItems = async (request: DeleteMultipleInventoryItemsRequest) => {
    return await client.delete(
      '/api/v1/Product/inventory/multiple',
      {
        showSuccessToast: true,
        successMessage: 'Items deleted successfully',
        body: request
      }
    )
  }

  /**
   * Consume multiple inventory items
   */
  const consumeMultipleInventoryItems = async (request: ConsumeMultipleInventoryItemsRequest) => {
    return await client.post<InventoryItemInfo[]>(
      '/api/v1/Product/inventory/consume/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Consumption recorded successfully'
      }
    )
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
    deleteProductImage,
    // Inventory methods
    createInventoryItem,
    quickAddInventoryItem,
    updateInventoryItem,
    deleteInventoryItem,
    consumeInventoryItem,
    quickAddMultipleInventoryItems,
    moveInventoryItems,
    deleteMultipleInventoryItems,
    consumeMultipleInventoryItems
  }
}
