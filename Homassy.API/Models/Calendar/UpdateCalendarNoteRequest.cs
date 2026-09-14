using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Calendar
{
    public class UpdateCalendarNoteRequest
    {
        [Required]
        public required DateOnly Date { get; set; }

        [Required]
        [StringLength(120, MinimumLength = 1)]
        [SanitizedString]
        public required string Title { get; set; }

        [StringLength(2000)]
        [SanitizedString]
        public string? Content { get; set; }

        /// <summary>
        /// The new reminder instant, UTC, or null to disarm the reminder. Moving it re-arms a
        /// reminder that has already been sent.
        /// </summary>
        public DateTime? ReminderAt { get; set; }
    }
}
