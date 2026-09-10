namespace Homassy.API.Constants
{
    /// <summary>
    /// Which counter a badge is earned against. Each member names something a member of the
    /// household <em>did</em>; there is deliberately no metric for something they did not do, so
    /// the UI cannot render one even by accident (#109's positive-framing rule, made structural
    /// rather than left to copy review).
    /// </summary>
    public enum BadgeMetric
    {
        /// <summary>Inventory items added.</summary>
        ItemsAdded,

        /// <summary>Inventory items consumed (used up rather than thrown away).</summary>
        ItemsConsumed,

        /// <summary>Shopping lists cleared - every item on the list bought.</summary>
        ListsCompleted,

        /// <summary>Items fully consumed before their expiration date, rather than expiring unused.</summary>
        WasteAvoided,

        /// <summary>Consecutive days on which nothing in the household expired.</summary>
        NoExpiryStreakDays,

        /// <summary>Consecutive days on which the shopping list was cleared.</summary>
        ListClearedStreakDays
    }

    /// <summary>
    /// One badge: what it is called, what it is earned against, and everything a client needs to
    /// render it - including English fallback text, so a badge added here needs no frontend release.
    /// </summary>
    public sealed record BadgeDefinition
    {
        /// <summary>
        /// Stable, permanent id (e.g. <c>"items-added-100"</c>). Reaches the client, ends up in
        /// i18n keys and DOM ids, and is stored on every <see cref="Entities.User.UserBadge"/> row.
        /// </summary>
        public required string Id { get; init; }

        public required BadgeMetric Metric { get; init; }

        /// <summary>The counter value at which this badge is earned. Always greater than zero.</summary>
        public required int Threshold { get; init; }

        /// <summary>A lucide icon id, e.g. <c>"i-lucide-package-plus"</c>.</summary>
        public required string IconName { get; init; }

        /// <summary>The i18n key a client should prefer for the title, e.g. <c>"badges.itemsAdded100.title"</c>.</summary>
        public required string TitleKey { get; init; }

        public required string DescriptionKey { get; init; }

        /// <summary>
        /// English title shipped by the server, used whenever the client has no translation for
        /// <see cref="TitleKey"/>. This is what makes "a new badge needs no frontend release" true
        /// rather than aspirational: without it, a badge the client has never heard of renders as
        /// a blank tile.
        /// </summary>
        public required string FallbackTitle { get; init; }

        /// <summary>English description, for the same reason as <see cref="FallbackTitle"/>.</summary>
        public required string FallbackDescription { get; init; }
    }

    /// <summary>
    /// Every badge the server knows about. Code, not a table - a badge has no per-family
    /// configuration and nothing about it is user data, so a migration to add one would be pure
    /// ceremony. <see cref="Entities.User.UserBadge"/> stores only <em>which</em> badge a user
    /// earned, by id.
    ///
    /// <para>
    /// <b>Two invariants a future editor must preserve.</b>
    /// </para>
    /// <para>
    /// 1. <b>Ids are permanent.</b> Every earned row stores <see cref="BadgeDefinition.Id"/> as a
    /// string, so renaming one does not migrate anything - it orphans every row that carries the
    /// old id (the badge silently un-earns for everyone who had it) and, worse, re-grants the
    /// "new" badge with a fresh unlock celebration. Add a new id instead; never edit one.
    /// </para>
    /// <para>
    /// 2. <b>Thresholds only move if you accept re-granting or revoking retroactively.</b>
    /// Lowering one hands the badge to everyone already above the new line, each with a
    /// celebration. Raising one leaves rows earned under the old threshold in place while the
    /// progress ring reads as incomplete - the row is the earned state, and nothing revokes it.
    /// Neither is wrong, but both are product decisions, not tuning.
    /// </para>
    ///
    /// <para>
    /// Every title is recognition for something done. That is the whole of #109's copy rule, and it
    /// applies to the fallback text here just as much as to the translations, because this text is
    /// what a client without the key actually shows.
    /// </para>
    /// </summary>
    public static class BadgeCatalog
    {
        /// <summary>
        /// Three tiers per metric - enough that the first is reachable in a week and the last is a
        /// genuine milestone, few enough that the grid stays readable and every tile means
        /// something. Ordered by metric, then by threshold.
        /// </summary>
        public static IReadOnlyList<BadgeDefinition> All { get; } =
        [
            new BadgeDefinition
            {
                Id = "items-added-25",
                Metric = BadgeMetric.ItemsAdded,
                Threshold = 25,
                IconName = "i-lucide-package-plus",
                TitleKey = "badges.itemsAdded25.title",
                DescriptionKey = "badges.itemsAdded25.description",
                FallbackTitle = "Stocking up",
                FallbackDescription = "Added 25 items to the household inventory."
            },
            new BadgeDefinition
            {
                Id = "items-added-100",
                Metric = BadgeMetric.ItemsAdded,
                Threshold = 100,
                IconName = "i-lucide-package-plus",
                TitleKey = "badges.itemsAdded100.title",
                DescriptionKey = "badges.itemsAdded100.description",
                FallbackTitle = "Well stocked",
                FallbackDescription = "Added 100 items to the household inventory."
            },
            new BadgeDefinition
            {
                Id = "items-added-500",
                Metric = BadgeMetric.ItemsAdded,
                Threshold = 500,
                IconName = "i-lucide-warehouse",
                TitleKey = "badges.itemsAdded500.title",
                DescriptionKey = "badges.itemsAdded500.description",
                FallbackTitle = "Quartermaster",
                FallbackDescription = "Added 500 items to the household inventory."
            },

            new BadgeDefinition
            {
                Id = "items-consumed-25",
                Metric = BadgeMetric.ItemsConsumed,
                Threshold = 25,
                IconName = "i-lucide-utensils",
                TitleKey = "badges.itemsConsumed25.title",
                DescriptionKey = "badges.itemsConsumed25.description",
                FallbackTitle = "Put to good use",
                FallbackDescription = "Used up 25 items from the inventory."
            },
            new BadgeDefinition
            {
                Id = "items-consumed-100",
                Metric = BadgeMetric.ItemsConsumed,
                Threshold = 100,
                IconName = "i-lucide-utensils",
                TitleKey = "badges.itemsConsumed100.title",
                DescriptionKey = "badges.itemsConsumed100.description",
                FallbackTitle = "Nothing left over",
                FallbackDescription = "Used up 100 items from the inventory."
            },
            new BadgeDefinition
            {
                Id = "items-consumed-500",
                Metric = BadgeMetric.ItemsConsumed,
                Threshold = 500,
                IconName = "i-lucide-chef-hat",
                TitleKey = "badges.itemsConsumed500.title",
                DescriptionKey = "badges.itemsConsumed500.description",
                FallbackTitle = "Kitchen regular",
                FallbackDescription = "Used up 500 items from the inventory."
            },

            new BadgeDefinition
            {
                Id = "lists-completed-5",
                Metric = BadgeMetric.ListsCompleted,
                Threshold = 5,
                IconName = "i-lucide-clipboard-check",
                TitleKey = "badges.listsCompleted5.title",
                DescriptionKey = "badges.listsCompleted5.description",
                FallbackTitle = "List cleared",
                FallbackDescription = "Bought everything on 5 shopping lists."
            },
            new BadgeDefinition
            {
                Id = "lists-completed-25",
                Metric = BadgeMetric.ListsCompleted,
                Threshold = 25,
                IconName = "i-lucide-clipboard-check",
                TitleKey = "badges.listsCompleted25.title",
                DescriptionKey = "badges.listsCompleted25.description",
                FallbackTitle = "Reliable shopper",
                FallbackDescription = "Bought everything on 25 shopping lists."
            },
            new BadgeDefinition
            {
                Id = "lists-completed-100",
                Metric = BadgeMetric.ListsCompleted,
                Threshold = 100,
                IconName = "i-lucide-shopping-cart",
                TitleKey = "badges.listsCompleted100.title",
                DescriptionKey = "badges.listsCompleted100.description",
                FallbackTitle = "Household logistics",
                FallbackDescription = "Bought everything on 100 shopping lists."
            },

            new BadgeDefinition
            {
                Id = "waste-avoided-10",
                Metric = BadgeMetric.WasteAvoided,
                Threshold = 10,
                IconName = "i-lucide-leaf",
                TitleKey = "badges.wasteAvoided10.title",
                DescriptionKey = "badges.wasteAvoided10.description",
                FallbackTitle = "In time",
                FallbackDescription = "Used up 10 items before they could expire."
            },
            new BadgeDefinition
            {
                Id = "waste-avoided-50",
                Metric = BadgeMetric.WasteAvoided,
                Threshold = 50,
                IconName = "i-lucide-leaf",
                TitleKey = "badges.wasteAvoided50.title",
                DescriptionKey = "badges.wasteAvoided50.description",
                FallbackTitle = "Nothing wasted",
                FallbackDescription = "Used up 50 items before they could expire."
            },
            new BadgeDefinition
            {
                Id = "waste-avoided-200",
                Metric = BadgeMetric.WasteAvoided,
                Threshold = 200,
                IconName = "i-lucide-sprout",
                TitleKey = "badges.wasteAvoided200.title",
                DescriptionKey = "badges.wasteAvoided200.description",
                FallbackTitle = "Zero-waste household",
                FallbackDescription = "Used up 200 items before they could expire."
            },

            new BadgeDefinition
            {
                Id = "no-expiry-streak-7",
                Metric = BadgeMetric.NoExpiryStreakDays,
                Threshold = 7,
                IconName = "i-lucide-calendar-check",
                TitleKey = "badges.noExpiryStreak7.title",
                DescriptionKey = "badges.noExpiryStreak7.description",
                FallbackTitle = "A clean week",
                FallbackDescription = "Seven days in a row with nothing expiring."
            },
            new BadgeDefinition
            {
                Id = "no-expiry-streak-30",
                Metric = BadgeMetric.NoExpiryStreakDays,
                Threshold = 30,
                IconName = "i-lucide-calendar-check",
                TitleKey = "badges.noExpiryStreak30.title",
                DescriptionKey = "badges.noExpiryStreak30.description",
                FallbackTitle = "A clean month",
                FallbackDescription = "Thirty days in a row with nothing expiring."
            },
            new BadgeDefinition
            {
                Id = "no-expiry-streak-100",
                Metric = BadgeMetric.NoExpiryStreakDays,
                Threshold = 100,
                IconName = "i-lucide-shield-check",
                TitleKey = "badges.noExpiryStreak100.title",
                DescriptionKey = "badges.noExpiryStreak100.description",
                FallbackTitle = "Hundred clean days",
                FallbackDescription = "A hundred days in a row with nothing expiring."
            },

            new BadgeDefinition
            {
                Id = "list-cleared-streak-3",
                Metric = BadgeMetric.ListClearedStreakDays,
                Threshold = 3,
                IconName = "i-lucide-flame",
                TitleKey = "badges.listClearedStreak3.title",
                DescriptionKey = "badges.listClearedStreak3.description",
                FallbackTitle = "On a roll",
                FallbackDescription = "Cleared the shopping list three days running."
            },
            new BadgeDefinition
            {
                Id = "list-cleared-streak-14",
                Metric = BadgeMetric.ListClearedStreakDays,
                Threshold = 14,
                IconName = "i-lucide-flame",
                TitleKey = "badges.listClearedStreak14.title",
                DescriptionKey = "badges.listClearedStreak14.description",
                FallbackTitle = "Two weeks running",
                FallbackDescription = "Cleared the shopping list fourteen days running."
            },
            new BadgeDefinition
            {
                Id = "list-cleared-streak-60",
                Metric = BadgeMetric.ListClearedStreakDays,
                Threshold = 60,
                IconName = "i-lucide-trophy",
                TitleKey = "badges.listClearedStreak60.title",
                DescriptionKey = "badges.listClearedStreak60.description",
                FallbackTitle = "Never caught short",
                FallbackDescription = "Cleared the shopping list sixty days running."
            }
        ];

        private static readonly Dictionary<string, BadgeDefinition> ById_ =
            All.ToDictionary(badge => badge.Id, StringComparer.Ordinal);

        /// <summary>
        /// The definition with this id, or <see langword="null"/> when the catalog has never heard
        /// of it. Null rather than a throw on purpose: the one caller that matters is reading badge
        /// ids back out of the database, where an id retired from the catalog is a perfectly
        /// ordinary thing to find and the right response is to ignore that row, not to fail the
        /// whole response.
        /// </summary>
        public static BadgeDefinition? ById(string id) =>
            ById_.TryGetValue(id, out var definition) ? definition : null;
    }
}
