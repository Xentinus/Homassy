/**
 * Global search (the command palette) types.
 */

/**
 * Which entity type a hit belongs to.
 *
 * Numeric, and the numbers mirror `Homassy.API/Enums/SearchResultKind.cs` — the API registers
 * no string enum converter, so the wire format is the number. Append only.
 */
export enum SearchResultKind {
  Product = 0,
  InventoryItem = 1,
  ShoppingList = 2,
  ShoppingLocation = 3,
  StorageLocation = 4,
  Automation = 5
}

export interface SearchResultItem {
  publicId: string
  kind: SearchResultKind
  title: string
  subtitle?: string | null
  /** API-relative product thumbnail path — run it through `useMediaUrl` before rendering. */
  imageUrl?: string | null
  color?: string | null
  /**
   * Where the hit opens when it is not itself a destination: an inventory item opens its
   * product. Null when `publicId` is the destination.
   */
  parentPublicId?: string | null
}

export interface SearchResultGroup {
  kind: SearchResultKind
  items: SearchResultItem[]
  /** Hits of this type in total, counted up to the server's scan cap — see `hasMore`. */
  totalCount: number
  /** True when the type had more matches than the scan cap, so `totalCount` is a floor. */
  hasMore: boolean
}

export interface GlobalSearchResponse {
  /** The query these groups answer, echoed so a late response can be discarded. */
  query: string
  groups: SearchResultGroup[]
  totalCount: number
}
