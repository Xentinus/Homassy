/**
 * Open Food Facts API types
 */

export interface OpenFoodFactsProduct {
  code: string
  product_name: string
  product_name_hu: string
  brands: string
  categories: string
  categories_tags: string[]
  quantity: string
  image_url: string
  image_small_url: string
  image_front_url: string
  nutrition_grades: string
  nutriscore_grade: string
  ecoscore_grade: string
  nova_group: number
  ingredients_text: string
  ingredients_text_hu: string
  allergens: string
  allergens_tags: string[]
  countries: string
  countries_tags: string[]
  stores: string
  nutriments: OpenFoodFactsNutriments
  image_base64?: string
}

export interface OpenFoodFactsNutriments {
  'energy-kcal_100g': number
  'energy-kj_100g': number
  proteins_100g: number
  carbohydrates_100g: number
  sugars_100g: number
  fat_100g: number
  'saturated-fat_100g': number
  fiber_100g: number
  salt_100g: number
  sodium_100g: number
}
