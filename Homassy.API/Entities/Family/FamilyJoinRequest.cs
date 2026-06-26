using Homassy.API.Entities.Common;
using Homassy.API.Enums;

namespace Homassy.API.Entities.Family
{
    /// <summary>
    /// A request by a user to join a family. Joining requires approval from an existing
    /// family member; a user may only have one <see cref="FamilyJoinRequestStatus.Pending"/>
    /// request at a time (enforced by a filtered unique index).
    /// </summary>
    public class FamilyJoinRequest : RecordChangeEntity
    {
        /// <summary>The user who wants to join.</summary>
        public int UserId { get; set; }

        /// <summary>The family the user wants to join.</summary>
        public int FamilyId { get; set; }

        public FamilyJoinRequestStatus Status { get; set; } = FamilyJoinRequestStatus.Pending;

        /// <summary>When the request was sent.</summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the request was approved/rejected (null while pending/cancelled).</summary>
        public DateTime? RespondedAt { get; set; }

        /// <summary>The family member who approved/rejected the request.</summary>
        public int? RespondedByUserId { get; set; }

        // Navigation properties
        public Homassy.API.Entities.User.User? User { get; set; }
        public Family? Family { get; set; }
    }
}
