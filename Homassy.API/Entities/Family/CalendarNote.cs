using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// A free-text note a family member attaches to a single calendar day, visible to the whole family.
    /// Optionally carries a reminder that pushes a notification to every member at one absolute instant.
    /// </summary>
    /// <remarks>
    /// The user foreign keys use <see cref="Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict"/>: a note is
    /// *family* data, so losing a member must not delete the notes they wrote for everyone. Users are only ever
    /// soft-deleted, so this never fires in practice — it just makes a hypothetical hard delete fail loudly
    /// instead of shredding family history. Neither user FK has a navigation property, because a required
    /// navigation to a soft-delete-filtered principal makes EF log a query-filter warning on every model build.
    /// </remarks>
    public class CalendarNote : RecordChangeEntity
    {
        public int FamilyId { get; set; }
        public Family Family { get; set; } = null!;

        /// <summary>
        /// The day the note is about. Deliberately <see cref="DateOnly"/> (a <c>date</c> column, no timezone):
        /// a <c>timestamptz</c> would be reinterpreted per session timezone, so a note written for "March 15"
        /// in Budapest could read as March 14 in New York — which breaks "visible to every family member".
        /// </summary>
        public DateOnly Date { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 1)]
        public required string Title { get; set; }

        /// <summary>
        /// Optional body. Null rather than <c>""</c> when absent, so "no content" has a single representation.
        /// </summary>
        [StringLength(2000)]
        public string? Content { get; set; }

        /// <summary>
        /// The wall-clock time the author picked. Stored alongside <see cref="ReminderAt"/> because the instant
        /// alone cannot be converted back to a wall-clock time without knowing which zone to go through — which
        /// is exactly what a later change to <see cref="Date"/> needs.
        /// </summary>
        public TimeOnly? ReminderTimeOfDay { get; set; }

        /// <summary>
        /// Snapshot of the author's timezone, taken when the reminder is first set and kept on later edits. That
        /// keeps the reminder anchored where the author intended: a member in another zone editing the note (or
        /// the author later changing their profile timezone) must not silently move it.
        /// </summary>
        [EnumDataType(typeof(UserTimeZone))]
        public UserTimeZone? ReminderTimeZone { get; set; }

        /// <summary>
        /// Absolute instant the reminder fires, derived from <see cref="Date"/> + <see cref="ReminderTimeOfDay"/>
        /// + <see cref="ReminderTimeZone"/>. Indexed (partially) because the worker scans it every minute.
        /// </summary>
        public DateTime? ReminderAt { get; set; }

        /// <summary>
        /// At-most-once claim marker. The worker commits this <em>before</em> pushing, so a crash or a lost race
        /// can only ever skip a send, never repeat one.
        /// </summary>
        public DateTime? ReminderSentAt { get; set; }

        /// <summary>
        /// The author. An explicit column rather than <c>RecordChange.LastModifiedBy</c>, which is unjoinable
        /// JSON, holds only the *last* modifier, and gets overwritten by <c>DeleteRecord</c>.
        /// </summary>
        public int CreatedByUserId { get; set; }

        /// <summary>Initialised to the author on create, so the response DTO never needs a null branch.</summary>
        public int LastEditedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Null until the note is actually edited — that is how the UI knows to show an editor line.</summary>
        public DateTime? LastEditedAt { get; set; }

        /// <summary>
        /// Optimistic-concurrency token, re-rolled on every user edit and echoed by the client so a stale form
        /// cannot silently overwrite another member's change.
        /// <para>
        /// An explicit column rather than PostgreSQL's <c>xmin</c>: Npgsql dropped its <c>xmin</c> helper, and
        /// hand-declaring the system column makes EF's migration try to CREATE it. Keeping it explicit also means
        /// the reminder worker's <c>ReminderSentAt</c> claim — which bypasses change tracking — deliberately does
        /// *not* bump the token, so a fired reminder never invalidates a form someone has open.
        /// </para>
        /// </summary>
        public Guid RowVersion { get; set; }
    }
}
