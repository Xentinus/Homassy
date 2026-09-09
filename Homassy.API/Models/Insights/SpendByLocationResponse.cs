using Homassy.API.Controllers;
using Homassy.API.Enums;
using Homassy.API.Functions;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One shopping location's purchases within the requested window, as returned by
    /// <see cref="SpendByLocationResponse"/>. See
    /// <see cref="InsightFunctions.GetSpendByLocationAsync"/> for the scope rule and
    /// <see cref="InsightFunctions"/>'s private <c>ComputeSpendByLocationAsync</c> for
    /// exactly how the three data rules below are enforced.
    /// </summary>
    public record LocationSpend
    {
        /// <summary>
        /// The location's public id, or <see langword="null"/> for the single "unknown location"
        /// bucket every purchase with no shopping location tag folds into - never dropped, and
        /// never split across more than one such bucket. Matches this codebase's convention of
        /// exposing <c>PublicId</c> rather than the internal primary key in public DTOs (see the
        /// "BaseEntity" section of <c>Homassy.API/Entities/CLAUDE.md</c>) - deliberately nullable, unlike most other public ids
        /// in this API, because the "unknown location" bucket has no location row to carry one
        /// (Fix round 1: this was originally the raw internal <c>int? ShoppingLocationId</c>, a
        /// deviation from that convention the brief specified verbatim - see this task's report).
        /// </summary>
        public Guid? ShoppingLocationPublicId { get; init; }

        /// <summary>
        /// The location's display name, or a fixed "unknown location" label when
        /// <see cref="ShoppingLocationPublicId"/> is <see langword="null"/> (or, defensively,
        /// when a non-null id can no longer be resolved to a location row).
        /// </summary>
        public string LocationName { get; init; } = "";

        /// <summary>
        /// Every purchase recorded at this location in the window, regardless of whether it has a
        /// <c>Price</c> - unlike <see cref="SpendByCurrency"/>, a missing price never excludes a
        /// purchase from this count.
        /// </summary>
        public int ItemCount { get; init; }

        /// <summary>
        /// Total spend per currency actually recorded at this location. Never summed across
        /// currencies and never converted between them - this milestone has no exchange rate to
        /// convert with, so two currencies at the same location are always two separate entries
        /// here, never one combined number. A purchase with a <see langword="null"/>
        /// <c>Price</c> contributes nothing to this dictionary - not even a zero-value entry -
        /// even though it is still counted in <see cref="ItemCount"/>.
        /// </summary>
        public IReadOnlyDictionary<Currency, decimal> SpendByCurrency { get; init; } = new Dictionary<Currency, decimal>();
    }

    /// <summary>
    /// The caller's purchases within the requested window, broken down by shopping location - see
    /// <see cref="InsightFunctions.GetSpendByLocationAsync"/> for the scope rule (the
    /// same personal-plus-family union <see cref="Functions.ProductFunctions.GetInventoryItemsByUserAndFamily"/>
    /// uses) and the window-length validation performed by
    /// <see cref="InsightsController.GetSpendByLocation"/>.
    /// </summary>
    public record SpendByLocationResponse
    {
        public IReadOnlyList<LocationSpend> Locations { get; init; } = [];
    }
}
