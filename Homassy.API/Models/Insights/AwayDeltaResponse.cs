using Homassy.API.Functions;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One of the people whose activity makes up an away delta, and how much of it was theirs -
    /// so the summary can say "Anna and Bence" rather than just a number.
    /// </summary>
    public record DeltaActor
    {
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = "";

        /// <summary>How many of the counted events were this person's.</summary>
        public int Count { get; init; }
    }

    /// <summary>
    /// What changed in the household while the caller was away (#127) - see
    /// <see cref="InsightFunctions.GetAwayDeltaAsync"/> for how the window is resolved and clamped.
    /// </summary>
    /// <remarks>
    /// <b>The caller's own activity is not in here.</b> "What changed while you were away" that
    /// reports your own actions back at you is worse than saying nothing: it inflates every number
    /// with things you already know about, and it makes the actor list read as if you had been
    /// busy in your own absence.
    /// </remarks>
    public record AwayDeltaResponse
    {
        /// <summary>Inventory items other members added in the window.</summary>
        public int ItemsAdded { get; init; }

        /// <summary>Inventory items other members used up in the window.</summary>
        public int ItemsConsumed { get; init; }

        /// <summary>Shopping-list items other members bought in the window.</summary>
        public int ListItemsPurchased { get; init; }

        /// <summary>
        /// Family-shared inventory items whose expiration date fell inside the window. Not an
        /// activity anyone performed - nobody expires a yoghurt - so it comes from the inventory
        /// itself and is attributed to nobody in <see cref="TopActors"/>.
        /// </summary>
        public int NewExpirations { get; init; }

        /// <summary>
        /// Household membership changes in the window - someone joined, left, or a join request was
        /// handled. Rare, and exactly the kind of thing worth knowing about after a week away.
        /// </summary>
        public int FamilyEvents { get; init; }

        /// <summary>
        /// The sum of the five counts above. Sent rather than left to the client to add up, so
        /// every surface that renders "N changes" agrees on N.
        /// </summary>
        public int Total { get; init; }

        /// <summary>
        /// At most three people, busiest first. Three because the summary is one line: "3 changes
        /// since your last visit · Anna and Bence" stops being a line at four names.
        /// </summary>
        public IReadOnlyList<DeltaActor> TopActors { get; init; } = [];

        /// <summary>
        /// The window's start as actually used - the caller's <c>since</c>, their stored last-seen,
        /// or the 90-day clamp. Echoed back because the client links to the activity timeline with
        /// this exact window, and because a clamped request must not be told it got what it asked
        /// for.
        /// </summary>
        public DateTime Since { get; init; }

        /// <summary>The window's end: the moment the delta was computed.</summary>
        public DateTime Until { get; init; }
    }
}
