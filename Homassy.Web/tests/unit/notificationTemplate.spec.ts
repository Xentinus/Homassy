import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { notificationTemplate } from '~/utils/notificationTemplate'

const LOCALES = ['en', 'hu', 'de'] as const

function loadLocale(locale: string): Record<string, unknown> {
  const path = fileURLToPath(new URL(`../../i18n/locales/${locale}.json`, import.meta.url))
  return JSON.parse(readFileSync(path, 'utf8'))
}

function lookup(bag: Record<string, unknown>, key: string): unknown {
  return key.split('.').reduce<unknown>(
    (node, part) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[part] : undefined),
    bag
  )
}

/** Every type the API can send, i.e. every member of its `NotificationType` enum. */
const ALL_TYPES = [
  'WeeklySummary',
  'ShoppingListItemsAdded',
  'ShoppingListItemsEdited',
  'ShoppingListItemsDeleted',
  'ShoppingListItemsPurchased',
  'ShoppingListCreated',
  'ShoppingListDeleted',
  'InventoryItemsCreated',
  'InventoryItemsUpdated',
  'InventoryItemsDeleted',
  'InventoryItemsConsumed',
  'AutomationExecuted',
  'AutomationReminder',
  'AutomationAddedToShoppingList',
  'LowStock',
  'FamilyJoinRequest',
  'FamilyJoinApproved',
  'FamilyJoinDeclined',
  'CalendarEventReminder'
]

describe('notificationTemplate', () => {
  it('keys a known type on its own name', () => {
    const template = notificationTemplate({ type: 'ShoppingListItemsAdded', parameters: { listName: 'Weekly shop', count: '3' } })

    expect(template.titleKey).toBe('notifications.types.ShoppingListItemsAdded.title')
    expect(template.bodyKey).toBe('notifications.types.ShoppingListItemsAdded.body')
    expect(template.params).toEqual({ listName: 'Weekly shop', count: '3' })
  })

  it('falls back for a type this build does not know', () => {
    // Reachable in normal operation: an installed PWA keeps running the bundle it was installed
    // with, so a client can be older than the deploy that added a type.
    const template = notificationTemplate({ type: 'SomethingFromTheFuture', parameters: { x: '1' } })

    expect(template.titleKey).toBe('notifications.types.unknown.title')
    expect(template.bodyKey).toBe('notifications.types.unknown.body')
    // The parameters are dropped with the template — interpolating them into a message written
    // for a different type would produce nonsense, not a partial answer.
    expect(template.params).toEqual({})
  })

  it('tolerates absent parameters', () => {
    expect(notificationTemplate({ type: 'WeeklySummary' }).params).toEqual({})
    expect(notificationTemplate({ type: 'WeeklySummary', parameters: null }).params).toEqual({})
  })

  it('gives every known type an icon', () => {
    for (const type of ALL_TYPES) {
      const template = notificationTemplate({ type })
      expect(template.icon, type).toMatch(/^i-lucide-/)
    }
  })

  describe('the calendar reminder branches on its parameters', () => {
    const reminder = (leadMinutes: string, isAllDay: string) =>
      notificationTemplate({ type: 'CalendarEventReminder', parameters: { eventTitle: 'Dentist', leadMinutes, isAllDay } }).bodyKey

    it('uses the "now" wording at zero lead', () => {
      expect(reminder('0', 'false')).toBe('notifications.types.CalendarEventReminder.bodyNow')
    })

    it('uses the lead wording ahead of the event', () => {
      expect(reminder('15', 'false')).toBe('notifications.types.CalendarEventReminder.bodyLead')
    })

    it('has its own pair for an all-day event', () => {
      expect(reminder('0', 'true')).toBe('notifications.types.CalendarEventReminder.bodyAllDayToday')
      expect(reminder('1440', 'true')).toBe('notifications.types.CalendarEventReminder.bodyAllDayLead')
    })

    it('treats missing parameters as "now", not as a crash', () => {
      expect(notificationTemplate({ type: 'CalendarEventReminder' }).bodyKey)
        .toBe('notifications.types.CalendarEventReminder.bodyNow')
    })
  })
})

describe('the locale files cover every notification type', () => {
  // The row's whole text comes from these keys, so a missing one is a blank notification rather
  // than a build error. This is the check that would have caught it.
  for (const locale of LOCALES) {
    it(`${locale} has a title and a body for each type`, () => {
      const bag = loadLocale(locale)
      const missing: string[] = []

      for (const type of [...ALL_TYPES, 'unknown']) {
        if (typeof lookup(bag, `notifications.types.${type}.title`) !== 'string') {
          missing.push(`${type}.title`)
        }

        // The calendar reminder is the one type with four bodies instead of one.
        const bodies = type === 'CalendarEventReminder'
          ? ['bodyNow', 'bodyLead', 'bodyAllDayToday', 'bodyAllDayLead']
          : ['body']

        for (const body of bodies) {
          if (typeof lookup(bag, `notifications.types.${type}.${body}`) !== 'string') {
            missing.push(`${type}.${body}`)
          }
        }
      }

      expect(missing).toEqual([])
    })
  }
})
