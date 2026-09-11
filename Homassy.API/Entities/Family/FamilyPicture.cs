using Homassy.API.Entities.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// A family's picture. One row per family at most; see <see cref="StoredImageEntity"/> for why
    /// the bytes do not live on <see cref="Family"/>.
    /// </summary>
    /// <remarks>
    /// The reason bites harder here than anywhere else: <c>Family</c> rows are held in
    /// <c>FamilyFunctions</c>' process-wide cache in full, so the base64 column this replaces was a
    /// picture resident in memory for every family on the instance, and it rode along in every
    /// family payload - including the one the chat bubble fetches on each load.
    /// </remarks>
    public class FamilyPicture : StoredImageEntity
    {
        [ForeignKey(nameof(Family))]
        public int FamilyId { get; set; }

        public Family Family { get; set; } = null!;
    }
}
