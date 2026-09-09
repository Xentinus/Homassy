using Homassy.API.Attributes.Validation;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.User
{
    public record UpdateUserSettingsRequest
    {
        [EmailAddress]
        public string? Email { get; init; }

        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? Name { get; init; }

        [StringLength(128, MinimumLength = 2)]
        [SanitizedString]
        public string? DisplayName { get; init; }

        [EnumDataType(typeof(Currency))]
        public Currency? DefaultCurrency { get; init; }

        [EnumDataType(typeof(UserTimeZone))]
        public UserTimeZone? DefaultTimeZone { get; init; }

        [EnumDataType(typeof(Language))]
        public Language? DefaultLanguage { get; init; }

        /// <summary>
        /// Identity-colour key from the curated palette, a custom six-digit hex colour
        /// (<c>#rrggbb</c>, either case — normalised to lowercase before storing; three-digit
        /// shorthand, alpha forms, <c>rgb()</c> and bare colour names are all rejected), the literal
        /// <c>"auto"</c> to clear the override and go back to the deterministic pick, or null to leave
        /// the current value untouched.
        /// </summary>
        public string? IdentityColor { get; init; }
    }
}
