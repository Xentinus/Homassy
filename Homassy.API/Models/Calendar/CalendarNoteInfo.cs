namespace Homassy.API.Models.Calendar
{
    /// <summary>
    /// A day note as the calendar view reads it (#60).
    /// </summary>
    public class CalendarNoteInfo
    {
        public Guid PublicId { get; set; }

        /// <summary>The day the note is about, as <c>yyyy-MM-dd</c>. A calendar day, never an instant.</summary>
        public DateOnly Date { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        /// <summary>When the family is reminded, UTC, or null for a note with no reminder.</summary>
        public DateTime? ReminderAt { get; set; }

        /// <summary>True once the reminder has gone out; the form shows it as sent rather than pending.</summary>
        public bool ReminderSent { get; set; }

        public Guid CreatedByPublicId { get; set; }

        public string CreatedByName { get; set; } = string.Empty;

        public Guid? LastEditedByPublicId { get; set; }

        public string? LastEditedByName { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
