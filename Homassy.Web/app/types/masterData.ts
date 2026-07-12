/**
 * Payload shapes for the master-data (Törzsadatok) realtime hub (`/hubs/master-data`).
 * Upsert events carry the full entity DTO (ProductInfo / StorageLocationInfo / ShoppingLocationInfo /
 * AutomationResponse / ExternalCalendarResponse); delete events carry only the public id.
 */
export interface MasterDataDeletedEvent {
  publicId: string
}
