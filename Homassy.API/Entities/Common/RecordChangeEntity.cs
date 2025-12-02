using Homassy.API.Models.Common;
using System.Text.Json;

namespace Homassy.API.Entities.Common
{
    public class RecordChangeEntity : SoftDeleteEntity
    {
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
