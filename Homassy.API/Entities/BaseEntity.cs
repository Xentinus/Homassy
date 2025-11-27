using Homassy.API.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Homassy.API.Entities
{
    public abstract class BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string RecordChange { get; set; } = JsonSerializer.Serialize(new RecordChange());

        public void UpdateRecordChange(int? modifiedBy = null)
        {
            RecordChange = JsonSerializer.Serialize(new RecordChange
            {
                LastModifiedDate = DateTime.UtcNow,
                LastModifiedBy = modifiedBy ?? -1
            });
        }

        public void DeleteRekord(int? modifiedBy = null)
        {
            IsDeleted = true;
            UpdateRecordChange(modifiedBy);
        }
    }
}
