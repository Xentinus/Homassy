/**
 * Household presence — who has the app open right now, and who is in a shop.
 *
 * Pushed by the API's `/hubs/presence` channel (`PresenceHub`) to everyone in the household, and
 * rendered by the home screen's member strip. Mirrors `FamilyPresenceMemberInfo` on the server.
 *
 * Unlike `PresenceMember` in `~/types/realtime` (who has *this shopping list* open), this roster
 * is household-wide and includes the viewer themselves — the strip shows the whole household.
 */
export interface FamilyPresenceMember {
  publicId: string
  displayName: string
  /** Server-relative avatar path; resolve with `useMediaUrl().mediaUrl()`. */
  profilePictureUrl?: string | null
  /** Identity-colour key from the curated palette, or null for the deterministic pick. */
  identityColor?: string | null
  /** How many of this member's devices have the app open. */
  deviceCount: number
  /** True while at least one of their devices is in in-store shopping mode. */
  isShopping: boolean
  /** The shopped list's name, when the server could resolve it for that member. */
  shoppingContext?: string | null
}
