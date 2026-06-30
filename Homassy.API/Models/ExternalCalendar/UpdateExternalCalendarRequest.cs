using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.ExternalCalendar
{
    public class UpdateExternalCalendarRequest
    {
        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public string? Name { get; set; }

        [StringLength(2048)]
        public string? ICalUrl { get; set; }

        [StringLength(7)]
        public string? Color { get; set; }

        public bool? IsEnabled { get; set; }
    }
}
