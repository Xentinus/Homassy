/**
 * Shopping Lists API composable
 * Provides shopping list-related API calls
 */
import type {
  ShoppingListInfo,
  DetailedShoppingListInfo,
  ShoppingListItemInfo,
  CreateShoppingListRequest,
  UpdateShoppingListRequest,
  CreateShoppingListItemRequest,
  UpdateShoppingListItemRequest,
  CreateMultipleShoppingListItemsRequest,
  DeleteMultipleShoppingListItemsRequest,
  QuickPurchaseFromShoppingListItemRequest,
  QuickPurchaseMultipleShoppingListItemsRequest,
  PagedResult
} from '~/types/api'

export const useShoppingListApi = () => {
  const client = useApiClient()

  /**
   * Get all shopping lists with pagination
   */
  const getShoppingLists = async (params?: {
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

    return await client.get<PagedResult<ShoppingListInfo>>(
      `/api/v1/ShoppingList?${queryParams.toString()}`
    )
  }

  /**
   * Get shopping list details with items
   */
  const getShoppingListDetails = async (publicId: string, includeCompleted?: boolean) => {
    const queryParams = new URLSearchParams()
    if (includeCompleted !== undefined) {
      queryParams.append('includeCompleted', includeCompleted.toString())
    }

    return await client.get<DetailedShoppingListInfo>(
      `/api/v1/ShoppingList/${publicId}?${queryParams.toString()}`
    )
  }

  /**
   * Create new shopping list
   */
  const createShoppingList = async (list: CreateShoppingListRequest) => {
    return await client.post<ShoppingListInfo>(
      '/api/v1/ShoppingList',
      list,
      {
        showSuccessToast: true,
        successMessage: 'Shopping list created successfully'
      }
    )
  }

  /**
   * Update shopping list
   */
  const updateShoppingList = async (publicId: string, list: UpdateShoppingListRequest) => {
    return await client.put<ShoppingListInfo>(
      `/api/v1/ShoppingList/${publicId}`,
      list,
      {
        showSuccessToast: true,
        successMessage: 'Shopping list updated successfully'
      }
    )
  }

  /**
   * Delete shopping list
   */
  const deleteShoppingList = async (publicId: string) => {
    return await client.delete(
      `/api/v1/ShoppingList/${publicId}`,
      {
        showSuccessToast: true,
        successMessage: 'Shopping list deleted successfully'
      }
    )
  }

  /**
   * Create shopping list item
   */
  const createShoppingListItem = async (item: CreateShoppingListItemRequest) => {
    return await client.post<ShoppingListItemInfo>(
      '/api/v1/ShoppingList/item',
      item,
      {
        showSuccessToast: true,
        successMessage: 'Item added successfully'
      }
    )
  }

  /**
   * Update shopping list item
   */
  const updateShoppingListItem = async (publicId: string, item: UpdateShoppingListItemRequest) => {
    return await client.put<ShoppingListItemInfo>(
      `/api/v1/ShoppingList/item/${publicId}`,
      item,
      {
        showSuccessToast: true,
        successMessage: 'Item updated successfully'
      }
    )
  }

  /**
   * Delete shopping list item
   */
  const deleteShoppingListItem = async (publicId: string) => {
    return await client.delete(
      `/api/v1/ShoppingList/item/${publicId}`,
      {
        showSuccessToast: true,
        successMessage: 'Item deleted successfully'
      }
    )
  }

  /**
   * Create multiple shopping list items
   */
  const createMultipleShoppingListItems = async (request: CreateMultipleShoppingListItemsRequest) => {
    return await client.post<ShoppingListItemInfo[]>(
      '/api/v1/ShoppingList/item/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Items added successfully'
      }
    )
  }

  /**
   * Delete multiple shopping list items
   */
  const deleteMultipleShoppingListItems = async (request: DeleteMultipleShoppingListItemsRequest) => {
    return await client.post(
      '/api/v1/ShoppingList/item/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Items deleted successfully'
      }
    )
  }

  /**
   * Quick purchase from shopping list item
   */
  const quickPurchaseItem = async (request: QuickPurchaseFromShoppingListItemRequest) => {
    return await client.post(
      '/api/v1/ShoppingList/item/quick-purchase',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Item purchased and added to inventory'
      }
    )
  }

  /**
   * Quick purchase multiple shopping list items
   */
  const quickPurchaseMultipleItems = async (request: QuickPurchaseMultipleShoppingListItemsRequest) => {
    return await client.post(
      '/api/v1/ShoppingList/item/quick-purchase/multiple',
      request,
      {
        showSuccessToast: true,
        successMessage: 'Items purchased and added to inventory'
      }
    )
  }

  return {
    getShoppingLists,
    getShoppingListDetails,
    createShoppingList,
    updateShoppingList,
    deleteShoppingList,
    createShoppingListItem,
    updateShoppingListItem,
    deleteShoppingListItem,
    createMultipleShoppingListItems,
    deleteMultipleShoppingListItems,
    quickPurchaseItem,
    quickPurchaseMultipleItems
  }
}
