/**
 * Composable for formatting dates based on the current locale
 * 
 * Hungarian: yyyy.MM.dd
 * English: dd/MM/yyyy (British format)
 * German: dd.MM.yyyy (German format)
 */
export const useDateFormat = () => {
  const { locale } = useI18n()

  const formatDate = (dateString: string | undefined): string => {
    if (!dateString) return ''

    try {
      const date = new Date(dateString)
      
      // Check if date is valid
      if (isNaN(date.getTime())) {
        return dateString
      }

      const year = date.getFullYear()
      const month = String(date.getMonth() + 1).padStart(2, '0')
      const day = String(date.getDate()).padStart(2, '0')

      switch (locale.value) {
        case 'hu':
          // Hungarian format: yyyy.MM.dd
          return `${year}.${month}.${day}`
        
        case 'de':
          // German format: dd.MM.yyyy
          return `${day}.${month}.${year}`
        
        case 'en':
        default:
          // British format: dd/MM/yyyy
          return `${day}/${month}/${year}`
      }
    } catch (error) {
      console.error('Date formatting error:', error)
      return dateString
    }
  }

  const formatDateTime = (dateString: string | undefined): string => {
    if (!dateString) return ''

    try {
      const date = new Date(dateString)
      
      // Check if date is valid
      if (isNaN(date.getTime())) {
        return dateString
      }

      const datePart = formatDate(dateString)
      const hours = String(date.getHours()).padStart(2, '0')
      const minutes = String(date.getMinutes()).padStart(2, '0')

      return `${datePart} ${hours}:${minutes}`
    } catch (error) {
      console.error('DateTime formatting error:', error)
      return dateString
    }
  }

  return {
    formatDate,
    formatDateTime
  }
}
