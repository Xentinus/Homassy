using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ExternalCalendar
{
    public class CreateExternalCalendarRequest
    {
        [Required]
        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public required string Name { get; set; }

        [Required]
        [StringLength(2048)]
        public required string ICalUrl { get; set; }

        [StringLength(7)]
        public string Color { get; set; } = "#3B82F6";
    }
}
