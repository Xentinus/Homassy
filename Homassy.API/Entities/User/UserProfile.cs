using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserProfile : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        [StringLength(128, MinimumLength = 2)]
        public required string DisplayName { get; set; }

        /// <summary>
        /// Content hash of the avatar in <see cref="UserProfilePicture"/>, or null when the user
        /// has none. Kept here so building an avatar URL costs nothing beyond the profile row the
        /// cache already holds — the bytes themselves are never loaded to render a list.
        /// </summary>
        [StringLength(32)]
        public string? ProfilePictureVersion { get; set; }

        /// <summary>
        /// The member's chosen identity colour: one of the app's curated palette keys (see the web
        /// project's <c>utils/memberColors.ts</c>), a custom colour stored as a lower-cased six-digit
        /// hex string (<c>#rrggbb</c>), or null to use the deterministic pick derived from
        /// <see cref="Common.BaseEntity.PublicId"/> (the owning <c>User</c>'s public id). A hex value's
        /// hue is whatever the member picked; the contrast guarantee for it comes from the web
        /// client adjusting lightness per theme at render time (<c>resolveMemberColor</c>), not from
        /// restricting storage to the palette. <c>[StringLength(24)]</c> already comfortably fits the
        /// 7-character <c>#rrggbb</c> form alongside the palette keys, so no migration was needed to
        /// accept it. Lives on the profile rather than in local storage so the choice follows the
        /// user across devices.
        /// </summary>
        [StringLength(24)]
        public string? IdentityColor { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }

        [EnumDataType(typeof(Currency))]
        public Currency DefaultCurrency { get; set; } = Currency.Huf;

        [EnumDataType(typeof(UserTimeZone))]
        public UserTimeZone DefaultTimeZone { get; set; } = UserTimeZone.CentralEuropeStandardTime;

        [EnumDataType(typeof(Language))]
        public Language DefaultLanguage { get; set; } = Language.Hungarian;

        // Navigation
        public User User { get; set; } = null!;
    }
}
