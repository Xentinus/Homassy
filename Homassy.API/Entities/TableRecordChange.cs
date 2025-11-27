namespace Homassy.API.Entities
{
    public class TableRecordChange : BaseEntity
    {
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
    }
}
