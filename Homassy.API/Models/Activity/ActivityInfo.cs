using Homassy.API.Enums;

namespace Homassy.API.Models.Activity
{
    public record ActivityInfo
    {
        public Guid PublicId { get; init; }
        public Guid UserPublicId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public int? FamilyId { get; init; }
        public DateTime Timestamp { get; init; }
        public ActivityType ActivityType { get; init; }
        public string ActivityTypeName { get; init; } = string.Empty;
        public int RecordId { get; init; }
        public string RecordName { get; init; } = string.Empty;
    }
}
