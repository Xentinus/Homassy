using Homassy.API.Enums;

namespace Homassy.API.Models.CalendarNote
{
    public class CalendarNoteResponse
    {
        public Guid PublicId { get; set; }
        public DateOnly Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }

        /// <summary>Absolute instant the reminder fires, or null when there is none.</summary>
        public DateTime? ReminderAt { get; set; }

        /// <summary>
        /// The wall-clock time the author picked, as <c>HH:mm</c>. Exposed alongside <see cref="ReminderAt"/>
        /// because a member in another zone rendering the instant locally would see a *different* time than the
        /// author chose — which reads as a bug unless the intent is available too.
        /// </summary>
        public string? ReminderTime { get; set; }

        /// <summary>The zone <see cref="ReminderTime"/> is anchored in, so the UI can qualify it.</summary>
        public UserTimeZone? ReminderTimeZone { get; set; }

        public DateTime? ReminderSentAt { get; set; }

        public Guid AuthorPublicId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public Guid LastEditedByPublicId { get; set; }
        public string LastEditedByName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        /// <summary>Null until the note is actually edited.</summary>
        public DateTime? LastEditedAt { get; set; }

        /// <summary>Opaque concurrency token; echo it back on update.</summary>
        public string Version { get; set; } = string.Empty;
    }
}
