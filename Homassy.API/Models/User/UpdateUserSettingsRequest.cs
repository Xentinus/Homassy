using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.User
{
    public record UpdateUserSettingsRequest
    {
        [EmailAddress]
        public string? Email { get; init; }

        [StringLength(128, MinimumLength = 2)]
        public string? Name { get; init; }

        [StringLength(128, MinimumLength = 2)]
        public string? DisplayName { get; init; }

        [EnumDataType(typeof(Currency))]
        public Currency? DefaultCurrency { get; init; }

        [EnumDataType(typeof(UserTimeZone))]
        public UserTimeZone? DefaultTimeZone { get; init; }

        [EnumDataType(typeof(Language))]
        public Language? DefaultLanguage { get; init; }
    }
}
