/**
 * Locations API composable
 * Provides storage and shopping location-related API calls
 */
import type { PagedResult } from '~/types/common'
import type {
  StorageLocationInfo,
  ShoppingLocationInfo,
  StorageLocationRequest,
  ShoppingLocationRequest,
  CreateMultipleStorageLocationsRequest,
  CreateMultipleShoppingLocationsRequest,
  DeleteMultipleStorageLocationsRequest,
  DeleteMultipleShoppingLocationsRequest
} from '~/types/location'

export const useLocationsApi = () => {
  const client = useApiClient()
  const $i18n = useI18n()

  // ==================
  // Storage Locations
  // ==================

  /**
   * Get all storage locations with pagination
   */
  const getStorageLocations = async (params?: {
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
      {
        Name: location.name,
        Description: location.description,
        Color: location.color,
        IsFreezer: location.isFreezer,
        IsSharedWithFamily: location.isSharedWithFamily
      },
      {
        showSuccessToast: true,
        successMessage: $i18n.t('toast.storageLocationCreated')
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
      {
        Name: location.name,
        Description: location.description,
        Color: location.color,
        IsFreezer: location.isFreezer,
        IsSharedWithFamily: location.isSharedWithFamily
      },
      {
        showSuccessToast: true,
        successMessage: $i18n.t('toast.storageLocationUpdated')
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
        successMessage: $i18n.t('toast.storageLocationDeleted')
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
        successMessage: $i18n.t('toast.storageLocationsCreated')
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
        successMessage: $i18n.t('toast.storageLocationsDeleted')
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
        successMessage: $i18n.t('toast.shoppingLocationCreated')
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
        successMessage: $i18n.t('toast.shoppingLocationUpdated')
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
        successMessage: $i18n.t('toast.shoppingLocationDeleted')
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
        successMessage: $i18n.t('toast.shoppingLocationsCreated')
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
        successMessage: $i18n.t('toast.shoppingLocationsDeleted')
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
