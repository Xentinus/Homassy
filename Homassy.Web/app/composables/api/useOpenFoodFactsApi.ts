/**
 * Open Food Facts API composable
 * Provides Open Food Facts product information API calls
 */
import type { ApiResponse } from '~/types/common'
import type { OpenFoodFactsProduct } from '~/types/openFoodFacts'

export const useOpenFoodFactsApi = () => {
  const { $api } = useNuxtApp()

  /**
   * Get product information from Open Food Facts by barcode
   * @param barcode - The product barcode
   */
  const getProductByBarcode = async (barcode: string): Promise<ApiResponse<OpenFoodFactsProduct>> => {
    return await $api<ApiResponse<OpenFoodFactsProduct>>(`/api/v1/OpenFoodFacts/${barcode}`, {
      method: 'GET'
    })
  }

  return {
    getProductByBarcode
  }
}
