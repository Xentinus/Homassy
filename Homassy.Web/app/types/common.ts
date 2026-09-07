/**
 * Common shared types used across the API
 */

export interface ApiResponse<T = unknown> {
  success: boolean
  data?: T
  errorCodes?: string[]
  timestamp: string
  /**
   * Not a server field. `useApiClient` sets this from an ASP.NET `ValidationProblemDetails`
   * body — the answer MVC gives to a model-validation failure, which carries no `errorCodes`.
   * Maps a camelCase field name to the server's own (English) messages; pass it through
   * `useApiFormErrors().toFormErrors` to put localized errors on the form's fields.
   */
  validationErrors?: Record<string, string[]>
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
  searchText?: string
}
