using Homassy.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Models.FamilyChat
{
    /// <summary>One thing the sender is attaching to a message.</summary>
    /// <remarks>
    /// Only a kind and an id. The label is <b>not</b> taken from the client: the server resolves it
    /// from what this caller is actually allowed to see, which is the same step that validates the
    /// reference - a client cannot attach a chip naming something it has no access to, nor one
    /// naming it something it is not.
    /// </remarks>
    public class FamilyChatReferenceRequest
    {
        [Required]
        [EnumDataType(typeof(FamilyChatReferenceKind))]
        public required FamilyChatReferenceKind Kind { get; set; }

        [Required]
        public required Guid PublicId { get; set; }
    }
}
