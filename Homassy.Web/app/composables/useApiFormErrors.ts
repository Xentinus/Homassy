import type { FormError } from '@nuxt/ui'

/**
 * ASP.NET model-validation failures never reach the API's own error envelope — MVC answers
 * them itself with a `ValidationProblemDetails` body that carries no `errorCodes`, only an
 * `errors` map. Its keys come in two spellings, both pinned by
 * `ProductControllerTests.CreateProduct_InvalidRequest_ReturnsValidationProblemDetailsKeyedByField`:
 *
 *   `"Name"`        a DataAnnotations failure, keyed by the PascalCase CLR property
 *   `"$.category"`  a JSON deserialization failure, keyed by a JSON path
 *
 * Both normalize onto the camelCase field name the forms use. A body can also carry keys that
 * are not fields at all — `"request"`, emitted when the whole body fails to bind — so those
 * survive normalization here and are filtered by `toFormErrors` against the form's own state.
 */
export const normalizeValidationErrors = (errors: unknown): Record<string, string[]> | undefined => {
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return undefined

  const normalized: Record<string, string[]> = {}

  for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
    const messages = Array.isArray(value) ? value.filter((message): message is string => typeof message === 'string') : []
    if (!messages.length) continue

    // Both spellings of one property collapse onto the same field, so append rather than assign.
    const field = normalizeFieldName(key)
    normalized[field] = [...(normalized[field] ?? []), ...messages]
  }

  return Object.keys(normalized).length ? normalized : undefined
}

/**
 * `"Name"` → `name`, `"$.category"` → `category`, `"$.locations[0].Name"` → `locations[0].Name`.
 * Only the first path segment is lower-cased; the rest is left alone, so a nested path still
 * points somewhere sensible even though no current form has one. Lower-casing just the first
 * character is what the API's camelCase naming policy does, which is why `ICalUrl` correctly
 * becomes `iCalUrl`.
 */
const normalizeFieldName = (key: string): string => {
  const path = key.startsWith('$.') ? key.slice(2) : key
  const separator = path.indexOf('.')
  const head = separator === -1 ? path : path.slice(0, separator)
  const tail = separator === -1 ? '' : path.slice(separator)

  return head.charAt(0).toLowerCase() + head.slice(1) + tail
}

export const useApiFormErrors = () => {
  const { t } = useI18n()

  /**
   * Turns a normalized `validationErrors` map into what `UForm.setErrors` takes, keeping only
   * the keys the form has a field for — a body-level key such as `request` has no input to
   * attach to, and the client's toast is what reports it.
   *
   * The server's own message is deliberately not shown: it is English DataAnnotations prose
   * and the UI runs in three languages. It stays in `validationErrors` and in the client's
   * `console.warn`, where whoever has to explain the rejection can read it.
   */
  const toFormErrors = (
    validationErrors: Record<string, string[]> | undefined,
    knownFields: Iterable<string>
  ): FormError[] => {
    if (!validationErrors) return []

    const fields = new Set(knownFields)

    return Object.keys(validationErrors)
      .filter(field => fields.has(field))
      .map(name => ({ name, message: t('common.invalidValue') }))
  }

  return { toFormErrors }
}
