using Homassy.API.Context;
using Homassy.API.Entities.ShoppingList;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class ShoppingListFunctions
    {
        private static readonly ConcurrentDictionary<int, ShoppingListItem> _shoppingListItemCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync()
        {
            var context = new HomassyDbContext();
            var shoppingListItems = await context.ShoppingListItems
                .AsNoTracking()
                .Where(w => !w.PurchasedAt.HasValue || w.PurchasedAt > DateTime.UtcNow)
                .ToListAsync();

            try
            {
                foreach (var shoppingListItem in shoppingListItems)
                {
                    _shoppingListItemCache[shoppingListItem.Id] = shoppingListItem;
                }

                Inited = true;
                Log.Information($"Initialized shopping list cache with {shoppingListItems.Count} shopping list items.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize shopping list cache.");
                throw;
            }
        }

        public async Task RefreshCacheAsync(int shoppingListItemId)
        {
            try
            {
                var context = new HomassyDbContext();
                var shoppingListItem = await context.ShoppingListItems.AsNoTracking().FirstOrDefaultAsync(sli => sli.Id == shoppingListItemId);
                var existsInCache = _shoppingListItemCache.ContainsKey(shoppingListItemId);

                if (shoppingListItem != null && existsInCache)
                {
                    _shoppingListItemCache[shoppingListItemId] = shoppingListItem;
                    Log.Debug($"Refreshed shopping list item {shoppingListItemId} in cache.");
                }
                else if (shoppingListItem != null && !existsInCache)
                {
                    _shoppingListItemCache[shoppingListItemId] = shoppingListItem;
                    Log.Debug($"Added shopping list item {shoppingListItemId} to cache.");
                }
                else if (shoppingListItem == null && existsInCache)
                {
                    _shoppingListItemCache.TryRemove(shoppingListItemId, out _);
                    Log.Debug($"Removed deleted shopping list item {shoppingListItemId} from cache.");
                }
                else
                {
                    Log.Debug($"Shopping list item {shoppingListItemId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for shopping list item {shoppingListItemId}.");
                throw;
            }
        }

        public ShoppingListItem? GetShoppingListItemById(int? shoppingListItemId)
        {
            if (shoppingListItemId == null) return null;
            ShoppingListItem? shoppingListItem = null;

            if (Inited)
            {
                _shoppingListItemCache.TryGetValue((int)shoppingListItemId, out shoppingListItem);
            }

            if (shoppingListItem == null)
            {
                var context = new HomassyDbContext();
                shoppingListItem = context.ShoppingListItems.AsNoTracking().FirstOrDefault(sli => sli.Id == shoppingListItemId);
            }

            return shoppingListItem;
        }

        public List<ShoppingListItem> GetShoppingListItemsByIds(List<int?> shoppingListItemIds)
        {
            if (shoppingListItemIds == null || !shoppingListItemIds.Any()) return new List<ShoppingListItem>();

            var validIds = shoppingListItemIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<ShoppingListItem>();

            var result = new List<ShoppingListItem>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_shoppingListItemCache.TryGetValue(id, out var shoppingListItem))
                    {
                        result.Add(shoppingListItem);
                    }
                    else
                    {
                        missingIds.Add(id);
                    }
                }
            }
            else
            {
                missingIds = validIds;
            }

            if (missingIds.Count > 0)
            {
                var context = new HomassyDbContext();
                var dbShoppingListItems = context.ShoppingListItems
                    .AsNoTracking()
                    .Where(sli => missingIds.Contains(sli.Id))
                    .ToList();

                result.AddRange(dbShoppingListItems);
            }

            return result;
        }

        public List<ShoppingListItem> GetShoppingListItemsByFamilyId(int familyId)
        {
            if (Inited)
            {
                return _shoppingListItemCache.Values
                    .Where(sli => sli.FamilyId == familyId)
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingListItems
                .AsNoTracking()
                .Where(sli => sli.FamilyId == familyId)
                .ToList();
        }

        public List<ShoppingListItem> GetShoppingListItemsByUserId(int userId)
        {
            if (Inited)
            {
                return _shoppingListItemCache.Values
                    .Where(sli => sli.UserId == userId)
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingListItems
                .AsNoTracking()
                .Where(sli => sli.UserId == userId)
                .ToList();
        }
        #endregion
    }
}
