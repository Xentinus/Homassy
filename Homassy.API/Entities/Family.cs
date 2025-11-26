using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Entities
{
    public class Family : BaseEntity
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        [Base64String]
        public string? FamilyPictureBase64 { get; set; }
    }
}
