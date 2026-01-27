export const useEnumLabel = () => {
  const { t } = useI18n()

  const getEnumLabel = (enumKey: string, value: string | number | null | undefined): string => {
    if (value === null || value === undefined || value === '') return ''

    const id = typeof value === 'string' ? value : String(value)
    const translationKey = `enums.${enumKey}.${id}`
    const translated = t(translationKey)

    return translated === translationKey ? id : translated
  }

  const formatProductCategory = (value: string | number | null | undefined): string => {
    return getEnumLabel('productCategory', value)
  }

  return {
    getEnumLabel,
    formatProductCategory
  }
}
