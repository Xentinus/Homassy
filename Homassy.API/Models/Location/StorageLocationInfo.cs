namespace Homassy.API.Models.Location
{
    public class StorageLocationInfo
    {
        public Guid PublicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Color { get; set; }
        public bool IsFreezer { get; set; }
        /// <summary>Manual position — see <c>LocationBase.SortOrder</c>. Sparse, not an index.</summary>
        public int SortOrder { get; set; }
        public bool IsSharedWithFamily { get; set; } = false;
    }
}
