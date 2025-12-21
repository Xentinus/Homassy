using Homassy.API.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Extensions;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        if (pagination.ReturnAll)
        {
            var allItems = await query.ToListAsync(cancellationToken);
            return PagedResult<T>.CreateUnpaginated(allItems);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<T>.Create(items, totalCount, pagination.PageNumber, pagination.PageSize);
    }

    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> items,
        PaginationRequest pagination)
    {
        var itemList = items as List<T> ?? items.ToList();

        if (pagination.ReturnAll)
        {
            return PagedResult<T>.CreateUnpaginated(itemList);
        }

        var totalCount = itemList.Count;
        var pagedItems = itemList
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToList();

        return PagedResult<T>.Create(pagedItems, totalCount, pagination.PageNumber, pagination.PageSize);
    }
}
