using Homassy.API.Models.Common;
using Homassy.API.Models.Internal;

namespace Homassy.Notifications.Services;

/// <summary>
/// Asks Homassy.API which family members are currently watching their chat (#149).
///
/// The "actively watching" flags live in the API's memory, because that is where the SignalR hub
/// is; this worker decides whether to notify. Rather than move the state into the database - where
/// a flag refreshed by a heartbeat would be a write per reader per minute - the question crosses
/// the boundary over the same authenticated internal HTTP path
/// <see cref="InventoryBroadcastServiceClient"/> uses in the other direction.
/// </summary>
public sealed class FamilyChatActivityClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FamilyChatActivityClient> _logger;

    public FamilyChatActivityClient(HttpClient httpClient, IConfiguration configuration, ILogger<FamilyChatActivityClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["HomassyApi:BaseUrl"] ?? "http://homassy-api:8080";
        var apiKey = configuration["InternalApi:ApiKey"] ?? string.Empty;

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    /// <summary>
    /// Which of <paramref name="userIds"/> has at least one connection actively watching the chat.
    /// </summary>
    /// <remarks>
    /// <b>Fails open, deliberately.</b> If the API cannot be reached the answer is "nobody is
    /// active", so everyone gets their notification. The alternative - treating an unknown as
    /// active - would silently swallow messages whenever the two services could not talk, and an
    /// extra notification is a far better failure than a missing one.
    /// </remarks>
    public async Task<HashSet<int>> GetActiveUserIdsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return [];

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/internal/family-chat/active-users",
                new FamilyChatActiveUsersRequest { UserIds = [.. userIds] },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Active chat user lookup failed with {StatusCode}; treating everyone as inactive",
                    response.StatusCode);
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<FamilyChatActiveUsersResponse>>(cancellationToken);
            return body?.Data?.ActiveUserIds is { } active ? [.. active] : [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active chat user lookup failed; treating everyone as inactive");
            return [];
        }
    }
}
