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
  QuickPurchaseMultipleShoppingListItemsRequest,
  DeadlineCountResponse
} from '~/types/shoppingList'

export const useShoppingListApi = () => {
  const client = useApiClient()
  const $i18n = useI18n()
  const { emit } = useEventBus()
  const { isExpiringWithinTwoWeeks } = useExpirationCheck()

  /**
   * Get all shopping lists with pagination
   */
  const getShoppingLists = async (params?: {
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
    const result = await client.post<ShoppingListItemInfo>(
      '/api/v1/ShoppingList/item',
      item,
      {
        showSuccessToast: true,
        successMessage: $i18n.t('toast.shoppingItemAdded')
      }
    )

    // Emit event if deadline or due date is within 2 weeks
    const hasDeadline = item.deadlineAt && isExpiringWithinTwoWeeks(item.deadlineAt)
    const hasDueDate = item.dueAt && isExpiringWithinTwoWeeks(item.dueAt)
    if (hasDeadline || hasDueDate) {
      emit('shopping-list-item:created')
    }

    return result
  }

  /**
   * Update shopping list item
   */
  const updateShoppingListItem = async (publicId: string, item: UpdateShoppingListItemRequest) => {
    const result = await client.put<ShoppingListItemInfo>(
      `/api/v1/ShoppingList/item/${publicId}`,
      item,
      {
        showSuccessToast: true,
        successMessage: $i18n.t('toast.shoppingItemUpdated')
      }
    )

    // Emit event if deadline or due date is within 2 weeks
    const hasDeadline = item.deadlineAt && isExpiringWithinTwoWeeks(item.deadlineAt)
    const hasDueDate = item.dueAt && isExpiringWithinTwoWeeks(item.dueAt)
    if (hasDeadline || hasDueDate) {
      emit('shopping-list-item:updated')
    }

    return result
  }

  /**
   * Delete shopping list item
   */
  const deleteShoppingListItem = async (publicId: string) => {
    const result = await client.delete(
      `/api/v1/ShoppingList/item/${publicId}`,
      {
        showSuccessToast: true,
        successMessage: $i18n.t('toast.shoppingItemDeleted')
      }
    )

    emit('shopping-list-item:deleted')
    return result
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
    const result = await client.get<ShoppingListItemInfo>(
      `/api/v1/ShoppingList/item/${publicId}/quick-purchase`
    )

    emit('shopping-list-item:purchased')
    return result
  }

  /**
   * Restore purchase of shopping list item (removes purchase date)
   */
  const restorePurchaseShoppingListItem = async (publicId: string) => {
    const result = await client.get<ShoppingListItemInfo>(
      `/api/v1/ShoppingList/item/${publicId}/restore-purchase`
    )

    // Emit event if deadline or due date is within 2 weeks
    const hasDeadline = result.deadlineAt && isExpiringWithinTwoWeeks(result.deadlineAt)
    const hasDueDate = result.dueAt && isExpiringWithinTwoWeeks(result.dueAt)
    if (hasDeadline || hasDueDate) {
      emit('shopping-list-item:restored')
    }

    return result
  }

  /**
   * Get count of overdue and due soon shopping list items
   */
  const getDeadlineCount = async () => {
    return await client.get<DeadlineCountResponse>(
      '/api/v1/ShoppingList/item/deadline-count'
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
    restorePurchaseShoppingListItem,
    getDeadlineCount
  }
}
