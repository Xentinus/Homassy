using Homassy.API.Attributes.Validation;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.User
{
    public class CreateUserRequest
    {
        [EmailAddress]
        public required string Email { get; set; }

        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public required string Name { get; set; }

        [StringLength(64, MinimumLength = 2)]
        [SanitizedString]
        public string? DisplayName { get; set; }
    }
}
