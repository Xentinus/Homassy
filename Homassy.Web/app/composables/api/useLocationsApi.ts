/**
 * Locations API composable
 * Provides storage and shopping location-related API calls
 */
import type {
  StorageLocationInfo,
  ShoppingLocationInfo,
  StorageLocationRequest,
  ShoppingLocationRequest,
  CreateMultipleStorageLocationsRequest,
  CreateMultipleShoppingLocationsRequest,
  DeleteMultipleStorageLocationsRequest,
  DeleteMultipleShoppingLocationsRequest,
  PagedResult
} from '~/types/api'

export const useLocationsApi = () => {
  const client = useApiClient()

  // ==================
  // Storage Locations
  // ==================

  /**
   * Get all storage locations with pagination
   */
  const getStorageLocations = async (params?: {
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

    return await client.get<PagedResult<StorageLocationInfo>>(
      `/api/v1/Location/storage?${queryParams.toString()}`
    )
  }

  /**
   * Create new storage location
   */
  const createStorageLocation = async (location: StorageLocationRequest) => {
    return await client.post<StorageLocationInfo>(
      '/api/v1/Location/storage',
      location,
      {
        showSuccessToast: true,
        successMessage: 'Storage location created successfully'
      }
    )
  }

  /**
   * Update storage location
   */
  const updateStorageLocation = async (
    storageLocationPublicId: string,
    location: StorageLocationRequest
  ) => {
    return await client.put<StorageLocationInfo>(
      `/api/v1/Location/storage/${storageLocationPublicId}`,
      location,
      {
        showSuccessToast: true,
        successMessage: 'Storage location updated successfully'
      }
    )
  }

  /**
   * Delete storage location
   */
  const deleteStorageLocation = async (storageLocationPublicId: string) => {
    return await client.delete(
      `/api/v1/Location/storage/${storageLocationPublicId}`,
      {
        showSuccessToast: true,
        successMessage: 'Storage location deleted successfully'
      }
    )
  }

  /**
   * Create multiple storage locations
   */
  const createMultipleStorageLocations = async (request: CreateMultipleStorageLocationsRequest) => {
    return await client.post<StorageLocationInfo[]>(
      '/api/v1/Location/storage/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Storage locations created successfully'
      }
    )
  }

  /**
   * Delete multiple storage locations
   */
  const deleteMultipleStorageLocations = async (request: DeleteMultipleStorageLocationsRequest) => {
    return await client.post(
      '/api/v1/Location/storage/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Storage locations deleted successfully'
      }
    )
  }

  // ==================
  // Shopping Locations
  // ==================

  /**
   * Get all shopping locations with pagination
   */
  const getShoppingLocations = async (params?: {
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

    return await client.get<PagedResult<ShoppingLocationInfo>>(
      `/api/v1/Location/shopping?${queryParams.toString()}`
    )
  }

  /**
   * Create new shopping location
   */
  const createShoppingLocation = async (location: ShoppingLocationRequest) => {
    return await client.post<ShoppingLocationInfo>(
      '/api/v1/Location/shopping',
      location,
      {
        showSuccessToast: true,
        successMessage: 'Shopping location created successfully'
      }
    )
  }

  /**
   * Update shopping location
   */
  const updateShoppingLocation = async (
    shoppingLocationPublicId: string,
    location: ShoppingLocationRequest
  ) => {
    return await client.put<ShoppingLocationInfo>(
      `/api/v1/Location/shopping/${shoppingLocationPublicId}`,
      location,
      {
        showSuccessToast: true,
        successMessage: 'Shopping location updated successfully'
      }
    )
  }

  /**
   * Delete shopping location
   */
  const deleteShoppingLocation = async (shoppingLocationPublicId: string) => {
    return await client.delete(
      `/api/v1/Location/shopping/${shoppingLocationPublicId}`,
      {
        showSuccessToast: true,
        successMessage: 'Shopping location deleted successfully'
      }
    )
  }

  /**
   * Create multiple shopping locations
   */
  const createMultipleShoppingLocations = async (request: CreateMultipleShoppingLocationsRequest) => {
    return await client.post<ShoppingLocationInfo[]>(
      '/api/v1/Location/shopping/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Shopping locations created successfully'
      }
    )
  }

  /**
   * Delete multiple shopping locations
   */
  const deleteMultipleShoppingLocations = async (request: DeleteMultipleShoppingLocationsRequest) => {
    return await client.post(
      '/api/v1/Location/shopping/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Shopping locations deleted successfully'
      }
    )
  }

  return {
    // Storage locations
    getStorageLocations,
    createStorageLocation,
    updateStorageLocation,
    deleteStorageLocation,
    createMultipleStorageLocations,
    deleteMultipleStorageLocations,
    // Shopping locations
    getShoppingLocations,
    createShoppingLocation,
    updateShoppingLocation,
    deleteShoppingLocation,
    createMultipleShoppingLocations,
    deleteMultipleShoppingLocations
  }
}
