using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    /// <summary>
    /// One notification delivered to one user, and whether they have read it (#116).
    /// </summary>
    /// <remarks>
    /// Before this, everything <c>Homassy.Notifications</c> sent was fire-and-forget: the workers
    /// pushed and emailed, and that was the end of it. Miss the push and the information was gone;
    /// have push switched off and it never existed. This table is the record, so the app can show
    /// an inbox.
    /// <para>
    /// <b>Parameters, not prose.</b> The row stores the notification's <see cref="Type"/> and the
    /// values that vary (<see cref="ParametersJson"/>) rather than a rendered title and body. The
    /// text is composed when it is read, in the language the reader is using *then* - a user who
    /// switches to German should not find a month of Hungarian sentences in their inbox. It also
    /// means fixing a typo in a notification's wording fixes it retroactively.
    /// </para>
    /// <para>
    /// Per-user, not per-family: read state is inherently personal, and the same event
    /// legitimately produces a row for each recipient. The rows are written by the notification
    /// workers, alongside the push they send, so the two cannot diverge.
    /// </para>
    /// <para>
    /// Soft-deleted like everything else here, which is what "swipe to dismiss" writes. Rows are
    /// pruned past a retention window by the notification scheduler - an inbox is a recent-events
    /// list, not an archive, and the activity feed already keeps the durable history.
    /// </para>
    /// </remarks>
    public class UserNotification : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [EnumDataType(typeof(NotificationType))]
        public NotificationType Type { get; set; }

        /// <summary>
        /// The notification's parameters as a JSON object of string values (e.g.
        /// <c>{"listName":"Weekly shop","count":"3"}</c>), or <c>"{}"</c> for a type that takes
        /// none.
        /// </summary>
        /// <remarks>
        /// A JSON blob rather than columns: the parameter set differs per type, and there are
        /// nineteen types. Values are strings even when they are counts - the client interpolates
        /// them into a localized template and never does arithmetic on them, and keeping one type
        /// means the JSON round-trips without a per-type schema.
        /// <para>
        /// <c>text</c> rather than <c>jsonb</c>: nothing queries inside it, and the column is read
        /// only by the row's own DTO projection.
        /// </para>
        /// </remarks>
        [Required]
        public string ParametersJson { get; set; } = "{}";

        /// <summary>
        /// Where tapping the notification should go - an app-relative path such as
        /// <c>/shopping-lists</c>, or null when there is nothing specific to open.
        /// </summary>
        /// <remarks>
        /// The same path the push notification carries, recorded here so the inbox row and the
        /// push land in the same place. Deliberately a path and not an entity reference: several
        /// of these point at a list view rather than one row, and a nullable foreign key per
        /// possible target entity would be five columns to serve one tap.
        /// </remarks>
        [StringLength(512)]
        public string? TargetUrl { get; set; }

        /// <summary>When the notification was emitted. What the inbox sorts and groups by.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this user read it, or null while it is unread. Nullable timestamp rather than a
        /// bool for the same reason as elsewhere in this codebase: "when" costs the same as
        /// "whether" and answers more.
        /// </summary>
        public DateTime? ReadAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
