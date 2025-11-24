using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Homassy.API.Entities
{
    public abstract class BaseEntity
    {
        [Key]
        public required int Id { get; set; }
        public required bool IsDeleted { get; set; } = false;
        public string RecordChange { get; set; } = JsonSerializer.Serialize(new { LastModifiedDate = DateTime.UtcNow });

        public void UpdateRecordChange()
        {
            RecordChange = JsonSerializer.Serialize(new { LastModifiedDate = DateTime.UtcNow });
        }

        public void DeleteRekord()
        {
            IsDeleted = true;
            UpdateRecordChange();
        }
    }
}
