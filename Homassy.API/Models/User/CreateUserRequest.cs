using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.User
{
    public class CreateUserRequest
    {
        [EmailAddress]
        public required string Email { get; set; }

        public required string Name { get; set; }

        public string? DisplayName { get; set; }
    }
}
