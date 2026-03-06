namespace Homassy.API.Models.Statistics;

/// <summary>
/// Global platform statistics cached nightly by the StatisticsRefreshWorker.
/// </summary>
public record GlobalStatisticsResponse
{
    /// <summary>Total number of non-deleted products across all families.</summary>
    public long TotalProducts { get; init; }

    /// <summary>Total number of non-deleted product inventory items.</summary>
    public long TotalInventoryItems { get; init; }

    /// <summary>Total number of non-deleted shopping lists.</summary>
    public long TotalShoppingLists { get; init; }

    /// <summary>Total number of shopping list items that have been purchased.</summary>
    public long TotalPurchasedItems { get; init; }

    /// <summary>Total number of non-deleted shopping locations.</summary>
    public long TotalShoppingLocations { get; init; }

    /// <summary>Total number of non-deleted storage locations.</summary>
    public long TotalStorageLocations { get; init; }

    /// <summary>UTC timestamp of the last successful statistics refresh; null before the first refresh.</summary>
    public DateTime? LastUpdatedUtc { get; init; }
}
