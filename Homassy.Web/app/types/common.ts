/**
 * Common shared types used across the API
 */

export interface ApiResponse<T = unknown> {
  success: boolean
  data?: T
  errorCodes?: string[]
  timestamp: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
  isUnpaginated: boolean
}

export interface PaginationParams {
  pageNumber?: number
  pageSize?: number
  returnAll?: boolean
  skip?: number
}
