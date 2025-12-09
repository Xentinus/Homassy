namespace Homassy.API.Entities.Common
{
    public class SoftDeleteEntity : BaseEntity
    {
        public bool IsDeleted { get; set; } = false;
        public void DeleteRecord()
        {
            IsDeleted = true;
        }
    }
}
