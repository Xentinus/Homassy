using Homassy.API.Entities.Common;
using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// One thing a chat message points at - a product, a shop, a storage place, a shopping list.
    /// </summary>
    /// <remarks>
    /// <b>A reference plus a snapshot, not one or the other.</b> <see cref="TargetPublicId"/> is
    /// what makes the chip live: the name is resolved when the message is read, so a product
    /// renamed after it was mentioned reads correctly in the old message rather than preserving a
    /// name nobody uses any more. <see cref="Label"/> is what the target was called when it was
    /// sent, and it is the fallback for the case the id alone cannot survive - the product was
    /// deleted, or the reader is a family member who cannot see it. Without the snapshot those
    /// chips would render blank; without the id they would go stale.
    /// <para>
    /// Descends from <see cref="BaseEntity"/> rather than the soft-delete chain: a reference has no
    /// life of its own. It is created with its message, it is hidden when that message is soft
    /// deleted, and it is removed with it when the row finally goes.
    /// </para>
    /// </remarks>
    public class FamilyChatMessageReference : BaseEntity
    {
        public int FamilyChatMessageId { get; set; }

        public FamilyChatReferenceKind Kind { get; set; }

        /// <summary>The referenced entity's own public id - what a tap on the chip navigates to.</summary>
        public Guid TargetPublicId { get; set; }

        /// <summary>What the target was called when the message was sent. See the class remarks.</summary>
        [StringLength(255)]
        public required string Label { get; set; }

        public FamilyChatMessage Message { get; set; } = null!;
    }
}
