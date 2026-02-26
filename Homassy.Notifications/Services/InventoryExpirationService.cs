using Homassy.API.Context;
using Homassy.Notifications.Models;
using Microsoft.EntityFrameworkCore;

namespace Homassy.Notifications.Services;

public sealed class InventoryExpirationService
{
    private const int ExpiringThresholdDays = 14;

    private readonly HomassyDbContext _context;
    private readonly ILogger<InventoryExpirationService> _logger;

    public InventoryExpirationService(HomassyDbContext context, ILogger<InventoryExpirationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> GetExpiringCountForUserAsync(int userId, int? familyId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var expiringThreshold = today.AddDays(ExpiringThresholdDays);

        var query = _context.ProductInventoryItems
            .AsNoTracking()
            .Where(i => !i.IsFullyConsumed && i.ExpirationAt.HasValue);

        if (familyId.HasValue)
            query = query.Where(i => i.UserId == userId || i.FamilyId == familyId);
        else
            query = query.Where(i => i.UserId == userId);

        return await query.CountAsync(i => i.ExpirationAt!.Value.Date <= expiringThreshold, ct);
    }

    public async Task<List<ExpiringProductItem>> GetExpiringItemsForUserAsync(int userId, int? familyId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var expiringThreshold = today.AddDays(ExpiringThresholdDays);

        var query = _context.ProductInventoryItems
            .AsNoTracking()
            .Where(i => !i.IsFullyConsumed && i.ExpirationAt.HasValue);

        if (familyId.HasValue)
            query = query.Where(i => i.UserId == userId || i.FamilyId == familyId);
        else
            query = query.Where(i => i.UserId == userId);

        // Only include expired or expiring-soon items
        query = query.Where(i => i.ExpirationAt!.Value.Date <= expiringThreshold);

        var items = await query
            .OrderBy(i => i.ExpirationAt)
            .Select(i => new
            {
                ProductName = i.Product.Name,
                Brand = i.Product.Brand,
                ExpirationDate = i.ExpirationAt!.Value
            })
            .ToListAsync(ct);

        return items.Select(i => new ExpiringProductItem(
            i.ProductName,
            i.Brand,
            i.ExpirationDate,
            IsExpired: i.ExpirationDate.Date < today))
            .ToList();
    }
}
