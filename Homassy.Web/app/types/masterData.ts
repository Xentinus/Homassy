/**
 * Payload shapes for the master-data (Törzsadatok) realtime hub (`/hubs/master-data`).
 * Upsert events carry the full entity DTO (ProductInfo / StorageLocationInfo / ShoppingLocationInfo /
 * AutomationResponse / ExternalCalendarResponse); delete events carry only the public id.
 */
export interface MasterDataDeletedEvent {
  publicId: string
}

/**
 * One row's new manual position, as carried by every `*Reordered` event and returned by every
 * reorder endpoint. Only the rows that actually moved appear — the server's ordering scheme is
 * sparse, so a single drag normally names one row (see `Homassy.API.Functions.SparseOrdering`).
 */
export interface ReorderedEntry {
  publicId: string
  sortOrder: number
}

/** `StorageLocationsReordered` / `ShoppingLocationsReordered` / `AutomationsReordered` payload. */
export interface MasterDataReorderedEvent {
  entries: ReorderedEntry[]
}
