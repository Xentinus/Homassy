using Homassy.API.Constants;
using Homassy.API.Functions;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One badge as the caller currently stands with it: everything needed to render the tile, plus
    /// how far along they are and whether this is the response that first reports it as earned.
    /// </summary>
    /// <remarks>
    /// The definition fields are copied from <see cref="BadgeDefinition"/> rather than referenced by
    /// id, so a client can render a badge it has never heard of - which is what makes "a new badge
    /// needs no frontend release" true. See <see cref="FallbackTitle"/>.
    /// </remarks>
    public record BadgeState
    {
        /// <summary>The catalog's stable id - also the client's key for i18n and DOM ids.</summary>
        public string Id { get; init; } = "";

        public string IconName { get; init; } = "";

        public string TitleKey { get; init; } = "";

        public string DescriptionKey { get; init; } = "";

        /// <summary>
        /// English title shipped by the server, for a client with no translation for
        /// <see cref="TitleKey"/> - without it a badge added server-side renders as a blank tile.
        /// </summary>
        public string FallbackTitle { get; init; } = "";

        public string FallbackDescription { get; init; } = "";

        /// <summary>The counter value at which this badge is earned.</summary>
        public int Threshold { get; init; }

        /// <summary>
        /// How far the caller has got, <b>capped at <see cref="Threshold"/></b> so a progress ring
        /// can be drawn as <c>Progress / Threshold</c> without ever overflowing past full.
        /// </summary>
        public int Progress { get; init; }

        /// <summary>
        /// When the badge was earned, or <see langword="null"/> while it is still locked. This -
        /// not <see cref="Progress"/> - is what "earned" means: the row exists.
        /// </summary>
        public DateTime? EarnedAt { get; init; }

        /// <summary>
        /// <see langword="true"/> only on the response that first reports this badge as earned, so
        /// the client fires its unlock celebration exactly once. Every later response carries the
        /// same <see cref="EarnedAt"/> with this back to <see langword="false"/> - see
        /// <see cref="InsightFunctions.GetBadgeStateAsync"/>, and the test that reloads the endpoint
        /// specifically to stop the confetti firing on every page load.
        /// </summary>
        public bool JustUnlocked { get; init; }
    }

    /// <summary>
    /// Every badge in the catalog, in catalog order, locked and unlocked alike - a locked badge
    /// still carries its title and threshold, because a mystery box tells the family nothing about
    /// what to aim for.
    /// </summary>
    public record BadgeStateResponse
    {
        public IReadOnlyList<BadgeState> Badges { get; init; } = [];
    }
}
