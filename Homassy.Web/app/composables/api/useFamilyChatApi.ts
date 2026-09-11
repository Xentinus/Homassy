/**
 * Family chat API composable (#144).
 *
 * The REST half of the chat: paging back through history, and the two writes. Live messages come
 * over the socket (`useFamilyChatSocket`), and the socket also answers the *first* page as part of
 * joining — so this composable's `getMessages` is what pages older, and what a client with no
 * working socket falls back to.
 *
 * Separate from `useFamilyApi`, which manages the family as a thing (members, share code,
 * picture). A conversation is content.
 */
import type {
  FamilyChatMessage,
  FamilyChatPage,
  FamilyChatUnreadResponse,
  SendFamilyChatImageRequest,
  SendFamilyChatMessageRequest
} from '~/types/familyChat'

export const useFamilyChatApi = () => {
  const client = useApiClient()

  /**
   * One page of the family conversation, newest first.
   * @param before - `nextCursor` from the previous page; omit for the newest page.
   * @param limit - Messages to fetch; the API clamps it.
   */
  const getMessages = async (before?: string | null, limit = 30) => {
    const params = new URLSearchParams({ limit: limit.toString() })
    if (before) params.append('before', before)

    // No toast: the panel shows its own inline failure state, and a toast for a page that did not
    // load is a second report of something already on screen.
    return await client.get<FamilyChatPage>(`/api/v1/FamilyChat/messages?${params.toString()}`, {
      showErrorToast: false
    })
  }

  /**
   * Sends a text message.
   *
   * No toast either: the message is already on screen as a pending bubble, and a failure turns it
   * into a failed one with a retry on it — which is both more specific and more actionable than a
   * toast that disappears.
   */
  const sendMessage = async (request: SendFamilyChatMessageRequest) => {
    return await client.post<FamilyChatMessage>('/api/v1/FamilyChat/messages', request, {
      showErrorToast: false
    })
  }

  /**
   * Sends a picture, with an optional caption (#147).
   *
   * Base64 in, a message with an image **URL** out — the bytes never come back in a payload. Same
   * no-toast rule as the text path: the failure belongs on the message bubble.
   */
  const sendImageMessage = async (request: SendFamilyChatImageRequest) => {
    return await client.post<FamilyChatMessage>('/api/v1/FamilyChat/messages/image', request, {
      showErrorToast: false
    })
  }

  /**
   * How many messages the caller has not read (#149).
   *
   * No error toast: this is fetched at boot and after every read, neither of which the user asked
   * for, and a toast about a badge that failed to refresh is noise about nothing they can act on.
   */
  const getUnreadCount = async () => {
    return await client.get<FamilyChatUnreadResponse>('/api/v1/FamilyChat/unread-count', {
      showErrorToast: false
    })
  }

  /** Marks the conversation read up to now. Answers with what is left unread. */
  const markRead = async () => {
    return await client.post<FamilyChatUnreadResponse>('/api/v1/FamilyChat/read', {}, {
      showErrorToast: false
    })
  }

  /** Deletes one of your own messages. */
  const deleteMessage = async (publicId: string) => {
    return await client.delete(`/api/v1/FamilyChat/messages/${publicId}`)
  }

  return { getMessages, sendMessage, sendImageMessage, getUnreadCount, markRead, deleteMessage }
}
