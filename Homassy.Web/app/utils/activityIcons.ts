/**
 * Activity-type → icon mapping.
 *
 * Pure and framework-free on purpose, so it is unit-testable without mounting a component. Moved
 * out of `ActivityCard.vue` (see `ActivityCardShell.vue`, which is the one place that now imports
 * it) so there is exactly one copy instead of one per card that shows an activity.
 */
import { ActivityType } from '~/types/activity'

/** Icon for an activity's type, grouped by the entity the activity happened to. */
export const getActivityIcon = (activityType: ActivityType): string => {
  if (activityType >= ActivityType.ProductCreate && activityType <= ActivityType.ProductPhotoDownloadFromOpenFoodFacts) {
    return 'i-lucide-package'
  }
  if (activityType >= ActivityType.ProductInventoryCreate && activityType <= ActivityType.ProductInventoryDelete) {
    return 'i-lucide-archive'
  }
  if (activityType >= ActivityType.ShoppingListCreate && activityType <= ActivityType.ShoppingListDelete) {
    return 'i-lucide-shopping-cart'
  }
  if ((activityType >= ActivityType.ShoppingListItemAdd && activityType <= ActivityType.ShoppingListItemDelete) ||
      activityType === ActivityType.ShoppingListItemQuickPurchase ||
      activityType === ActivityType.ShoppingListItemRestorePurchase) {
    return 'i-lucide-list'
  }
  if ((activityType >= ActivityType.FamilyCreate && activityType <= ActivityType.FamilyLeave) ||
      (activityType >= ActivityType.FamilyJoinRequestCreate && activityType <= ActivityType.FamilyJoinRequestDecline)) {
    return 'i-lucide-users'
  }
  return 'i-lucide-activity'
}

/** Semantic icon colour (Tailwind utility classes) for an activity's type. */
export const getActivityIconColor = (activityType: ActivityType): string => {
  if (activityType >= ActivityType.ProductCreate && activityType <= ActivityType.ProductPhotoDownloadFromOpenFoodFacts) {
    return 'text-primary-600 dark:text-primary-400'
  }
  if (activityType >= ActivityType.ProductInventoryCreate && activityType <= ActivityType.ProductInventoryDelete) {
    return 'text-amber-600 dark:text-amber-400'
  }
  if (activityType >= ActivityType.ShoppingListCreate && activityType <= ActivityType.ShoppingListDelete) {
    return 'text-pink-600 dark:text-pink-400'
  }
  if ((activityType >= ActivityType.ShoppingListItemAdd && activityType <= ActivityType.ShoppingListItemDelete) ||
      activityType === ActivityType.ShoppingListItemQuickPurchase ||
      activityType === ActivityType.ShoppingListItemRestorePurchase) {
    return 'text-pink-600 dark:text-pink-400'
  }
  if ((activityType >= ActivityType.FamilyCreate && activityType <= ActivityType.FamilyLeave) ||
      (activityType >= ActivityType.FamilyJoinRequestCreate && activityType <= ActivityType.FamilyJoinRequestDecline)) {
    return 'text-primary-600 dark:text-primary-400'
  }
  return 'text-gray-600 dark:text-gray-400'
}
