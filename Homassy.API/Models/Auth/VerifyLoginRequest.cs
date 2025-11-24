namespace Homassy.API.Models.Auth
{
    public class VerifyLoginRequest : LoginRequest
    {
        public required string VerificationCode { get; set; }
    }
}
