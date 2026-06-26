namespace Homassy.API.Enums
{
    /// <summary>
    /// Lifecycle of a request to join a family.
    /// </summary>
    public enum FamilyJoinRequestStatus
    {
        /// <summary>Awaiting a decision from an existing family member.</summary>
        Pending = 0,

        /// <summary>Accepted by a family member; the requester has joined the family.</summary>
        Approved = 1,

        /// <summary>Declined by a family member.</summary>
        Rejected = 2,

        /// <summary>Withdrawn by the requester before a decision was made.</summary>
        Cancelled = 3
    }
}
