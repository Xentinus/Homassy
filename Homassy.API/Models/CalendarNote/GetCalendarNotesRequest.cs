using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.CalendarNote
{
    public class GetCalendarNotesRequest
    {
        /// <summary>
        /// Nullable on purpose: <c>[Required]</c> on a non-nullable struct is a no-op, so a missing field would
        /// bind to <c>0001-01-01</c> and sail through <c>ModelState</c>.
        /// </summary>
        [Required]
        public DateOnly? StartDate { get; set; }

        [Required]
        public DateOnly? EndDate { get; set; }
    }
}
