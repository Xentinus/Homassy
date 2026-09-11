using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.FamilyChat
{
    /// <summary>A picture to post to the family's chat (#147).</summary>
    /// <remarks>
    /// Base64 in JSON, like every other upload in this API (<c>UploadProductImageRequest</c>,
    /// <c>UploadUserProfileImageRequest</c>) - the client already holds a cropped data URL, and one
    /// shape of upload across the API is worth more than the third of a request this costs. What is
    /// deliberately <b>not</b> base64 is the answer: the message that comes back carries an image
    /// URL, so a conversation of a hundred pictures is a hundred cacheable requests rather than a
    /// hundred blobs inside a history page.
    /// </remarks>
    public class SendFamilyChatImageRequest
    {
        [Required]
        [Base64String]
        public required string ImageBase64 { get; set; }

        /// <summary>
        /// Optional text under the picture.
        /// </summary>
        /// <remarks>
        /// Not <c>[SanitizedString]</c>, for the same reason a text message's body is not - see
        /// <see cref="SendFamilyChatMessageRequest.Body"/>.
        /// </remarks>
        [StringLength(500)]
        public string? Caption { get; set; }

        /// <summary>The sender's own id for this message, echoed back on the broadcast.</summary>
        [StringLength(64)]
        public string? CorrelationId { get; set; }
    }
}
