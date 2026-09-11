using Homassy.API.Entities.Common;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// How far one member has read their family's conversation (#149).
    /// </summary>
    /// <remarks>
    /// <b>A marker, not a receipt per message.</b> One row per member per family holding a
    /// <see cref="LastReadAt"/> instant answers both questions the feature actually asks - how many
    /// messages are unread, and whether a message has already been seen elsewhere - at a cost of
    /// one row per person rather than one per person per message. Per-message receipts would also
    /// be a promise this chat does not make: nobody is shown who has read what.
    /// <para>
    /// The row is per family rather than per user alone because a user can leave one family and
    /// join another, and the marker from the old conversation must not silently apply to the new
    /// one.
    /// </para>
    /// </remarks>
    public class FamilyChatReadState : RecordChangeEntity
    {
        public int UserId { get; set; }

        public int FamilyId { get; set; }

        /// <summary>
        /// The moment this member last had the conversation in front of them, UTC.
        /// </summary>
        /// <remarks>
        /// Compared against a message's <c>SentAt</c>, so it is deliberately an instant and not a
        /// message id: messages arrive while the panel is open, and "everything up to now" is what
        /// the client is actually reporting.
        /// </remarks>
        public DateTime LastReadAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Homassy.API.Entities.User.User? User { get; set; }
        public Family? Family { get; set; }
    }
}
