/**
 * Products API composable
 * Provides product-related API calls
 */
import type {
  ProductInfo,
  DetailedProductInfo,
  CreateProductRequest,
  UpdateProductRequest,
  CreateMultipleProductsRequest,
  UploadProductImageRequest,
  PagedResult
} from '~/types/api'

export const useProductsApi = () => {
  const client = useApiClient()

  /**
   * Get all products with pagination
   */
  const getProducts = async (params?: {
    pageNumber?: number
    pageSize?: number
    searchTerm?: string
    sortBy?: string
  }) => {
    const queryParams = new URLSearchParams()
    if (params?.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString())
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString())
    if (params?.searchTerm) queryParams.append('searchTerm', params.searchTerm)
    if (params?.sortBy) queryParams.append('sortBy', params.sortBy)

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
    searchTerm?: string
    sortBy?: string
  }) => {
    const queryParams = new URLSearchParams()
    if (params?.pageNumber) queryParams.append('pageNumber', params.pageNumber.toString())
    if (params?.pageSize) queryParams.append('pageSize', params.pageSize.toString())
    if (params?.searchTerm) queryParams.append('searchTerm', params.searchTerm)
    if (params?.sortBy) queryParams.append('sortBy', params.sortBy)

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
    deleteProductImage
  }
}
