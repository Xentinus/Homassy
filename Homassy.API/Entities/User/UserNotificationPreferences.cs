using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.User
{
    public class UserNotificationPreferences : RecordChangeEntity
    {
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }

        // Email notifications
        public bool EmailNotificationsEnabled { get; set; } = false;
        public bool EmailWeeklySummaryEnabled { get; set; } = false;
        public DateTime? LastWeeklyEmailSentAt { get; set; }

        // Push notifications
        public bool PushNotificationsEnabled { get; set; } = false;
        public bool PushWeeklySummaryEnabled { get; set; } = false;

        /// <summary>
        /// Whether the notification centre records what the workers send to this user (#116).
        /// </summary>
        /// <remarks>
        /// <b>Defaults to on</b>, unlike the push and email switches next to it, and the migration
        /// that shipped the notification centre turned it on for every existing row. Those two
        /// channels interrupt - they ring a phone, they land in a mailbox - so opting in is the
        /// right default for them. The inbox does not: nothing pops up, nothing vibrates, it is a
        /// record of what the app already did, sitting behind a bell the user has to open. A
        /// default of off would have shipped an empty inbox to everyone and made the switch a
        /// thing you had to find before the feature existed at all.
        /// <para>
        /// It was <c>false</c> and unread by anything before #116, which is why the backfill is a
        /// safe overwrite rather than a lost preference: no user had ever been shown a control
        /// that did something.
        /// </para>
        /// </remarks>
        public bool InAppNotificationsEnabled { get; set; } = true;

        // Navigation
        public User User { get; set; } = null!;
    }
}
