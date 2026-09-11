using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    /// <summary>
    /// The caller is not a member of the family whose conversation they asked for - or is in no
    /// family at all, which for a family chat is the same answer (#144).
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="ShoppingListAccessDeniedException"/> deliberately, down to the 403 the
    /// middleware maps it to: the chat is family-scoped exactly the way a shared list is, and one
    /// shape of "not yours" across the API is what keeps the client's error handling uniform.
    /// </remarks>
    public class FamilyChatAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.FamilyChatAccessDenied;

        public FamilyChatAccessDeniedException(string message = "You do not have access to this family chat") : base(message) { }
    }

    /// <summary>
    /// No such message in the caller's family - deleted, never existed, or somebody else's.
    /// </summary>
    /// <remarks>
    /// The three are deliberately indistinguishable, the same stance
    /// <c>NotificationNotFoundException</c> takes: telling them apart would let a caller probe
    /// which message ids exist in a family they cannot read.
    /// </remarks>
    public class FamilyChatMessageNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.FamilyChatMessageNotFound;

        public FamilyChatMessageNotFoundException(string message = "Chat message not found") : base(message) { }
    }

    /// <summary>The message cannot be posted as sent (empty body, oversized image, unreadable cursor).</summary>
    public class FamilyChatMessageInvalidException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.FamilyChatMessageInvalid;

        public FamilyChatMessageInvalidException(string message) : base(message) { }
    }

    /// <summary>
    /// The sender is posting faster than the per-user allowance (#144).
    /// </summary>
    /// <remarks>
    /// Per user, not per IP, which is what the middleware's route-template buckets already do: a
    /// family behind one NAT would otherwise share a bucket, and a single tab spamming the socket
    /// would throttle everybody in the house. Maps to 429, the same status the middleware answers
    /// with, so a client that already handles rate limiting needs no new branch.
    /// </remarks>
    public class FamilyChatRateLimitedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.FamilyChatRateLimited;

        public FamilyChatRateLimitedException(string message = "You are sending messages too quickly") : base(message) { }
    }
}
