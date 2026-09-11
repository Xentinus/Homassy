/**
 * Notification centre API composable (#116).
 *
 * Deliberately separate from `useUserApi`, which carries the notification *preferences*
 * (`/user/notification`). Those are settings; these are content — cursor-paged and mutated per
 * row — and conflating the two under one prefix is how `NotificationsDrawer` came to mean "the
 * preferences panel".
 */
import type { NotificationPage, UnreadCountResponse } from '~/types/notification'

export const useNotificationsApi = () => {
  const client = useApiClient()

  /**
   * One page of the caller's notifications, newest first.
   * @param cursor - `nextCursor` from the previous page; omit for the first page.
   * @param pageSize - Rows to fetch; the API clamps it.
   */
  const getNotifications = async (cursor?: string | null, pageSize = 25) => {
    const params = new URLSearchParams({ pageSize: pageSize.toString() })
    if (cursor) params.append('cursor', cursor)

    return await client.get<NotificationPage>(`/api/v1/Notification?${params.toString()}`)
  }

  /**
   * The unread count on its own.
   *
   * No error toast: this is fetched at boot and on every push arrival, neither of which the user
   * asked for, and a toast about a badge that failed to refresh is noise about nothing they can
   * act on.
   */
  const getUnreadCount = async () => {
    return await client.get<UnreadCountResponse>('/api/v1/Notification/unread-count', { showErrorToast: false })
  }

  /** Marks one notification read. Answers with the remaining unread count. */
  const markRead = async (publicId: string) => {
    return await client.post<UnreadCountResponse>(`/api/v1/Notification/${publicId}/read`, {}, { showErrorToast: false })
  }

  /** Marks every unread notification read. */
  const markAllRead = async () => {
    return await client.post<UnreadCountResponse>('/api/v1/Notification/read-all', {})
  }

  /** Dismisses one notification — the soft delete behind swipe-to-dismiss. */
  const dismiss = async (publicId: string) => {
    return await client.delete<UnreadCountResponse>(`/api/v1/Notification/${publicId}`)
  }

  return { getNotifications, getUnreadCount, markRead, markAllRead, dismiss }
}
