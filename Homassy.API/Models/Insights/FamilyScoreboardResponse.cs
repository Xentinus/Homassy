using Homassy.API.Controllers;
using Homassy.API.Functions;

namespace Homassy.API.Models.Insights
{
    /// <summary>
    /// One family member's contribution over the requested window (#109).
    /// </summary>
    /// <remarks>
    /// <b>Every counter here names something the member did.</b> There is deliberately no field for
    /// anything they did not do - nothing counting items they let expire, lists they left unfinished
    /// or days they were absent - so a client cannot render a shaming metric even by accident. That
    /// is the copy rule made structural rather than left to review, and
    /// <c>InsightsControllerTests</c> asserts it against the serialized JSON.
    /// </remarks>
    public record MemberScore
    {
        /// <summary>The member's public id - the internal key never leaves the server.</summary>
        public Guid PublicId { get; init; }

        public string DisplayName { get; init; } = "";

        /// <summary>
        /// The member's R4 identity colour, or <see langword="null"/> when they have not been
        /// assigned one. Used as an accent only (a ring, a dot, a bar fill), never as a text
        /// colour on an arbitrary background - see the frontend's <c>memberColors</c> rule.
        /// </summary>
        public string? IdentityColor { get; init; }

        /// <summary>Inventory items this member added in the window.</summary>
        public int ItemsAdded { get; init; }

        /// <summary>Inventory items this member used up in the window.</summary>
        public int ItemsConsumed { get; init; }

        /// <summary>
        /// Shopping-list items this member bought in the window.
        /// </summary>
        /// <remarks>
        /// <b>Deviation from the task brief, deliberate:</b> the brief called this
        /// <c>ListsCompleted</c>, which this schema cannot honestly report per member.
        /// <c>ShoppingListItem</c> records <c>PurchasedAt</c> but not <em>who</em> purchased it, so
        /// the only per-member attribution that exists is the <c>ShoppingListItemPurchase</c> /
        /// <c>ShoppingListItemQuickPurchase</c> activity rows - which count items, not lists.
        /// Reporting a count of items under a name that says "lists" would be a number nobody could
        /// reconcile with their own list. Whole lists <em>are</em> reportable family-wide, without
        /// attribution, and that is what <see cref="FamilyScoreboardResponse.ListClearedStreak"/>
        /// does. The same rename applies to <c>BadgeMetric</c>, and
        /// <c>AwayDeltaResponse.ListItemsPurchased</c> (Task 25) already uses this name.
        /// </remarks>
        public int ListItemsPurchased { get; init; }

        /// <summary>
        /// Items this member finished before they could expire - the household's "nothing thrown
        /// away" signal, attributed to whoever actually used the item up.
        /// </summary>
        /// <remarks>
        /// Reported alongside the totals but deliberately <b>not</b> added into
        /// <see cref="CurrentPeriodTotal"/>: every item counted here was also counted in
        /// <see cref="ItemsConsumed"/> (finishing something is consuming it), so adding both would
        /// count the same action twice and quietly rank whoever happens to use up perishables.
        /// </remarks>
        public int WasteAvoided { get; init; }

        /// <summary>
        /// <see cref="ItemsAdded"/> + <see cref="ItemsConsumed"/> + <see cref="ListItemsPurchased"/>
        /// over the window immediately before the current one, of exactly the same length. What the
        /// client compares against to show a direction of travel.
        /// </summary>
        public int PreviousPeriodTotal { get; init; }

        /// <summary>
        /// <see cref="ItemsAdded"/> + <see cref="ItemsConsumed"/> + <see cref="ListItemsPurchased"/>
        /// over the current window - the value members are ranked by. See
        /// <see cref="WasteAvoided"/> for why it is not part of this sum.
        /// </summary>
        public int CurrentPeriodTotal { get; init; }
    }

    /// <summary>
    /// A household streak: how many consecutive days it is on now, and the best it has ever been -
    /// see <see cref="StreakCalculator"/>, including why a run ending yesterday still counts as
    /// current.
    /// </summary>
    public record StreakInfo
    {
        public int Current { get; init; }

        public int Longest { get; init; }
    }

    /// <summary>
    /// The family's scoreboard for the requested window: one entry per member plus two household
    /// streaks - see <see cref="InsightFunctions.GetFamilyScoreboardAsync"/> for how each number is
    /// computed and for the scope rule.
    /// </summary>
    public record FamilyScoreboardResponse
    {
        /// <summary>
        /// Every member of the family, busiest first, including members with nothing at all in the
        /// window: they are part of the household, not absent from it, and a leaderboard that drops
        /// the quiet ones is a leaderboard about who is on it.
        /// </summary>
        public IReadOnlyList<MemberScore> Members { get; init; } = [];

        /// <summary>
        /// Consecutive days on which nothing in the household expired. Household-wide, not
        /// per-member: nothing expiring is something the family achieves together, and there is no
        /// member to attribute a non-event to.
        /// </summary>
        public StreakInfo NoExpiryStreak { get; init; } = new();

        /// <summary>
        /// Consecutive days on which a shopping list was cleared - every item on it bought. Also
        /// household-wide: <c>ShoppingListItem</c> records no purchaser, so a list's completion
        /// belongs to the family rather than to one member.
        /// </summary>
        public StreakInfo ListClearedStreak { get; init; } = new();

        /// <summary>The window's first day, on the caller's own calendar.</summary>
        public DateOnly PeriodStart { get; init; }

        /// <summary>The window's last day (the caller's local today), on the caller's own calendar.</summary>
        public DateOnly PeriodEnd { get; init; }
    }
}
