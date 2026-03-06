using Homassy.API.Models.Statistics;

namespace Homassy.API.Services;

/// <summary>
/// Singleton in-memory cache for global platform statistics.
/// Updated nightly by <see cref="Background.StatisticsRefreshWorker"/>.
/// Thread-safe via <see langword="volatile"/> field replacement.
/// </summary>
public sealed class StatisticsService
{
    private volatile GlobalStatisticsResponse _current = new();

    /// <summary>Returns the most recently cached statistics snapshot.</summary>
    public GlobalStatisticsResponse GetStatistics() => _current;

    /// <summary>Atomically replaces the cached statistics snapshot.</summary>
    public void UpdateStatistics(GlobalStatisticsResponse statistics) =>
        _current = statistics;
}
