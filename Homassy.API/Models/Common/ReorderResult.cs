namespace Homassy.API.Models.Common
{
    /// <summary>
    /// One row's position after a reorder. Only the rows that actually moved are returned (and
    /// broadcast) — the sparse scheme means most reorders touch a single row, and sending the whole
    /// list back would hide that.
    /// </summary>
    public class ReorderedEntry
    {
        public Guid PublicId { get; set; }
        public int SortOrder { get; set; }
    }
}
