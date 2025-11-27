using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Homassy.API.Entities
{
    public class TableRecordChange : BaseEntity
    {
        public string TableName { get; set; } = string.Empty;
        public int RowId { get; set; }
    }
}
