using Homassy.API.Enums;
using Homassy.API.Models.Common;

namespace Homassy.API.Models.Activity
{
    public class GetActivitiesRequest : PaginationRequest
    {
        public ActivityType? ActivityType { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public Guid? UserPublicId { get; init; }
    }
}
