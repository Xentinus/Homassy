using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Homassy.API.Entities
{
    public abstract class BaseEntity
    {
        [Key]
        public required int Id { get; set; }
        public required bool IsDeleted { get; set; } = false;
        public string RekordChange { get; set; } = JsonSerializer.Serialize(new { LastModifiedDate = DateTime.Now });

        public void UpdateRekordChange()
        {
            RekordChange = JsonSerializer.Serialize(new { LastModifiedDate = DateTime.Now });
        }

        public void DeleteRekord()
        {
            IsDeleted = true;
            UpdateRekordChange();
        }
    }
}
