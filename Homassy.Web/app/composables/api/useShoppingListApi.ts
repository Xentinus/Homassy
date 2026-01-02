/**
 * Shopping Lists API composable
 * Provides shopping list-related API calls
 */
import type { PagedResult } from '~/types/common'
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
  QuickPurchaseMultipleShoppingListItemsRequest
} from '~/types/shoppingList'

export const useShoppingListApi = () => {
  const client = useApiClient()
  const $i18n = useI18n()

  /**
   * Get all shopping lists with pagination
   */
  const getShoppingLists = async (params?: {
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

    return await client.get<PagedResult<ShoppingListInfo>>(
      `/api/v1/ShoppingList?${queryParams.toString()}`
    )
  }

  /**
   * Get shopping list details with items
   */
  const getShoppingListDetails = async (publicId: string, showPurchased?: boolean) => {
    const queryParams = new URLSearchParams()
    if (showPurchased !== undefined) {
      queryParams.append('showPurchased', showPurchased.toString())
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
        successMessage: $i18n.t('toast.shoppingListCreated')
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
        successMessage: $i18n.t('toast.shoppingListUpdated')
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
        successMessage: $i18n.t('toast.shoppingListDeleted')
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
        successMessage: $i18n.t('toast.shoppingItemAdded')
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
        successMessage: $i18n.t('toast.shoppingItemUpdated')
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
        successMessage: $i18n.t('toast.shoppingItemDeleted')
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
        successMessage: $i18n.t('toast.shoppingItemsAdded')
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
        successMessage: $i18n.t('toast.shoppingItemsDeleted')
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
        successMessage: $i18n.t('toast.itemPurchased')
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
        successMessage: $i18n.t('toast.itemsPurchased')
      }
    )
  }

  /**
   * Quick purchase shopping list item (simple - just marks as purchased)
   */
  const quickPurchaseShoppingListItem = async (publicId: string) => {
    return await client.get<ShoppingListItemInfo>(
      `/api/v1/ShoppingList/item/${publicId}/quick-purchase`
    )
  }

  /**
   * Restore purchase of shopping list item (removes purchase date)
   */
  const restorePurchaseShoppingListItem = async (publicId: string) => {
    return await client.get<ShoppingListItemInfo>(
      `/api/v1/ShoppingList/item/${publicId}/restore-purchase`
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
    quickPurchaseMultipleItems,
    quickPurchaseShoppingListItem,
    restorePurchaseShoppingListItem
  }
}
