using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    /// <summary>
    /// One badge one user has earned, and when. The only durable piece of the R5 gamification
    /// feature (#109): counters, streaks and progress are all derived from activity rows and are
    /// re-computed (and cached) on demand, but "has this user already been told about this badge"
    /// cannot be derived from anything - and it has to be right exactly once, because it is what
    /// decides whether the unlock celebration fires.
    /// </summary>
    /// <remarks>
    /// A row here means earned, full stop; there is no "unearned" row and nothing ever downgrades
    /// one. <see cref="BadgeId"/> is <see cref="Constants.BadgeCatalog"/>'s own string id rather
    /// than a foreign key, because the catalog is code, not a table - see that class's remarks for
    /// the two invariants that keeps honest. The uniqueness of (user, badge) is enforced by an
    /// index in <see cref="Context.HomassyDbContext.OnModelCreating"/>, not only by the code that
    /// inserts: two concurrent requests both crossing the same threshold is an ordinary race, and
    /// the database refusing the second insert is what makes double-granting impossible rather
    /// than merely unlikely.
    /// </remarks>
    public class UserBadge : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        /// <summary>
        /// The catalog id (e.g. <c>"items-added-100"</c>). Permanent: changing an id in the catalog
        /// orphans every row that carries the old one - see <see cref="Constants.BadgeCatalog"/>.
        /// </summary>
        [Required]
        [StringLength(64)]
        public required string BadgeId { get; set; }

        /// <summary>
        /// When the threshold was first crossed - or rather, when the server first noticed it was.
        /// Stamped once at insert and never updated, since it is what the UI shows as "earned on".
        /// </summary>
        public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
    }
}
