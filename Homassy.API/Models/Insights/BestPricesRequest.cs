using Homassy.API.Controllers;
using Homassy.API.Functions;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// The products a client wants the best known price for, in one call - see
    /// <see cref="InsightsController.GetBestPrices"/>.
    /// </summary>
    /// <remarks>
    /// A POST body rather than a query string, even though the operation only reads: a shopping
    /// list can hold enough items that the id list would run into URL length limits, and the list
    /// is the request's payload, not a filter on a resource path.
    /// <para>
    /// <b>Deviation from the task brief, deliberate.</b> The brief specified
    /// <c>IReadOnlyList&lt;int&gt; ProductIds</c> and an <c>int</c>-keyed response. Both are the
    /// internal primary key, which this codebase never hands out from a public DTO (see the
    /// "BaseEntity" section of <c>Homassy.API/Entities/CLAUDE.md</c>) - and the client has no
    /// internal ids to send in the first place, since every product it holds came from a payload
    /// carrying <c>PublicId</c>. Task 9 made exactly this correction to
    /// <see cref="LocationSpend"/> after the same brief-level slip, so this follows the fix rather
    /// than repeating the mistake.
    /// </para>
    /// </remarks>
    public record BestPricesRequest
    {
        /// <summary>
        /// The products to look up, by public id. Duplicates are harmless (the lookup is a set)
        /// and an empty list is valid, answering an empty map without touching the database. The
        /// upper bound is enforced by <see cref="InsightsController.GetBestPrices"/>, not here: an
        /// unbounded id list is an unbounded <c>IN</c> clause, so the length limit is a request
        /// validation with its own <b>400</b>, not a silent truncation.
        /// </summary>
        public IReadOnlyList<Guid> ProductPublicIds { get; init; } = [];
    }
}
