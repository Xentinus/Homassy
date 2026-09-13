namespace Homassy.Data.Models.Common
{
    public class RecordChange
    {
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
        public int? LastModifiedBy { get; set; }
    }
}
