namespace Homassy.API.Models.Common;

public class PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool IsUnpaginated { get; init; }

    public static PagedResult<T> Create(List<T> items, int totalCount, int pageNumber, int pageSize)
    {
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            IsUnpaginated = false
        };
    }

    public static PagedResult<T> CreateUnpaginated(List<T> items)
    {
        return new PagedResult<T>
        {
            Items = items,
            TotalCount = items.Count,
            PageNumber = 1,
            PageSize = items.Count,
            IsUnpaginated = true
        };
    }
}
