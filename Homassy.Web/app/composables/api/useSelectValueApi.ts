/**
 * Select Value API composable
 * Provides select value related API calls
 */
import type { ApiResponse } from '~/types/common'
import type { SelectValue, SelectValueType } from '~/types/selectValue'

export const useSelectValueApi = () => {
  const { $api } = useNuxtApp()

  /**
   * Get select values for a specific type
   * @param type - SelectValueType enum (units, currencies, timezones, etc.)
   */
  const getSelectValues = async (type: SelectValueType): Promise<ApiResponse<SelectValue[]>> => {
    return await $api<ApiResponse<SelectValue[]>>(`/api/v1.0/SelectValue/${type}`, {
      method: 'GET'
    })
  }

  return {
    getSelectValues
  }
}
