/**
 * Family chat types (#144, #146).
 *
 * The wire shapes mirror `Homassy.API/Models/FamilyChat`; the client-only additions are marked as
 * such, because a reader tracking a field back to the API needs to know which ones are not there.
 */

export type FamilyChatMessageKind = 'Text' | 'Image'

/** Who sent a message — public id, a name, an avatar path and the identity colour. Never bytes, never an internal id. */
export interface FamilyChatSender {
  publicId: string
  displayName: string
  profilePictureUrl?: string | null
  /** Chosen identity-colour palette key, or null/absent for the deterministic pick (#114). */
  identityColor?: string | null
}

export interface FamilyChatMessage {
  publicId: string
  kind: FamilyChatMessageKind
  body?: string | null
  sentAt: string
  editedAt?: string | null
  sender: FamilyChatSender
  /** Thumbnail path for an image message (#147). */
  imageUrl?: string | null
  /** Full-size path, for the viewer (#147). */
  imageFullUrl?: string | null
  imageWidth?: number | null
  imageHeight?: number | null
}

export interface FamilyChatPage {
  items: FamilyChatMessage[]
  nextCursor?: string | null
}

export interface SendFamilyChatMessageRequest {
  body: string
  /** The sender's own id for this message, echoed back on the broadcast so it reconciles rather than duplicating. */
  correlationId?: string
}

/** A picture to post to the chat (#147). */
export interface SendFamilyChatImageRequest {
  imageBase64: string
  caption?: string
  correlationId?: string
}

/**
 * Where an optimistically appended message is in its life.
 *
 * **Client-only.** The API has no such field: a message that reached the database is simply a
 * message. This tracks the window between the send starting and the broadcast arriving, which is
 * the only time the distinction exists.
 */
export type FamilyChatSendState = 'pending' | 'sent' | 'failed'

/** A message as the stream holds it: the wire shape plus what only the sender's own client knows. */
export interface FamilyChatStreamMessage extends FamilyChatMessage {
  /** Client-only. Absent on everything that arrived from the server. */
  sendState?: FamilyChatSendState
  /** Client-only. The id this client generated before sending, used to reconcile the broadcast. */
  correlationId?: string
  /**
   * Client-only. The `data:` URL of a picture still being uploaded (#147).
   *
   * The sender sees their own photo the moment they pick it, rather than a grey box until the
   * round trip finishes — and it is dropped the moment the committed message arrives with a real
   * image URL on it.
   */
  localPreview?: string
}

/** How many chat messages the caller has not read (#149) — the bubble's badge. */
export interface FamilyChatUnreadResponse {
  totalCount: number
}

/** One family member currently typing (#148) — the payload of the hub's `TypingChanged` event is a list of these. */
export interface FamilyChatTypingMember {
  publicId: string
  displayName: string
}

/** The payload of the hub's `MessageCreated` event. */
export interface FamilyChatMessageCreatedEvent {
  message: FamilyChatMessage
  correlationId?: string | null
}

/** The payload of the hub's `MessageDeleted` event. */
export interface FamilyChatMessageDeletedEvent {
  publicId: string
  actorPublicId?: string | null
}
