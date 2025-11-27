using Homassy.API.Security;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities
{
    public class Family : BaseEntity
    {
        [StringLength(8)]
        public string ShareCode { get; set; } = Cryptography.GenerateShareCode();
        [StringLength(64, MinimumLength = 2)]
        public required string Name { get; set; }
        [StringLength(255)]
        public string? Description { get; set; }
        [Base64String]
        public string? FamilyPictureBase64 { get; set; }
    }
}
