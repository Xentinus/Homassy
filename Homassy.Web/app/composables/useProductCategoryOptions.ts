import type { Ref } from 'vue'
import type { SelectValue } from '~/types/selectValue'
import { PRODUCT_CATEGORY_GROUP_ORDER, getProductCategoryGroup } from '~/utils/productCategoryGroups'

export interface ProductCategoryOption {
  label: string
  value?: number | string
  type?: 'label'
}

/**
 * Turns the raw `SelectValueType.ProductCategory` list from the API into grouped
 * `USelectMenu` items — an array of arrays, each headed by a `type: 'label'`
 * entry. There are ~950 categories, so a flat list is unusable; grouping plus the
 * select's built-in search is what makes it navigable. Empty groups are dropped
 * by the select itself while filtering.
 *
 * Pass `numeric: false` when the consuming form field holds the category as a
 * string (`ProductFormDrawer`); the default emits numbers.
 */
export const useProductCategoryOptions = (
  raw: Ref<SelectValue[]>,
  { numeric = true }: { numeric?: boolean } = {}
) => {
  const { t, locale } = useI18n()

  const categoryOptions = computed<ProductCategoryOption[][]>(() => {
    const byGroup = new Map<number, ProductCategoryOption[]>()

    for (const selectValue of raw.value) {
      const numericValue = Number.parseInt(selectValue.text, 10)
      if (Number.isNaN(numericValue)) continue

      // A value with no group would otherwise vanish from the picker entirely.
      const group = getProductCategoryGroup(numericValue) ?? PRODUCT_CATEGORY_GROUP_ORDER[0]!

      if (!byGroup.has(group)) byGroup.set(group, [])
      byGroup.get(group)!.push({
        label: t(`enums.productCategory.${numericValue}`),
        value: numeric ? numericValue : selectValue.text
      })
    }

    return PRODUCT_CATEGORY_GROUP_ORDER
      .filter(group => byGroup.has(group))
      .map(group => [
        { label: t(`enums.productCategoryGroup.${group}`), type: 'label' as const },
        ...byGroup.get(group)!.sort((a, b) => a.label.localeCompare(b.label, locale.value))
      ])
  })

  return { categoryOptions }
}
