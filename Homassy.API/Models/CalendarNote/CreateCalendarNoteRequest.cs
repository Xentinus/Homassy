using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.CalendarNote
{
    public class CreateCalendarNoteRequest
    {
        /// <summary>The day the note is about. Nullable so <c>[Required]</c> actually bites (see the range request).</summary>
        [Required]
        public DateOnly? Date { get; set; }

        [Required]
        [StringLength(128, MinimumLength = 1)]
        [SanitizedString]
        public required string Title { get; set; }

        [StringLength(2000)]
        [SafeFreeText]
        public string? Content { get; set; }

        /// <summary>
        /// Reminder time of day as <c>HH:mm</c>, interpreted in the author's profile timezone. Null = no reminder.
        /// A wall-clock string rather than a client-computed instant: the browser's device timezone is not
        /// necessarily the author's configured one, and a client instant could also be set unrelated to
        /// <see cref="Date"/>. The server owns the conversion.
        /// </summary>
        [StringLength(8)]
        public string? ReminderTime { get; set; }
    }
}
