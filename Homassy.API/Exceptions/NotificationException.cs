using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    /// <summary>
    /// A notification the caller asked to read, dismiss or open is not theirs to act on (#116).
    /// </summary>
    /// <remarks>
    /// One exception for three conditions - no such row, already dismissed, or someone else's -
    /// on purpose. Distinguishing them would let a caller probe whether a given notification id
    /// exists for another user, which is exactly the kind of oracle a per-user inbox must not
    /// offer, and there is nothing a client could usefully do differently in the three cases
    /// anyway. Maps to 404 in <c>GlobalExceptionMiddleware</c>.
    /// </remarks>
    public class NotificationNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.NotificationNotFound;

        public NotificationNotFoundException(string message = "Notification not found") : base(message) { }
    }
}
