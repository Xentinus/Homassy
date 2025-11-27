using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.User
{
    public class CreateUserRequest
    {
        [EmailAddress]
        public required string Email { get; set; }
        [StringLength(64, MinimumLength = 2)]
        public required string Name { get; set; }
        [StringLength(64, MinimumLength = 2)]
        public string? DisplayName { get; set; }
    }
}
