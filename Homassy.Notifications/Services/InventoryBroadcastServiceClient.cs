using Homassy.API.Models.Internal;
using Homassy.API.Models.Product;

namespace Homassy.Notifications.Services;

/// <summary>
/// Calls the Homassy.API internal broadcast endpoint so inventory changes made in this out-of-process
/// worker (e.g. automation auto-consume) are pushed live to connected clients over SignalR. The API
/// hosts the hub; this service cannot reach it in-process, so it relays over authenticated HTTP
/// (mirrors <see cref="EmailServiceClient"/> / the API's own <c>NotificationsServiceClient</c>).
/// </summary>
public sealed class InventoryBroadcastServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InventoryBroadcastServiceClient> _logger;

    public InventoryBroadcastServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<InventoryBroadcastServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["HomassyApi:BaseUrl"] ?? "http://homassy-api:8080";
        var apiKey = configuration["InternalApi:ApiKey"] ?? string.Empty;

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    /// <summary>Pushes an item create/update. Safe: logs and swallows failures so it never breaks the worker.</summary>
    public Task BroadcastUpsertAsync(int userId, int? familyId, InventoryGridProductInfo product, InventoryGridItemInfo item, CancellationToken cancellationToken = default)
        => SendAsync(new InventoryBroadcastRequest
        {
            EventType = "upserted",
            UserId = userId,
            FamilyId = familyId,
            SharedWithFamily = item.IsSharedWithFamily,
            Product = product,
            Item = item
        }, cancellationToken);

    /// <summary>Pushes an item removal / full-consumption. Safe: logs and swallows failures.</summary>
    public Task BroadcastDeleteAsync(int userId, int? familyId, bool sharedWithFamily, Guid productPublicId, Guid itemPublicId, CancellationToken cancellationToken = default)
        => SendAsync(new InventoryBroadcastRequest
        {
            EventType = "deleted",
            UserId = userId,
            FamilyId = familyId,
            SharedWithFamily = sharedWithFamily,
            ProductPublicId = productPublicId,
            ItemPublicId = itemPublicId
        }, cancellationToken);

    private async Task SendAsync(InventoryBroadcastRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/internal/inventory/broadcast", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Inventory broadcast relay returned {StatusCode}: {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // A broadcast failure must never break the worker.
            _logger.LogError(ex, "Failed to relay inventory {EventType} broadcast to the API", request.EventType);
        }
    }
}
