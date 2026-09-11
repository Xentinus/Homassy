using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.FamilyChat
{
    /// <summary>A text message to post to the family's chat (#144).</summary>
    public class SendFamilyChatMessageRequest
    {
        /// <summary>
        /// The message text.
        /// </summary>
        /// <remarks>
        /// Deliberately <b>not</b> <c>[SanitizedString]</c>, which every other free-text field in
        /// the API carries. That attribute rejects a value containing <c>&lt;</c> or <c>&gt;</c>
        /// (and the substring <c>eval(</c>), which is right for a product name and wrong for a
        /// conversation: "5 &lt; 10", "-&gt;" and a line of code are ordinary things to send to
        /// your family, and a chat that refuses them is broken. The safety it buys elsewhere is
        /// bought here instead by how the text is rendered - as text nodes, never <c>v-html</c>,
        /// with links the only markup derived from user input (#147).
        /// </remarks>
        /// <remarks>
        /// Not <c>[Required]</c> any more: a message may be nothing but attached things - "the
        /// shop" and "the milk" with no sentence around them is a complete thought in a family
        /// chat. Emptiness is rejected in the Functions layer, where the references are also known,
        /// rather than by an attribute that can only see this one field.
        /// </remarks>
        [StringLength(4000)]
        public string? Body { get; set; }

        /// <summary>
        /// Things in the app this message points at - a product, a shop, a storage place, a list.
        /// </summary>
        /// <remarks>
        /// Capped low on purpose. A handful of chips under a sentence reads as a message; twenty
        /// reads as a form, and the cap is also what bounds the resolution work a single send can
        /// ask the server for.
        /// </remarks>
        [MaxLength(MaxReferences)]
        public List<FamilyChatReferenceRequest>? References { get; set; }

        /// <summary>How many things one message may point at.</summary>
        public const int MaxReferences = 5;

        /// <summary>
        /// The sender's own id for this message, echoed back on the broadcast.
        /// </summary>
        /// <remarks>
        /// The stream appends optimistically on send and the server broadcasts the committed
        /// message to the whole group - the sender included. Without something to match on, the
        /// sender renders their own message twice. Client-generated and opaque to the API: it is
        /// never stored, only echoed, so two clients colliding on one costs nothing.
        /// </remarks>
        [StringLength(64)]
        public string? CorrelationId { get; set; }
    }
}
