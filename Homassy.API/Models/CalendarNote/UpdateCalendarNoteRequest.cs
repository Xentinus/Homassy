using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.CalendarNote
{
    /// <summary>
    /// Partial patch: a null property leaves that field unchanged.
    /// </summary>
    public class UpdateCalendarNoteRequest
    {
        public DateOnly? Date { get; set; }

        [StringLength(128, MinimumLength = 1)]
        [SanitizedString]
        public string? Title { get; set; }

        /// <summary>
        /// Null leaves the body unchanged; an empty or whitespace string erases it. A string has an unambiguous
        /// empty value, so it needs no companion flag the way the reminder does.
        /// </summary>
        [StringLength(2000)]
        [SafeFreeText]
        public string? Content { get; set; }

        /// <summary>Null leaves the reminder unchanged. Use <see cref="ClearReminder"/> to remove it.</summary>
        [StringLength(8)]
        public string? ReminderTime { get; set; }

        /// <summary>
        /// Removes the reminder. Needed because a null <see cref="ReminderTime"/> already means "no change", and
        /// <c>""</c> cannot stand in for "none" here — it would reach the time parser as an accidental 400.
        /// Sending this together with a non-null <see cref="ReminderTime"/> is rejected, not silently resolved.
        /// </summary>
        public bool ClearReminder { get; set; }

        /// <summary>
        /// The concurrency token the client read with the note. Compared against the row's current version so a
        /// stale form cannot silently overwrite another member's edit; a mismatch is a 409.
        /// </summary>
        [Required]
        [StringLength(64)]
        public required string Version { get; set; }
    }
}
