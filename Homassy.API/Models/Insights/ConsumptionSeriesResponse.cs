using Homassy.API.Functions;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// A consumption time series - one <see cref="SeriesPoint"/> per <see cref="Bucket"/>-sized
    /// step across the requested window, dense (see <see cref="SeriesZeroFill.Densify"/> - no
    /// missing days/weeks even when nothing was consumed) and bucketed by the viewer's own
    /// calendar, not the server's. <see cref="InsightFunctions.GetConsumptionSeriesAsync"/> is
    /// what produces this; see its remarks for the timezone and scope reasoning.
    /// </summary>
    public record ConsumptionSeriesResponse
    {
        public IReadOnlyList<SeriesPoint> Points { get; init; } = [];

        public SeriesBucket Bucket { get; init; }

        /// <summary>
        /// The IANA zone id the series was actually bucketed in - always a real, resolvable id,
        /// never the caller's raw (possibly unresolvable) input: falls back to <c>"UTC"</c> rather
        /// than propagating an unknown zone. Echoed back so the chart can label its axis with the
        /// zone it is actually looking at, and so a client can tell a genuine "no timezone on
        /// record" UTC default apart from "your saved zone, resolved".
        /// </summary>
        public string TimeZoneId { get; init; } = "UTC";
    }
}
