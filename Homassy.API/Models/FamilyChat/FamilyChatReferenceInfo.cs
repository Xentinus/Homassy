using Homassy.API.Enums;

namespace Homassy.API.Models.FamilyChat
{
    /// <summary>Something a message points at, as the stream renders it: a chip under the text.</summary>
    public record FamilyChatReferenceInfo
    {
        public FamilyChatReferenceKind Kind { get; init; }

        /// <summary>The referenced entity's public id - what a tap on the chip opens.</summary>
        public Guid PublicId { get; init; }

        /// <summary>
        /// What to call it: the target's current name, or the name it had when it was sent.
        /// </summary>
        /// <remarks>
        /// Resolved fresh on every read, so a product renamed after it was mentioned reads
        /// correctly in the old message. The stored snapshot only steps in when the target cannot
        /// be resolved at all - see <see cref="IsAvailable"/>.
        /// </remarks>
        public string Label { get; init; } = string.Empty;

        /// <summary>
        /// False when the target no longer resolves for this reader - deleted, or not theirs to
        /// see.
        /// </summary>
        /// <remarks>
        /// The chip is still rendered, with the snapshot label, because "Anna asked about Tejföl"
        /// stays a readable sentence after the product is gone. What the client drops is the link:
        /// a chip that navigates to a 404 is worse than one that does nothing.
        /// </remarks>
        public bool IsAvailable { get; init; }
    }
}
