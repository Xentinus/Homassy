using Homassy.API.Enums;

namespace Homassy.API.Models.Activity
{
    /// <summary>
    /// One row of the activity timeline: either a single activity (<see cref="Count"/> == 1,
    /// <see cref="Items"/> null) or a collapsed run of same-actor, same-type activities recorded
    /// close together in time (<see cref="Count"/> &gt; 1) - e.g. a bulk import that would otherwise
    /// produce dozens of near-identical cards.
    /// </summary>
    public record ActivityTimelineEntry
    {
        /// <summary>Identity for the entry: the run's newest activity's own <c>PublicId</c>.</summary>
        public Guid PublicId { get; init; }

        public Guid UserPublicId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public string? UserProfilePictureUrl { get; init; }
        public string? UserIdentityColor { get; init; }

        /// <summary>The run's newest activity's timestamp (its only timestamp when Count == 1).</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>The run's oldest activity's timestamp, or null when Count == 1.</summary>
        public DateTime? LastTimestamp { get; init; }

        public ActivityType ActivityType { get; init; }

        /// <summary>The run's newest activity's record name.</summary>
        public string RecordName { get; init; } = string.Empty;

        public Unit? Unit { get; init; }
        public decimal? Quantity { get; init; }

        /// <summary>1 for a single activity; &gt;1 for a collapsed run.</summary>
        public int Count { get; init; }

        /// <summary>The run's individual activities, newest first, for the expand-on-tap. Null when Count == 1.</summary>
        public IReadOnlyList<ActivityInfo>? Items { get; init; }
    }
}
