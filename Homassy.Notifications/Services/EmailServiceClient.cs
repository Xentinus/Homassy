using Homassy.API.Enums;
using Homassy.Notifications.Models;
using System.Net.Http.Json;

namespace Homassy.Notifications.Services;

public sealed class EmailServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailServiceClient> _logger;

    public EmailServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<EmailServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["EmailService:BaseUrl"] ?? "http://homassy-email:8080";
        var apiKey = configuration["EmailService:ApiKey"] ?? string.Empty;

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<bool> SendWeeklySummaryAsync(
        string to,
        Language language,
        string? name,
        List<ExpiringProductItem> expiredItems,
        List<ExpiringProductItem> expiringSoonItems,
        CancellationToken ct = default)
    {
        var request = new WeeklySummaryEmailRequest
        {
            To = to,
            Language = GetLanguageCode(language),
            Name = name,
            ExpiredItems = expiredItems
                .Select(i => new ExpiringProductDto(i.ProductName, i.Brand, i.ExpirationDate))
                .ToList(),
            ExpiringSoonItems = expiringSoonItems
                .Select(i => new ExpiringProductDto(i.ProductName, i.Brand, i.ExpirationDate))
                .ToList()
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/email/weekly-summary", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Email service returned {StatusCode} for weekly summary to {Email}: {Body}",
                    response.StatusCode, to, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send weekly summary email to {Email}", to);
            return false;
        }
    }

    private static string GetLanguageCode(Language language) => language switch
    {
        Language.German => "de",
        Language.English => "en",
        _ => "hu"
    };
}
