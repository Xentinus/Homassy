import { ProductCategoryGroup } from '~/types/enums'
import { getProductCategoryGroup } from './productCategoryGroups'

/**
 * What a product with no picture looks like: an icon and a colour per category group.
 *
 * Presentation-only, like the grouping it keys off — the API neither knows nor stores the group.
 * Hand-written, unlike `productCategoryGroups.ts`, which is generated and must not be edited.
 *
 * The hue is a number, not a Tailwind class: the placeholder is drawn as a tinted wash behind a
 * tinted icon, and the two have to be derived from one value so they stay in step. Lightness is
 * fixed per theme in `ProductImage.vue`, which is what lets one hue work in both.
 */
interface CategoryVisual {
  /** Lucide icon name, as `UIcon` expects it. */
  icon: string
  /** HSL hue, 0-359. */
  hue: number
}

const FALLBACK: CategoryVisual = { icon: 'i-lucide-package', hue: 220 }

const VISUALS: Record<ProductCategoryGroup, CategoryVisual> = {
  [ProductCategoryGroup.Other]: FALLBACK,
  [ProductCategoryGroup.Food]: { icon: 'i-lucide-apple', hue: 25 },
  [ProductCategoryGroup.Health]: { icon: 'i-lucide-pill', hue: 340 },
  [ProductCategoryGroup.PersonalCare]: { icon: 'i-lucide-sparkles', hue: 300 },
  [ProductCategoryGroup.Bathroom]: { icon: 'i-lucide-shower-head', hue: 195 },
  [ProductCategoryGroup.Cleaning]: { icon: 'i-lucide-spray-can', hue: 175 },
  [ProductCategoryGroup.PestControl]: { icon: 'i-lucide-bug', hue: 90 },
  [ProductCategoryGroup.Kitchen]: { icon: 'i-lucide-utensils', hue: 15 },
  [ProductCategoryGroup.Appliances]: { icon: 'i-lucide-microwave', hue: 210 },
  [ProductCategoryGroup.Home]: { icon: 'i-lucide-house', hue: 35 },
  [ProductCategoryGroup.HomeImprovement]: { icon: 'i-lucide-brick-wall', hue: 20 },
  [ProductCategoryGroup.Tools]: { icon: 'i-lucide-wrench', hue: 45 },
  [ProductCategoryGroup.Garden]: { icon: 'i-lucide-flower-2', hue: 120 },
  [ProductCategoryGroup.Automotive]: { icon: 'i-lucide-car', hue: 235 },
  [ProductCategoryGroup.Winter]: { icon: 'i-lucide-snowflake', hue: 200 },
  [ProductCategoryGroup.Electronics]: { icon: 'i-lucide-cpu', hue: 250 },
  [ProductCategoryGroup.SmartHome]: { icon: 'i-lucide-house-wifi', hue: 265 },
  [ProductCategoryGroup.Clothing]: { icon: 'i-lucide-shirt', hue: 320 },
  [ProductCategoryGroup.Entertainment]: { icon: 'i-lucide-gamepad-2', hue: 280 },
  [ProductCategoryGroup.SportsAndLeisure]: { icon: 'i-lucide-dumbbell', hue: 150 },
  [ProductCategoryGroup.Pets]: { icon: 'i-lucide-paw-print', hue: 60 },
  [ProductCategoryGroup.Office]: { icon: 'i-lucide-paperclip', hue: 215 },
  [ProductCategoryGroup.BabyAndKids]: { icon: 'i-lucide-baby', hue: 350 },
  [ProductCategoryGroup.PartyAndSeasonal]: { icon: 'i-lucide-party-popper', hue: 310 },
  [ProductCategoryGroup.Travel]: { icon: 'i-lucide-luggage', hue: 185 },
  [ProductCategoryGroup.Safety]: { icon: 'i-lucide-shield-check', hue: 5 },
  [ProductCategoryGroup.Valuables]: { icon: 'i-lucide-gem', hue: 50 }
}

/**
 * The icon and hue for a product's category. Falls back to a neutral package for a product with
 * no category, or a category number that falls in no group.
 */
export function getProductCategoryVisual(category?: number | null): CategoryVisual {
  if (category == null) return FALLBACK

  const group = getProductCategoryGroup(category)
  return group == null ? FALLBACK : VISUALS[group]
}
