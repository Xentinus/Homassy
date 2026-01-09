/**
 * Composable for providing locale configuration to UInputDate components
 *
 * Maps i18n locale codes to BCP 47 language tags for @internationalized/date
 *
 * Hungarian: hu-HU → yyyy. MM. dd. format
 * English: en-GB → dd/MM/yyyy format (British)
 * German: de-DE → dd.MM.yyyy format
 */
export const useInputDateLocale = () => {
  const { locale } = useI18n()

  const inputDateLocale = computed(() => {
    switch (locale.value) {
      case 'hu':
        return 'hu-HU'
      case 'de':
        return 'de-DE'
      case 'en':
      default:
        return 'en-GB' // British English format (dd/MM/yyyy)
    }
  })

  return {
    inputDateLocale
  }
}
