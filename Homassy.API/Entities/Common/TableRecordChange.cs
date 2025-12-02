namespace Homassy.API.Entities.Common
{
    public class TableRecordChange : SoftDeleteEntity
    {
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
    }
}
