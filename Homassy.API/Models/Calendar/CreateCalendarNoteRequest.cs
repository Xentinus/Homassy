using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Calendar
{
    public class CreateCalendarNoteRequest
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
        /// When the family should be reminded, UTC. Null means no reminder; the client sends the
        /// instant it wants rather than a lead time, because a note has no start time to lead from.
        /// </summary>
        public DateTime? ReminderAt { get; set; }
    }
}
