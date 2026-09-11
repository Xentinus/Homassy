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
        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public required string Body { get; set; }

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
