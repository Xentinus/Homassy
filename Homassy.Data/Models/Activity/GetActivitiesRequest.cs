using Homassy.Data.Enums;
using Homassy.Data.Models.Common;

namespace Homassy.Data.Models.Activity
{
    public class GetActivitiesRequest : PaginationRequest
    {
        public ActivityType? ActivityType { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public Guid? UserPublicId { get; init; }
    }
}
