using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.Auth
{
    public class LoginRequest
    {
        [EmailAddress]
        public required string Email { get; set; }
    }
}
