using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// One message in a family's conversation (#144).
    /// </summary>
    /// <remarks>
    /// Deliberately outside the trigger-based in-memory cache system, the same way
    /// <c>UserNotifications</c> is (see <c>Entities/CLAUDE.md</c>): that cache is for slow-changing
    /// master data a whole family reads over and over, and a chat is the opposite - write-heavy,
    /// read as one page when the panel opens, and invalidating nothing anybody caches.
    /// <c>DatabaseTriggerInitializer</c> therefore skips this table by name.
    /// <para>
    /// It still descends from <see cref="RecordChangeEntity"/>, because deleting a message is a
    /// soft delete: the row has to stay so a client that already rendered it can be told it is
    /// gone, and so the read markers in #149 keep pointing at something that exists.
    /// </para>
    /// </remarks>
    public class FamilyChatMessage : RecordChangeEntity
    {
        public int FamilyId { get; set; }

        public int SenderUserId { get; set; }

        public FamilyChatMessageKind Kind { get; set; } = FamilyChatMessageKind.Text;

        /// <summary>
        /// The message text, or an image message's caption.
        /// </summary>
        /// <remarks>
        /// Nullable because an image message may carry no caption at all. A <see cref="Kind"/> of
        /// <see cref="FamilyChatMessageKind.Text"/> always has one - enforced where messages are
        /// written, not by the column, which is shared by both kinds.
        /// </remarks>
        [StringLength(4000)]
        public string? Body { get; set; }

        /// <summary>When the sender sent it, UTC. The stream's sort key, together with the public id.</summary>
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>Set when the message is edited. No endpoint writes it yet; the column exists so editing does not need a migration.</summary>
        public DateTime? EditedAt { get; set; }

        // Navigation properties
        public Family? Family { get; set; }
        public Homassy.API.Entities.User.User? Sender { get; set; }
    }
}
