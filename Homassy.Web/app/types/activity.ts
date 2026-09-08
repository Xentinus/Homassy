/**
 * Activity tracking related types
 */

/**
 * Activity type enum
 */
export enum ActivityType {
  // Product Activities (1-6)
  ProductCreate = 1,
  ProductUpdate = 2,
  ProductDelete = 3,
  ProductPhotoUpload = 4,
  ProductPhotoDelete = 5,
  ProductPhotoDownloadFromOpenFoodFacts = 6,

  // ProductInventory Activities (7-10)
  ProductInventoryCreate = 7,
  ProductInventoryUpdate = 8,
  ProductInventoryDecrease = 9,
  ProductInventoryDelete = 10,

  // ShoppingList Activities (11-13)
  ShoppingListCreate = 11,
  ShoppingListUpdate = 12,
  ShoppingListDelete = 13,

  // ShoppingListItem Activities (14-17, 21-22)
  ShoppingListItemAdd = 14,
  ShoppingListItemUpdate = 15,
  ShoppingListItemPurchase = 16,
  ShoppingListItemDelete = 17,

  // Family Activities (18-20)
  FamilyCreate = 18,
  FamilyJoin = 19,
  FamilyLeave = 20,

  // ShoppingListItem Activities (continued)
  ShoppingListItemQuickPurchase = 21,
  ShoppingListItemRestorePurchase = 22,

  // Automation Activities (23-26)
  AutomationCreate = 23,
  AutomationUpdate = 24,
  AutomationDelete = 25,
  AutomationExecute = 26,

  // Family Join Request Activities (27-29)
  FamilyJoinRequestCreate = 27,
  FamilyJoinRequestApprove = 28,
  FamilyJoinRequestDecline = 29
}

/**
 * Activity info response
 */
export interface ActivityInfo {
  publicId: string
  userPublicId: string
  userName: string
  /** The actor's chosen identity-colour key, or null/absent for the deterministic pick. */
  identityColor?: string | null
  timestamp: string
  activityType: ActivityType
  recordName: string
  unit?: number
  quantity?: number
}

/**
 * Get activities request parameters
 */
export interface GetActivitiesRequest {
  activityType?: ActivityType
  startDate?: string
  endDate?: string
  userPublicId?: string
  pageNumber?: number
  pageSize?: number
  returnAll?: boolean
}

/**
 * Paginated activities response
 */
export interface PagedActivitiesResponse {
  items: ActivityInfo[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
  isUnpaginated: boolean
}

/**
 * One row of the cursor-paged, server-aggregated activity timeline: either a single activity
 * (`count === 1`, `items` absent) or a collapsed run of same-actor, same-activity-type activities
 * recorded within 5 minutes of each other (`count > 1`) — e.g. a bulk import that would otherwise
 * produce dozens of near-identical cards. Mirrors `Homassy.API.Models.Activity.ActivityTimelineEntry`
 * (see commit e55e6d1).
 */
export interface ActivityTimelineEntry {
  /** Identity for the entry: the run's newest activity's own publicId. */
  publicId: string
  userPublicId: string
  userName: string
  userProfilePictureUrl?: string | null
  /** The actor's chosen identity-colour key, or null/absent for the deterministic pick. */
  userIdentityColor?: string | null
  /** The run's newest activity's timestamp (its only timestamp when count === 1). */
  timestamp: string
  /** The run's oldest activity's timestamp. Absent when count === 1. */
  lastTimestamp?: string
  activityType: ActivityType
  /** The run's newest activity's record name. */
  recordName: string
  unit?: number
  quantity?: number
  /** 1 for a single activity; greater than 1 for a collapsed run. */
  count: number
  /** The run's individual activities, newest first, for the expand-on-tap. Absent when count === 1. */
  items?: ActivityInfo[]
}

/**
 * One page of the activity timeline, newest first. Mirrors
 * `Homassy.API.Models.Activity.ActivityTimelineResult`.
 */
export interface ActivityTimelineResult {
  entries: ActivityTimelineEntry[]
  /** Opaque cursor for the next page, or absent/null when this page reached the end. */
  nextCursor?: string | null
}
