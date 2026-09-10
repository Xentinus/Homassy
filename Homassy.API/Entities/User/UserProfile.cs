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

        /// <summary>
        /// When this user was last seen making an authenticated request, or <see langword="null"/>
        /// if they have not been seen since the column existed (#127).
        /// </summary>
        /// <remarks>
        /// <b>Deliberately approximate.</b> It is stamped in memory by
        /// <see cref="Services.LastSeenTracker"/> on every authenticated request and written here in
        /// batches every few minutes by <see cref="Services.Background.LastSeenFlushService"/> - so
        /// this column can lag the truth by that interval, and a process that stops in between loses
        /// its last batch. That is the trade the away-delta feature is built on: precision nobody can
        /// perceive, in exchange for not adding a row update to the hottest path in the application.
        /// <para>
        /// The client keeps its own per-device last-seen in <c>localStorage</c> and prefers it; this
        /// value exists for the one case that has none - the first launch on a new device.
        /// </para>
        /// </remarks>
        public DateTime? LastSeenAt { get; set; }

        /// <summary>
        /// When this user finished (or skipped) the first-run spotlight tour, or
        /// <see langword="null"/> if they have not yet - which is what makes the tour start
        /// (#98). Replaying the tour from the profile settings clears it back to null.
        /// </summary>
        /// <remarks>
        /// On the user record rather than in <c>localStorage</c> alone so it follows the user to a
        /// new device: the tour explains the app, not this browser, and being walked through the
        /// bottom nav again on every new phone is the kind of thing that reads as a bug.
        /// <para>
        /// A timestamp rather than a bool, because "when" costs the same as "whether" and answers
        /// a question a bool cannot - notably whether a given user saw the tour before or after a
        /// step was added to it.
        /// </para>
        /// </remarks>
        public DateTime? OnboardingCompletedAt { get; set; }

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
