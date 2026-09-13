using Homassy.Data.Entities.Common;
using Homassy.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Data.Entities.User
{
    public class User : RecordChangeEntity
    {
        /// <summary>
        /// Kratos identity ID (UUID) linking this user to their Kratos identity.
        /// </summary>
        [StringLength(36)]
        public string? KratosIdentityId { get; set; }

        public int? FamilyId { get; set; }

        private string _email = string.Empty;

        /// <summary>
        /// The user's email address, always stored in its canonical form.
        /// </summary>
        /// <remarks>
        /// Normalisation happens here rather than in each write path. It used to be applied by
        /// <c>CreateUserAsync</c>, the profile update and the Kratos sync individually, while the
        /// lookup normalised the argument before comparing — so any row that reached the table by
        /// some other route kept its original casing and could then never be found by email. The
        /// caller saw "no such user" for a user that plainly existed (#136).
        ///
        /// EF writes the backing field directly when it materialises a row, so reading a legacy
        /// mixed-case row does not silently rewrite it; a row saved from code goes through this
        /// setter and comes out canonical.
        /// </remarks>
        [EmailAddress]
        public required string Email
        {
            get => _email;
            set => _email = NormalizeEmail(value);
        }

        /// <summary>
        /// The canonical form of an email address: trimmed and lower-cased. The single definition
        /// of "the same address" — use it for every lookup rather than normalising by hand.
        /// </summary>
        public static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

        [StringLength(128, MinimumLength = 2)]
        public required string Name { get; set; }

        [EnumDataType(typeof(UserStatus))]
        public UserStatus Status { get; set; } = UserStatus.PendingVerification;

        public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public UserProfile? Profile { get; set; }
        public UserNotificationPreferences? NotificationPreferences { get; set; }
        public ICollection<UserPushSubscription> PushSubscriptions { get; set; } = new List<UserPushSubscription>();
    }
}
