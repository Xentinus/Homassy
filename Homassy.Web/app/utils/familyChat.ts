/**
 * The pure shape of a chat stream (#146): who said what, in which run, on which day.
 *
 * Framework-free and side-effect-free, the same split `activityTimeline.ts` and `realtimeStatus.ts`
 * use — a plain module the composable and the component call from a `computed`. Day bucketing is
 * not reimplemented here: `groupByDay` already answers "Today / Yesterday / a date" in the
 * viewer's own local calendar, and a chat's day separators ask exactly that question.
 */
import { groupByDay, type DayGroup } from '~/utils/activityTimeline'
import { FamilyChatReferenceKind } from '~/types/enums'

/** How close together two messages from one sender have to be to collapse into a single run. */
export const SENDER_RUN_GAP_MINUTES = 5

const MS_PER_MINUTE = 60 * 1000

/** The least a message needs for the grouping rules to apply to it. */
export interface GroupableMessage {
  publicId: string
  sentAt: string
  sender: { publicId: string }
}

/**
 * Consecutive messages from one sender, close enough in time to read as one utterance.
 *
 * The run carries its own identity (`key`) so a list can be keyed on something stable: the first
 * message's public id is stable for the life of the run, because a run only ever grows at the end.
 */
export interface SenderRun<T extends GroupableMessage> {
  key: string
  senderPublicId: string
  /** When the run started — what the group header dates, rather than the newest message in it. */
  startedAt: string
  messages: T[]
}

/**
 * Collapses a chronological list into one run per stretch of same-sender messages sent within
 * `gapMinutes` of each other.
 *
 * Contiguity is the whole rule: two runs from the same person with somebody else's message in
 * between stay two runs, because that is what the conversation actually looked like. The gap is
 * measured against the run's **previous message**, not its start, so a person typing steadily for
 * an hour is one run rather than a new one every five minutes.
 *
 * `messages` is expected oldest-first, the order the stream renders in.
 */
export const groupBySender = <T extends GroupableMessage>(
  messages: T[],
  gapMinutes: number = SENDER_RUN_GAP_MINUTES
): SenderRun<T>[] => {
  const runs: SenderRun<T>[] = []
  const gapMs = gapMinutes * MS_PER_MINUTE

  for (const message of messages) {
    const current = runs.at(-1)
    const previous = current?.messages.at(-1)

    const continues = current !== undefined
      && previous !== undefined
      && current.senderPublicId === message.sender.publicId
      && Math.abs(new Date(message.sentAt).getTime() - new Date(previous.sentAt).getTime()) <= gapMs

    if (continues) {
      current.messages.push(message)
    } else {
      runs.push({
        key: message.publicId,
        senderPublicId: message.sender.publicId,
        startedAt: message.sentAt,
        messages: [message]
      })
    }
  }

  return runs
}

/** One day's worth of the stream: the day separator, and the sender runs under it. */
export interface ChatDaySection<T extends GroupableMessage> {
  key: string
  runs: SenderRun<T>[]
}

/**
 * The stream as the panel renders it: day sections outermost, sender runs inside them.
 *
 * Days first, then runs — never the other way round: a run that straddles local midnight has to
 * be split by the separator between the two days, and grouping by sender first would produce a
 * run that belongs to two sections at once.
 *
 * `messages` is expected oldest-first; `now` is passed in rather than read, so this stays pure and
 * a test can pin the boundary.
 */
export const buildChatSections = <T extends GroupableMessage>(
  messages: T[],
  now: Date,
  gapMinutes: number = SENDER_RUN_GAP_MINUTES
): ChatDaySection<T>[] => {
  const withTimestamp = messages.map(message => ({ ...message, timestamp: message.sentAt }))
  const days: DayGroup<T & { timestamp: string }>[] = groupByDay(withTimestamp, now)

  return days.map(day => ({
    key: day.key,
    runs: groupBySender(day.entries, gapMinutes)
  }))
}

/**
 * The icon a reference chip carries, per kind.
 *
 * Here rather than in the two components that render chips (the stream and the composer), which
 * each kept their own copy: two maps keyed by an enum are two places to forget a new kind, and a
 * missing key renders the fallback paperclip silently. Keyed numerically, like the enum is on the
 * wire.
 */
const REFERENCE_ICONS: Record<FamilyChatReferenceKind, string> = {
  [FamilyChatReferenceKind.Product]: 'i-lucide-package',
  [FamilyChatReferenceKind.ShoppingLocation]: 'i-lucide-store',
  [FamilyChatReferenceKind.StorageLocation]: 'i-lucide-archive',
  [FamilyChatReferenceKind.ShoppingList]: 'i-lucide-list-checks'
}

/**
 * Where a chip takes you.
 *
 * Products have a page of their own; the other three live inside a list or a settings screen, so
 * the chip lands on the screen that shows them rather than on a route that does not exist.
 */
const REFERENCE_ROUTES: Record<FamilyChatReferenceKind, (publicId: string) => string> = {
  [FamilyChatReferenceKind.Product]: publicId => `/products/${publicId}`,
  [FamilyChatReferenceKind.ShoppingLocation]: () => '/profile/shopping-locations',
  [FamilyChatReferenceKind.StorageLocation]: () => '/profile/storage-locations',
  [FamilyChatReferenceKind.ShoppingList]: () => '/shopping-lists'
}

/** The chip icon for a kind, or the paperclip for one this build does not know. */
export const referenceIcon = (kind: FamilyChatReferenceKind): string =>
  REFERENCE_ICONS[kind] ?? 'i-lucide-paperclip'

/** Where a chip of this kind points, or null when this build has no route for it. */
export const referenceRoute = (kind: FamilyChatReferenceKind, publicId: string): string | null =>
  REFERENCE_ROUTES[kind]?.(publicId) ?? null
