using Homassy.Data.Entities.Common;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Homassy.Data.Security;

namespace Homassy.Data.Entities.Family
{
    public class Family : RecordChangeEntity
    {
        [StringLength(8)]
        public string ShareCode { get; set; } = Cryptography.GenerateShareCode();
        [StringLength(64, MinimumLength = 2)]
        public required string Name { get; set; }
        [StringLength(255)]
        public string? Description { get; set; }
        /// <summary>
        /// Content hash of the picture in <see cref="FamilyPicture"/>, or null when the family has
        /// none. Kept here so building an image URL costs nothing beyond the cached family row.
        /// </summary>
        [StringLength(32)]
        public string? FamilyPictureVersion { get; set; }
    }
}
