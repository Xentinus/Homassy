namespace Homassy.API.Services.Sanitization
{
    public interface IInputSanitizationService
    {
        string SanitizePlainText(string? input);

        string? SanitizePlainTextOrNull(string? input);
    }
}
