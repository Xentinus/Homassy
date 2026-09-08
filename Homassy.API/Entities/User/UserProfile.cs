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
