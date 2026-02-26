using System.Net.Http.Json;

namespace Homassy.API.Infrastructure;

public sealed class NotificationsServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationsServiceClient> _logger;

    public NotificationsServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<NotificationsServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["NotificationsService:BaseUrl"] ?? "http://homassy-notifications:8080";
        var apiKey = configuration["NotificationsService:ApiKey"] ?? string.Empty;

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<bool> SendTestPushAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/push/test", new { userId }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Notifications service returned {StatusCode} for test push to user {UserId}: {Body}",
                    response.StatusCode, userId, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test push notification for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> SendTestEmailAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/email/test", new { userId }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Notifications service returned {StatusCode} for test email to user {UserId}: {Body}",
                    response.StatusCode, userId, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email for user {UserId}", userId);
            return false;
        }
    }
}
