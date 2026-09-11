using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Microsoft.EntityFrameworkCore;

namespace Homassy.Notifications.Services;

/// <summary>
/// The number a push writes onto the installed app's icon (#130).
/// </summary>
/// <remarks>
/// The app's own badge is a sum of sources the user picks between, and those switches are
/// device-local (<c>localStorage</c>, see <c>useAppBadge</c>) - a server cannot see them. What is
/// counted here is therefore the app's <b>default</b> pair: unread chat messages and shopping-list
/// items that are due or overdue. Both are somebody waiting on you, which is what the badge is
/// for; expiring products are off by default and are not counted.
/// <para>
/// A device that turned one of those sources off gets a number slightly too high until the app is
/// next opened, at which point the client recomputes the badge from its own switches. That is the
/// deliberate trade: a badge that is right for the default setup and self-correcting on open beats
/// no badge at all while the app is closed, which is exactly when the icon is the only surface
/// there is.
/// </para>
/// </remarks>
public static class AppBadgeCount
{
    /// <summary>
    /// How far ahead a shopping-list deadline counts, matching the web's deadline badge
    /// (<c>GET /shoppinglist/item/deadline-count</c>).
    /// </summary>
    private const int DeadlineWindowDays = 14;

    /// <summary>
    /// What one user's app icon should show: unread chat messages plus due or overdue
    /// shopping-list items.
    /// </summary>
    public static async Task<int> ForUserAsync(
        HomassyDbContext context,
        int userId,
        int? familyId,
        CancellationToken cancellationToken)
    {
        var unreadChat = await UnreadChatMessagesAsync(context, userId, familyId, cancellationToken);
        var deadlines = await DueShoppingListItemsAsync(context, userId, familyId, cancellationToken);

        return unreadChat + deadlines;
    }

    /// <summary>
    /// Messages in the user's family sent after their read marker, their own excluded.
    /// </summary>
    /// <remarks>
    /// The same rule as <c>FamilyChatFunctions.CountUnreadAsync</c>, and it has to stay the same
    /// rule: the icon and the in-app badge showing different numbers for the same conversation is
    /// worse than either being slightly stale.
    /// </remarks>
    private static async Task<int> UnreadChatMessagesAsync(
        HomassyDbContext context,
        int userId,
        int? familyId,
        CancellationToken cancellationToken)
    {
        if (!familyId.HasValue) return 0;

        var lastReadAt = await context.Set<FamilyChatReadState>()
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.FamilyId == familyId.Value)
            .Select(r => (DateTime?)r.LastReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        return await context.Set<FamilyChatMessage>()
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId.Value
                && m.SenderUserId != userId
                && (lastReadAt == null || m.SentAt > lastReadAt))
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Unpurchased items on the user's own and their family's lists, due within the window or
    /// already past it.
    /// </summary>
    private static async Task<int> DueShoppingListItemsAsync(
        HomassyDbContext context,
        int userId,
        int? familyId,
        CancellationToken cancellationToken)
    {
        var horizon = DateTime.UtcNow.Date.AddDays(DeadlineWindowDays);

        var listIds = await context.ShoppingLists
            .AsNoTracking()
            .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
            .Select(sl => sl.Id)
            .ToListAsync(cancellationToken);

        if (listIds.Count == 0) return 0;

        return await context.ShoppingListItems
            .AsNoTracking()
            .Where(i => !i.PurchasedAt.HasValue
                && listIds.Contains(i.ShoppingListId)
                && ((i.DeadlineAt.HasValue && i.DeadlineAt.Value.Date <= horizon)
                    || (i.DueAt.HasValue && i.DueAt.Value.Date <= horizon)))
            .CountAsync(cancellationToken);
    }
}
