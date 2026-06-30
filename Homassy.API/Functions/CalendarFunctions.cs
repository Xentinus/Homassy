using Homassy.API.Context;
using Homassy.API.Models.Calendar;
using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Functions
{
    public class CalendarFunctions
    {
        public async Task<List<CalendarEventInfo>> GetCalendarEventsAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            var familyId = SessionInfo.GetFamilyId();

            var inventoryTask = GetInventoryExpirationEventsAsync(new HomassyDbContext(), userId, familyId, startDate, endDate, cancellationToken);
            var automationTask = GetAutomationEventsAsync(new HomassyDbContext(), userId, familyId, startDate, endDate, cancellationToken);
            var shoppingTask = GetShoppingListDeadlineEventsAsync(new HomassyDbContext(), userId, familyId, startDate, endDate, cancellationToken);
            var externalTask = GetExternalCalendarEventsAsync(familyId, startDate, endDate);

            await Task.WhenAll(inventoryTask, automationTask, shoppingTask, externalTask);

            var result = new List<CalendarEventInfo>();
            result.AddRange(await inventoryTask);
            result.AddRange(await automationTask);
            result.AddRange(await shoppingTask);
            result.AddRange(await externalTask);

            return result.OrderBy(e => e.Start).ToList();
        }

        private static Task<List<CalendarEventInfo>> GetExternalCalendarEventsAsync(
            int? familyId,
            DateTime startDate,
            DateTime endDate)
        {
            if (!familyId.HasValue)
                return Task.FromResult(new List<CalendarEventInfo>());

            var events = new ExternalCalendarFunctions()
                .GetCachedEventsForDateRange(familyId.Value, startDate, endDate);

            return Task.FromResult(events);
        }

        private static async Task<List<CalendarEventInfo>> GetInventoryExpirationEventsAsync(
            HomassyDbContext context,
            int? userId,
            int? familyId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct)
        {
            var query = context.ProductInventoryItems
                .Include(i => i.Product)
                .Where(i =>
                    i.ExpirationAt.HasValue &&
                    i.ExpirationAt.Value >= startDate &&
                    i.ExpirationAt.Value <= endDate &&
                    !i.IsFullyConsumed);

            query = familyId.HasValue
                ? query.Where(i => i.UserId == userId || i.FamilyId == familyId)
                : query.Where(i => i.UserId == userId);

            var items = await query.ToListAsync(ct);

            return items.Select(i => new CalendarEventInfo
            {
                PublicId = i.PublicId,
                Title = i.Product.Name,
                EventType = CalendarEventType.InventoryExpiration,
                Start = i.ExpirationAt!.Value,
                Detail = null,
                RelatedEntityPublicId = i.Product.PublicId
            }).ToList();
        }

        private static async Task<List<CalendarEventInfo>> GetAutomationEventsAsync(
            HomassyDbContext context,
            int? userId,
            int? familyId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct)
        {
            var query = context.ItemAutomations
                .Include(a => a.Product)
                .Where(a =>
                    a.NextExecutionAt.HasValue &&
                    a.NextExecutionAt.Value >= startDate &&
                    a.NextExecutionAt.Value <= endDate &&
                    a.IsEnabled);

            query = familyId.HasValue
                ? query.Where(a => a.UserId == userId || a.FamilyId == familyId)
                : query.Where(a => a.UserId == userId);

            var automations = await query.ToListAsync(ct);

            return automations.Select(a => new CalendarEventInfo
            {
                PublicId = a.PublicId,
                Title = a.Product?.Name ?? string.Empty,
                EventType = CalendarEventType.AutomationExecution,
                Start = a.NextExecutionAt!.Value,
                Detail = a.ActionType.ToString(),
                RelatedEntityPublicId = a.Product?.PublicId
            }).ToList();
        }

        private static async Task<List<CalendarEventInfo>> GetShoppingListDeadlineEventsAsync(
            HomassyDbContext context,
            int? userId,
            int? familyId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken ct)
        {
            var query = context.ShoppingListItems
                .Include(i => i.ShoppingList)
                .Where(i =>
                    i.PurchasedAt == null &&
                    (
                        (i.DeadlineAt.HasValue && i.DeadlineAt.Value >= startDate && i.DeadlineAt.Value <= endDate) ||
                        (i.DueAt.HasValue && i.DueAt.Value >= startDate && i.DueAt.Value <= endDate)
                    ));

            query = familyId.HasValue
                ? query.Where(i => i.ShoppingList.UserId == userId || i.ShoppingList.FamilyId == familyId)
                : query.Where(i => i.ShoppingList.UserId == userId);

            var items = await query.ToListAsync(ct);

            return items.Select(i => new CalendarEventInfo
            {
                PublicId = i.PublicId,
                Title = i.CustomName ?? i.ShoppingList.Name,
                EventType = CalendarEventType.ShoppingListDeadline,
                Start = (i.DeadlineAt ?? i.DueAt)!.Value,
                Detail = i.ShoppingList.Name,
                RelatedEntityPublicId = i.ShoppingList.PublicId
            }).ToList();
        }
    }
}
