using Homassy.API.Entities.Common;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// The picture behind an image message (#147).
    /// </summary>
    /// <remarks>
    /// A table of its own, like <c>UserProfilePictures</c> and <c>ProductImages</c>, and for the
    /// sharpest version of that reason: a chat page is dozens of rows, and bytes on the message
    /// row would be bytes in every history response. The message keeps no picture column at all -
    /// the presence of a row here is what makes it an image message, and the URL a client renders
    /// is built from this row's <see cref="StoredImageEntity.Version"/>.
    /// <para>
    /// No navigation property from <c>FamilyChatMessage</c>, deliberately: a navigation is an
    /// invitation to <c>Include()</c> the bytes back into the history query.
    /// </para>
    /// </remarks>
    public class FamilyChatImage : StoredImageEntity
    {
        public int FamilyChatMessageId { get; set; }

        public FamilyChatMessage Message { get; set; } = null!;
    }
}
