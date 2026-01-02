using Homassy.API.Context;
using Homassy.API.Entities.ShoppingList;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Common;
using Homassy.API.Models.ShoppingList;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class ShoppingListFunctions
    {
        private static readonly ConcurrentDictionary<int, ShoppingList> _shoppingListCache = new();
        private static readonly ConcurrentDictionary<int, ShoppingListItem> _shoppingListItemCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();
            var shoppingLists = await context.ShoppingLists
                .ToListAsync(cancellationToken);

            var shoppingListItems = await context.ShoppingListItems
                .Where(w => !w.PurchasedAt.HasValue || w.PurchasedAt >= DateTime.UtcNow.Date.AddDays(-1))
                .ToListAsync(cancellationToken);

            try
            {
                foreach (var shoppingList in shoppingLists)
                {
                    _shoppingListCache[shoppingList.Id] = shoppingList;
                }

                foreach (var shoppingListItem in shoppingListItems)
                {
                    _shoppingListItemCache[shoppingListItem.Id] = shoppingListItem;
                }

                Inited = true;
                Log.Information($"Initialized shopping list cache with {shoppingLists.Count} shopping lists and {shoppingListItems.Count} shopping list items.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize shopping list cache.");
                throw;
            }
        }

        public async Task RefreshShoppingListCacheAsync(int shoppingListId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var shoppingList = await context.ShoppingLists.FirstOrDefaultAsync(sl => sl.Id == shoppingListId, cancellationToken);
                var existsInCache = _shoppingListCache.ContainsKey(shoppingListId);

                if (shoppingList != null && existsInCache)
                {
                    _shoppingListCache[shoppingListId] = shoppingList;
                    Log.Debug($"Refreshed shopping list {shoppingListId} in cache.");
                }
                else if (shoppingList != null && !existsInCache)
                {
                    _shoppingListCache[shoppingListId] = shoppingList;
                    Log.Debug($"Added shopping list {shoppingListId} to cache.");
                }
                else if (shoppingList == null && existsInCache)
                {
                    _shoppingListCache.TryRemove(shoppingListId, out _);
                    Log.Debug($"Removed deleted shopping list {shoppingListId} from cache.");
                }
                else
                {
                    Log.Debug($"Shopping list {shoppingListId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for shopping list {shoppingListId}.");
                throw;
            }
        }

        public async Task RefreshShoppingListItemCacheAsync(int shoppingListItemId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var shoppingListItem = await context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.Id == shoppingListItemId, cancellationToken);
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
        #endregion

        #region Cache Getters - ShoppingList
        public ShoppingList? GetShoppingListById(int? shoppingListId)
        {
            if (shoppingListId == null) return null;
            ShoppingList? shoppingList = null;

            if (Inited)
            {
                _shoppingListCache.TryGetValue((int)shoppingListId, out shoppingList);
            }

            if (shoppingList == null)
            {
                var context = new HomassyDbContext();
                shoppingList = context.ShoppingLists.AsNoTracking().FirstOrDefault(sl => sl.Id == shoppingListId);
            }

            return shoppingList;
        }

        public ShoppingList? GetShoppingListByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var shoppingList = _shoppingListCache.Values.FirstOrDefault(sl => sl.PublicId == publicId);
                if (shoppingList != null) return shoppingList;
            }

            var context = new HomassyDbContext();
            return context.ShoppingLists.AsNoTracking().FirstOrDefault(sl => sl.PublicId == publicId);
        }

        public List<ShoppingList> GetShoppingListsByIds(List<int?> shoppingListIds)
        {
            if (shoppingListIds == null || !shoppingListIds.Any()) return new List<ShoppingList>();

            var validIds = shoppingListIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<ShoppingList>();

            var result = new List<ShoppingList>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_shoppingListCache.TryGetValue(id, out var shoppingList))
                    {
                        result.Add(shoppingList);
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
                var dbShoppingLists = context.ShoppingLists
                    .Where(sl => missingIds.Contains(sl.Id))
                    .ToList();

                result.AddRange(dbShoppingLists);
            }

            return result;
        }

        public List<ShoppingList> GetShoppingListsByUserAndFamily(int userId, int? familyId)
        {
            if (Inited)
            {
                return _shoppingListCache.Values
                    .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingLists
                .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
                .ToList();
        }
        #endregion

        #region Cache Getters - ShoppingListItem
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
                shoppingListItem = context.ShoppingListItems.FirstOrDefault(sli => sli.Id == shoppingListItemId);
            }

            return shoppingListItem;
        }

        public ShoppingListItem? GetShoppingListItemByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var shoppingListItem = _shoppingListItemCache.Values.FirstOrDefault(sli => sli.PublicId == publicId);
                if (shoppingListItem != null) return shoppingListItem;
            }

            var context = new HomassyDbContext();
            return context.ShoppingListItems.FirstOrDefault(sli => sli.PublicId == publicId);
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
                    .Where(sli => missingIds.Contains(sli.Id))
                    .ToList();

                result.AddRange(dbShoppingListItems);
            }

            return result;
        }

        public List<ShoppingListItem> GetShoppingListItemsByShoppingListId(int shoppingListId, bool includePurchased = false)
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            if (Inited)
            {
                return _shoppingListItemCache.Values
                    .Where(sli => sli.ShoppingListId == shoppingListId &&
                                  (includePurchased || !sli.PurchasedAt.HasValue || sli.PurchasedAt >= yesterday))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingListItems
                .Where(sli => sli.ShoppingListId == shoppingListId &&
                              (includePurchased || !sli.PurchasedAt.HasValue || sli.PurchasedAt >= yesterday))
                .ToList();
        }

        public List<ShoppingListItem> GetShoppingListItemsByFamilyId(int familyId)
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            // ShoppingListItem doesn't have FamilyId directly, need to filter through ShoppingList
            var shoppingListIds = GetShoppingListsByUserAndFamily(0, familyId)
                .Where(sl => sl.FamilyId == familyId)
                .Select(sl => sl.Id)
                .ToList();

            if (Inited)
            {
                return _shoppingListItemCache.Values
                    .Where(sli => shoppingListIds.Contains(sli.ShoppingListId) &&
                                  (!sli.PurchasedAt.HasValue || sli.PurchasedAt >= yesterday))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingListItems
                .Where(sli => shoppingListIds.Contains(sli.ShoppingListId) &&
                              (!sli.PurchasedAt.HasValue || sli.PurchasedAt >= yesterday))
                .ToList();
        }

        public List<ShoppingListItem> GetShoppingListItemsByUserId(int userId)
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            // ShoppingListItem doesn't have UserId directly, need to filter through ShoppingList
            var shoppingListIds = GetShoppingListsByUserAndFamily(userId, null)
                .Where(sl => sl.UserId == userId)
                .Select(sl => sl.Id)
                .ToList();

            if (Inited)
            {
                return _shoppingListItemCache.Values
                    .Where(sli => shoppingListIds.Contains(sli.ShoppingListId) &&
                                  (!sli.PurchasedAt.HasValue || sli.PurchasedAt >= yesterday))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingListItems
                .Where(sli => shoppingListIds.Contains(sli.ShoppingListId) &&
                              (!sli.PurchasedAt.HasValue || sli.PurchasedAt >= yesterday))
                .ToList();
        }
        #endregion

        #region ShoppingList CRUD Methods
        public List<ShoppingListInfo> GetAllShoppingLists()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var shoppingLists = GetShoppingListsByUserAndFamily(userId.Value, familyId);

            return shoppingLists.Select(sl => new ShoppingListInfo
            {
                PublicId = sl.PublicId,
                Name = sl.Name,
                Description = sl.Description,
                Color = sl.Color,
                IsSharedWithFamily = sl.FamilyId.HasValue
            }).ToList();
        }

        public DetailedShoppingListInfo? GetDetailedShoppingList(Guid publicId, bool showPurchased = false)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingList = GetShoppingListByPublicId(publicId);
            if (shoppingList == null)
            {
                return null;
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var items = GetShoppingListItemsByShoppingListId(shoppingList.Id, showPurchased);
            var locationFunctions = new LocationFunctions();
            var productFunctions = new ProductFunctions();

            return new DetailedShoppingListInfo
            {
                PublicId = shoppingList.PublicId,
                Name = shoppingList.Name,
                Description = shoppingList.Description,
                Color = shoppingList.Color,
                IsSharedWithFamily = shoppingList.FamilyId.HasValue,
                Items = items.Select(sli =>
                {
                    var product = sli.ProductId.HasValue ? productFunctions.GetProductById(sli.ProductId) : null;
                    var shoppingLocation = sli.ShoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(sli.ShoppingLocationId) : null;

                    return new ShoppingListItemInfo
                    {
                        PublicId = sli.PublicId,
                        ShoppingListPublicId = shoppingList.PublicId,
                        ProductPublicId = product?.PublicId,
                        ShoppingLocationPublicId = shoppingLocation?.PublicId,
                        Product = productFunctions.ConvertToProductInfo(product, userId),
                        ShoppingLocation = locationFunctions.ConvertToShoppingLocationInfo(shoppingLocation),
                        CustomName = sli.CustomName,
                        Quantity = sli.Quantity,
                        Unit = sli.Unit,
                        Note = sli.Note,
                        PurchasedAt = sli.PurchasedAt,
                        DeadlineAt = sli.DeadlineAt,
                        DueAt = sli.DueAt
                    };
                }).ToList()
            };
        }

        public async Task<ShoppingListInfo> CreateShoppingListAsync(CreateShoppingListRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException("Name is required");
            }

            var familyId = SessionInfo.GetFamilyId();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var shoppingList = new ShoppingList
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    Color = request.Color?.Trim(),
                    UserId = userId.Value,
                    FamilyId = request.IsSharedWithFamily == true && familyId.HasValue ? familyId : null
                };

                context.ShoppingLists.Add(shoppingList);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created shopping list {shoppingList.Id} (PublicId: {shoppingList.PublicId}), shared with family: {request.IsSharedWithFamily}");

                // Record activity
                try
                {
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListCreate,
                        shoppingList.Id,
                        shoppingList.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListCreate activity for shopping list {shoppingList.Name}");
                }

                return new ShoppingListInfo
                {
                    PublicId = shoppingList.PublicId,
                    Name = shoppingList.Name,
                    Description = shoppingList.Description,
                    Color = shoppingList.Color,
                    IsSharedWithFamily = shoppingList.FamilyId.HasValue
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create shopping list for user {userId.Value}");
                throw;
            }
        }

        public async Task<ShoppingListInfo> UpdateShoppingListAsync(Guid publicId, UpdateShoppingListRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingList = GetShoppingListByPublicId(publicId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedList = await context.ShoppingLists.FindAsync([shoppingList.Id], cancellationToken);
                if (trackedList == null)
                {
                    throw new ShoppingListNotFoundException();
                }

                bool hasChanges = false;

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    trackedList.Name = request.Name.Trim();
                    hasChanges = true;
                }

                if (request.Description != null)
                {
                    trackedList.Description = string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim();
                    hasChanges = true;
                }

                if (request.Color != null)
                {
                    trackedList.Color = string.IsNullOrWhiteSpace(request.Color)
                        ? null
                        : request.Color.Trim();
                    hasChanges = true;
                }

                if (request.IsSharedWithFamily.HasValue)
                {
                    trackedList.FamilyId = request.IsSharedWithFamily.Value && familyId.HasValue
                        ? familyId
                        : null;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information($"User {userId.Value} updated shopping list {trackedList.Id} (PublicId: {trackedList.PublicId})");
                }
                else
                {
                    Log.Debug($"User {userId.Value} attempted to update shopping list {trackedList.Id} but no changes were made");
                }

                await transaction.CommitAsync(cancellationToken);

                // Record activity only if changes were made
                if (hasChanges)
                {
                    try
                    {
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ShoppingListUpdate,
                            trackedList.Id,
                            trackedList.Name,
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ShoppingListUpdate activity for shopping list {trackedList.Name}");
                    }
                }

                return new ShoppingListInfo
                {
                    PublicId = trackedList.PublicId,
                    Name = trackedList.Name,
                    Description = trackedList.Description,
                    Color = trackedList.Color,
                    IsSharedWithFamily = trackedList.FamilyId.HasValue
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to update shopping list {publicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteShoppingListAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingList = GetShoppingListByPublicId(publicId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedList = await context.ShoppingLists.FindAsync([shoppingList.Id], cancellationToken);
                if (trackedList == null)
                {
                    throw new ShoppingListNotFoundException();
                }

                trackedList.DeleteRecord(userId.Value);

                context.ShoppingLists.Update(trackedList);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted shopping list {shoppingList.Id} (PublicId: {shoppingList.PublicId})");

                // Record activity
                try
                {
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListDelete,
                        shoppingList.Id,
                        shoppingList.Name,
                        cancellationToken: cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListDelete activity for shopping list {shoppingList.Name}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete shopping list {publicId} for user {userId.Value}");
                throw;
            }
        }
        #endregion

        #region ShoppingListItem CRUD Methods
        public async Task<ShoppingListItemInfo> CreateShoppingListItemAsync(CreateShoppingListItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingList = GetShoppingListByPublicId(request.ShoppingListPublicId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            if (string.IsNullOrWhiteSpace(request.CustomName) && !request.ProductPublicId.HasValue)
            {
                throw new InvalidShoppingListItemException("Either CustomName or ProductPublicId must be provided");
            }

            var locationFunctions = new LocationFunctions();
            var productFunctions = new ProductFunctions();

            int? productId = null;
            int? shoppingLocationId = null;

            if (request.ProductPublicId.HasValue)
            {
                var product = productFunctions.GetProductByPublicId(request.ProductPublicId.Value);
                if (product == null)
                {
                    throw new ProductNotFoundException("Product not found");
                }
                productId = product.Id;
            }

            if (request.ShoppingLocationPublicId.HasValue)
            {
                var shoppingLocation = locationFunctions.GetShoppingLocationByPublicId(request.ShoppingLocationPublicId.Value);
                if (shoppingLocation == null)
                {
                    throw new ShoppingLocationNotFoundException("Shopping location not found");
                }
                shoppingLocationId = shoppingLocation.Id;
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var shoppingListItem = new ShoppingListItem
                {
                    ShoppingListId = shoppingList.Id,
                    ProductId = productId,
                    ShoppingLocationId = shoppingLocationId,
                    CustomName = request.CustomName?.Trim(),
                    Quantity = request.Quantity,
                    Unit = request.Unit,
                    Note = request.Note?.Trim(),
                    DeadlineAt = request.DeadlineAt,
                    DueAt = request.DueAt
                };

                context.ShoppingListItems.Add(shoppingListItem);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created shopping list item {shoppingListItem.Id} (PublicId: {shoppingListItem.PublicId}) in shopping list {shoppingList.Id}");

                // Record activity
                try
                {
                    var itemName = !string.IsNullOrWhiteSpace(shoppingListItem.CustomName) 
                        ? shoppingListItem.CustomName 
                        : (productId.HasValue ? productFunctions.GetProductById(productId.Value)?.Name : null) ?? "Unknown";
                    
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListItemAdd,
                        shoppingListItem.Id,
                        $"{shoppingList.Name} - {itemName}",
                        cancellationToken: cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListItemAdd activity for shopping list item {shoppingListItem.PublicId}");
                }

                return new ShoppingListItemInfo
                {
                    PublicId = shoppingListItem.PublicId,
                    ShoppingListPublicId = shoppingList.PublicId,
                    ProductPublicId = request.ProductPublicId,
                    ShoppingLocationPublicId = request.ShoppingLocationPublicId,
                    CustomName = shoppingListItem.CustomName,
                    Quantity = shoppingListItem.Quantity,
                    Unit = shoppingListItem.Unit,
                    Note = shoppingListItem.Note,
                    PurchasedAt = shoppingListItem.PurchasedAt,
                    DeadlineAt = shoppingListItem.DeadlineAt,
                    DueAt = shoppingListItem.DueAt
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create shopping list item for user {userId.Value}");
                throw;
            }
        }

        public async Task<ShoppingListItemInfo> UpdateShoppingListItemAsync(Guid publicId, UpdateShoppingListItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingListItem = GetShoppingListItemByPublicId(publicId);
            if (shoppingListItem == null)
            {
                throw new ShoppingListItemNotFoundException();
            }

            var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var locationFunctions = new LocationFunctions();
            var productFunctions = new ProductFunctions();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                if (trackedItem == null)
                {
                    throw new ShoppingListItemNotFoundException();
                }

                bool hasChanges = false;

                if (request.ProductPublicId.HasValue)
                {
                    var product = productFunctions.GetProductByPublicId(request.ProductPublicId.Value);
                    if (product == null)
                    {
                        throw new ProductNotFoundException("Product not found");
                    }
                    trackedItem.ProductId = product.Id;
                    hasChanges = true;
                }

                if (request.ShoppingLocationPublicId.HasValue)
                {
                    var shoppingLocation = locationFunctions.GetShoppingLocationByPublicId(request.ShoppingLocationPublicId.Value);
                    if (shoppingLocation == null)
                    {
                        throw new ShoppingLocationNotFoundException("Shopping location not found");
                    }
                    trackedItem.ShoppingLocationId = shoppingLocation.Id;
                    hasChanges = true;
                }

                if (request.CustomName != null)
                {
                    trackedItem.CustomName = string.IsNullOrWhiteSpace(request.CustomName)
                        ? null
                        : request.CustomName.Trim();
                    hasChanges = true;
                }

                if (request.Quantity.HasValue)
                {
                    trackedItem.Quantity = request.Quantity.Value;
                    hasChanges = true;
                }

                if (request.Unit.HasValue)
                {
                    trackedItem.Unit = request.Unit.Value;
                    hasChanges = true;
                }

                if (request.Note != null)
                {
                    trackedItem.Note = string.IsNullOrWhiteSpace(request.Note)
                        ? null
                        : request.Note.Trim();
                    hasChanges = true;
                }

                if (request.PurchasedAt.HasValue)
                {
                    trackedItem.PurchasedAt = request.PurchasedAt.Value;
                    hasChanges = true;
                }

                if (request.DeadlineAt.HasValue)
                {
                    trackedItem.DeadlineAt = request.DeadlineAt.Value;
                    hasChanges = true;
                }

                if (request.DueAt.HasValue)
                {
                    trackedItem.DueAt = request.DueAt.Value;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information($"User {userId.Value} updated shopping list item {trackedItem.Id} (PublicId: {trackedItem.PublicId})");
                }
                else
                {
                    Log.Debug($"User {userId.Value} attempted to update shopping list item {trackedItem.Id} but no changes were made");
                }

                await transaction.CommitAsync(cancellationToken);

                // Record activity only if changes were made
                if (hasChanges)
                {
                    try
                    {
                        var itemName = !string.IsNullOrWhiteSpace(trackedItem.CustomName) 
                            ? trackedItem.CustomName 
                            : (trackedItem.ProductId.HasValue ? productFunctions.GetProductById(trackedItem.ProductId.Value)?.Name : null) ?? "Unknown";
                        
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ShoppingListItemUpdate,
                            trackedItem.Id,
                            $"{shoppingList.Name} - {itemName}",
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ShoppingListItemUpdate activity for shopping list item {trackedItem.PublicId}");
                    }
                }

                return new ShoppingListItemInfo
                {
                    PublicId = trackedItem.PublicId,
                    ShoppingListPublicId = shoppingList.PublicId,
                    ProductPublicId = trackedItem.ProductId.HasValue ? productFunctions.GetProductById(trackedItem.ProductId)?.PublicId : null,
                    ShoppingLocationPublicId = trackedItem.ShoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(trackedItem.ShoppingLocationId)?.PublicId : null,
                    CustomName = trackedItem.CustomName,
                    Quantity = trackedItem.Quantity,
                    Unit = trackedItem.Unit,
                    Note = trackedItem.Note,
                    PurchasedAt = trackedItem.PurchasedAt,
                    DeadlineAt = trackedItem.DeadlineAt,
                    DueAt = trackedItem.DueAt
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to update shopping list item {publicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteShoppingListItemAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingListItem = GetShoppingListItemByPublicId(publicId);
            if (shoppingListItem == null)
            {
                throw new ShoppingListItemNotFoundException();
            }

            var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                if (trackedItem == null)
                {
                    throw new ShoppingListItemNotFoundException();
                }

                trackedItem.DeleteRecord(userId.Value);

                context.ShoppingListItems.Update(trackedItem);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted shopping list item {shoppingListItem.Id} (PublicId: {shoppingListItem.PublicId})");

                // Record activity
                try
                {
                    var productFunctions = new ProductFunctions();
                    var itemName = !string.IsNullOrWhiteSpace(shoppingListItem.CustomName) 
                        ? shoppingListItem.CustomName 
                        : (shoppingListItem.ProductId.HasValue ? productFunctions.GetProductById(shoppingListItem.ProductId.Value)?.Name : null) ?? "Unknown";
                    
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListItemDelete,
                        shoppingListItem.Id,
                        $"{shoppingList.Name} - {itemName}",
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListItemDelete activity for shopping list item {shoppingListItem.PublicId}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete shopping list item {publicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task<ShoppingListItemInfo> QuickPurchaseFromShoppingListItemAsync(QuickPurchaseFromShoppingListItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingListItem = GetShoppingListItemByPublicId(request.ShoppingListItemPublicId);
            if (shoppingListItem == null)
            {
                throw new ShoppingListItemNotFoundException();
            }

            var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Update shopping list item with purchase date
                var trackedShoppingListItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                if (trackedShoppingListItem == null)
                {
                    throw new ShoppingListItemNotFoundException();
                }

                trackedShoppingListItem.PurchasedAt = request.PurchasedAt;
                await context.SaveChangesAsync(cancellationToken);

                // If it's a custom item (no ProductId), just set PurchasedAt and return
                if (!shoppingListItem.ProductId.HasValue)
                {
                    await transaction.CommitAsync(cancellationToken);

                    Log.Information($"User {userId} quick purchased custom shopping list item {shoppingListItem.Id} (PublicId: {shoppingListItem.PublicId})");

                    // Record activity for custom item
                    try
                    {
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ShoppingListItemPurchase,
                            shoppingListItem.Id,
                            $"{shoppingList.Name} - {trackedShoppingListItem.CustomName ?? "Custom Item"}",
                            null,
                            null,
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ShoppingListItemPurchase activity for custom shopping list item {shoppingListItem.PublicId}");
                    }

                    var customItemInfo = new ShoppingListItemInfo
                    {
                        PublicId = trackedShoppingListItem.PublicId,
                        ShoppingListPublicId = shoppingList.PublicId,
                        ProductPublicId = null,
                        ShoppingLocationPublicId = shoppingListItem.ShoppingLocationId.HasValue ? new LocationFunctions().GetShoppingLocationById(shoppingListItem.ShoppingLocationId)?.PublicId : null,
                        CustomName = trackedShoppingListItem.CustomName,
                        Quantity = trackedShoppingListItem.Quantity,
                        Unit = trackedShoppingListItem.Unit,
                        Note = trackedShoppingListItem.Note,
                        PurchasedAt = trackedShoppingListItem.PurchasedAt,
                        DeadlineAt = trackedShoppingListItem.DeadlineAt,
                        DueAt = trackedShoppingListItem.DueAt
                    };

                    return customItemInfo;
                }

                // For items with products, continue with inventory creation
                var locationFunctions = new LocationFunctions();
                var productFunctions = new ProductFunctions();

                var product = productFunctions.GetProductById(shoppingListItem.ProductId);
                if (product == null)
                {
                    throw new ProductNotFoundException("Product not found");
                }

                int? storageLocationId = null;
                if (request.StorageLocationPublicId.HasValue)
                {
                    var storageLocation = locationFunctions.GetStorageLocationByPublicId(request.StorageLocationPublicId.Value);
                    if (storageLocation == null)
                    {
                        throw new StorageLocationNotFoundException("Storage location not found");
                    }
                    storageLocationId = storageLocation.Id;
                }

                int? shoppingLocationId = shoppingListItem.ShoppingLocationId;

                var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);
                var currency = request.Currency ?? userProfile?.DefaultCurrency;

                // Create inventory item
                var inventoryItem = new Entities.Product.ProductInventoryItem
                {
                    ProductId = product.Id,
                    UserId = request.IsSharedWithFamily && familyId.HasValue ? null : userId.Value,
                    FamilyId = request.IsSharedWithFamily && familyId.HasValue ? familyId : null,
                    StorageLocationId = storageLocationId,
                    CurrentQuantity = request.Quantity,
                    Unit = shoppingListItem.Unit,
                    ExpirationAt = request.ExpirationAt
                };

                context.ProductInventoryItems.Add(inventoryItem);
                await context.SaveChangesAsync(cancellationToken);

                // Create purchase info
                Entities.Product.ProductPurchaseInfo? purchaseInfo = null;
                if (request.Price.HasValue || shoppingLocationId.HasValue)
                {
                    purchaseInfo = new Entities.Product.ProductPurchaseInfo
                    {
                        ProductInventoryItemId = inventoryItem.Id,
                        PurchasedAt = request.PurchasedAt,
                        OriginalQuantity = request.Quantity,
                        Price = request.Price,
                        Currency = currency,
                        ShoppingLocationId = shoppingLocationId
                    };

                    context.ProductPurchaseInfos.Add(purchaseInfo);
                    await context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} quick purchased shopping list item {shoppingListItem.Id} (PublicId: {shoppingListItem.PublicId}) and created inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId})");

                // Record activity
                try
                {
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListItemPurchase,
                        shoppingListItem.Id,
                        $"{shoppingList.Name} - {product.Name}",
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListItemPurchase activity for shopping list item {shoppingListItem.PublicId}");
                }

                var shoppingListItemInfo = new ShoppingListItemInfo
                {
                    PublicId = trackedShoppingListItem.PublicId,
                    ShoppingListPublicId = shoppingList.PublicId,
                    ProductPublicId = product.PublicId,
                    ShoppingLocationPublicId = shoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(shoppingLocationId)?.PublicId : null,
                    CustomName = trackedShoppingListItem.CustomName,
                    Quantity = trackedShoppingListItem.Quantity,
                    Unit = trackedShoppingListItem.Unit,
                    Note = trackedShoppingListItem.Note,
                    PurchasedAt = trackedShoppingListItem.PurchasedAt,
                    DeadlineAt = trackedShoppingListItem.DeadlineAt,
                    DueAt = trackedShoppingListItem.DueAt
                };

                return shoppingListItemInfo;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to quick purchase shopping list item {request.ShoppingListItemPublicId} for user {userId}");
                throw;
            }
        }

        public async Task<ShoppingListItemInfo> SimpleQuickPurchaseAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingListItem = GetShoppingListItemByPublicId(publicId);
            if (shoppingListItem == null)
            {
                throw new ShoppingListItemNotFoundException();
            }

            var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedShoppingListItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                if (trackedShoppingListItem == null)
                {
                    throw new ShoppingListItemNotFoundException();
                }

                trackedShoppingListItem.PurchasedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} quick purchased shopping list item {shoppingListItem.Id} (PublicId: {shoppingListItem.PublicId})");

                // Record activity
                try
                {
                    var displayName = shoppingListItem.ProductId.HasValue 
                        ? new ProductFunctions().GetProductById(shoppingListItem.ProductId)?.Name 
                        : trackedShoppingListItem.CustomName ?? "Item";

                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListItemQuickPurchase,
                        shoppingListItem.Id,
                        $"{shoppingList.Name} - {displayName}",
                        null,
                        null,
                        cancellationToken: cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListItemQuickPurchase activity for shopping list item {shoppingListItem.PublicId}");
                }

                // Refresh cache
                await RefreshShoppingListItemCacheAsync(shoppingListItem.Id, cancellationToken);

                var locationFunctions = new LocationFunctions();
                var productFunctions = new ProductFunctions();

                var shoppingListItemInfo = new ShoppingListItemInfo
                {
                    PublicId = trackedShoppingListItem.PublicId,
                    ShoppingListPublicId = shoppingList.PublicId,
                    ProductPublicId = shoppingListItem.ProductId.HasValue ? productFunctions.GetProductById(shoppingListItem.ProductId)?.PublicId : null,
                    ShoppingLocationPublicId = shoppingListItem.ShoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(shoppingListItem.ShoppingLocationId)?.PublicId : null,
                    CustomName = trackedShoppingListItem.CustomName,
                    Quantity = trackedShoppingListItem.Quantity,
                    Unit = trackedShoppingListItem.Unit,
                    Note = trackedShoppingListItem.Note,
                    PurchasedAt = trackedShoppingListItem.PurchasedAt,
                    DeadlineAt = trackedShoppingListItem.DeadlineAt,
                    DueAt = trackedShoppingListItem.DueAt
                };

                return shoppingListItemInfo;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to quick purchase shopping list item {publicId} for user {userId}");
                throw;
            }
        }

        public async Task<ShoppingListItemInfo> RestorePurchaseAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingListItem = GetShoppingListItemByPublicId(publicId);
            if (shoppingListItem == null)
            {
                throw new ShoppingListItemNotFoundException();
            }

            var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedShoppingListItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                if (trackedShoppingListItem == null)
                {
                    throw new ShoppingListItemNotFoundException();
                }

                trackedShoppingListItem.PurchasedAt = null;
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} restored purchase for shopping list item {shoppingListItem.Id} (PublicId: {shoppingListItem.PublicId})");

                // Record activity
                try
                {
                    var displayName = shoppingListItem.ProductId.HasValue 
                        ? new ProductFunctions().GetProductById(shoppingListItem.ProductId)?.Name 
                        : trackedShoppingListItem.CustomName ?? "Item";

                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ShoppingListItemRestorePurchase,
                        shoppingListItem.Id,
                        $"{shoppingList.Name} - {displayName}",
                        null,
                        null,
                        cancellationToken: cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ShoppingListItemRestorePurchase activity for shopping list item {shoppingListItem.PublicId}");
                }

                // Refresh cache
                await RefreshShoppingListItemCacheAsync(shoppingListItem.Id, cancellationToken);

                var locationFunctions = new LocationFunctions();
                var productFunctions = new ProductFunctions();

                var shoppingListItemInfo = new ShoppingListItemInfo
                {
                    PublicId = trackedShoppingListItem.PublicId,
                    ShoppingListPublicId = shoppingList.PublicId,
                    ProductPublicId = shoppingListItem.ProductId.HasValue ? productFunctions.GetProductById(shoppingListItem.ProductId)?.PublicId : null,
                    ShoppingLocationPublicId = shoppingListItem.ShoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(shoppingListItem.ShoppingLocationId)?.PublicId : null,
                    CustomName = trackedShoppingListItem.CustomName,
                    Quantity = trackedShoppingListItem.Quantity,
                    Unit = trackedShoppingListItem.Unit,
                    Note = trackedShoppingListItem.Note,
                    PurchasedAt = trackedShoppingListItem.PurchasedAt,
                    DeadlineAt = trackedShoppingListItem.DeadlineAt,
                    DueAt = trackedShoppingListItem.DueAt
                };

                return shoppingListItemInfo;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to restore purchase for shopping list item {publicId} for user {userId}");
                throw;
            }
        }

        public async Task<List<ShoppingListItemInfo>> CreateMultipleShoppingListItemsAsync(CreateMultipleShoppingListItemsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingList = GetShoppingListByPublicId(request.ShoppingListPublicId);
            if (shoppingList == null)
            {
                throw new ShoppingListNotFoundException();
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingList.UserId != userId.Value &&
                (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
            {
                throw new ShoppingListAccessDeniedException();
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                throw new BadRequestException("At least one item is required");
            }

            foreach (var item in request.Items)
            {
                if (string.IsNullOrWhiteSpace(item.CustomName) && !item.ProductPublicId.HasValue)
                {
                    throw new InvalidShoppingListItemException("Either CustomName or ProductPublicId must be provided for all items");
                }
            }

            var locationFunctions = new LocationFunctions();
            var productFunctions = new ProductFunctions();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var createdItems = new List<ShoppingListItem>();

                foreach (var item in request.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int? productId = null;
                    int? shoppingLocationId = null;

                    if (item.ProductPublicId.HasValue)
                    {
                        var product = productFunctions.GetProductByPublicId(item.ProductPublicId.Value);
                        if (product == null)
                        {
                            throw new ProductNotFoundException($"Product not found: {item.ProductPublicId}");
                        }
                        productId = product.Id;
                    }

                    if (item.ShoppingLocationPublicId.HasValue)
                    {
                        var shoppingLocation = locationFunctions.GetShoppingLocationByPublicId(item.ShoppingLocationPublicId.Value);
                        if (shoppingLocation == null)
                        {
                            throw new ShoppingLocationNotFoundException($"Shopping location not found: {item.ShoppingLocationPublicId}");
                        }
                        shoppingLocationId = shoppingLocation.Id;
                    }

                    var shoppingListItem = new ShoppingListItem
                    {
                        ShoppingListId = shoppingList.Id,
                        ProductId = productId,
                        ShoppingLocationId = shoppingLocationId,
                        CustomName = item.CustomName?.Trim(),
                        Quantity = item.Quantity,
                        Unit = item.Unit,
                        Note = item.Note?.Trim(),
                        DeadlineAt = item.DeadlineAt,
                        DueAt = item.DueAt
                    };

                    createdItems.Add(shoppingListItem);
                }

                context.ShoppingListItems.AddRange(createdItems);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created {createdItems.Count} shopping list items in shopping list {shoppingList.Id}");

                // Record activity for each created item
                foreach (var sli in createdItems)
                {
                    try
                    {
                        var itemName = !string.IsNullOrWhiteSpace(sli.CustomName) 
                            ? sli.CustomName 
                            : (sli.ProductId.HasValue ? productFunctions.GetProductById(sli.ProductId.Value)?.Name : null) ?? "Unknown";
                        
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ShoppingListItemAdd,
                            sli.Id,
                            $"{shoppingList.Name} - {itemName}",
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ShoppingListItemAdd activity for shopping list item {sli.PublicId}");
                    }
                }

                return createdItems.Select(sli => new ShoppingListItemInfo
                {
                    PublicId = sli.PublicId,
                    ShoppingListPublicId = shoppingList.PublicId,
                    ProductPublicId = sli.ProductId.HasValue ? productFunctions.GetProductById(sli.ProductId)?.PublicId : null,
                    ShoppingLocationPublicId = sli.ShoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(sli.ShoppingLocationId)?.PublicId : null,
                    CustomName = sli.CustomName,
                    Quantity = sli.Quantity,
                    Unit = sli.Unit,
                    Note = sli.Note,
                    PurchasedAt = sli.PurchasedAt,
                    DeadlineAt = sli.DeadlineAt,
                    DueAt = sli.DueAt
                }).ToList();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create multiple shopping list items for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteMultipleShoppingListItemsAsync(DeleteMultipleShoppingListItemsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (request.ItemPublicIds == null || request.ItemPublicIds.Count == 0)
            {
                throw new BadRequestException("At least one item is required");
            }

            var familyId = SessionInfo.GetFamilyId();
            var productFunctions = new ProductFunctions();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var itemPublicId in request.ItemPublicIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var shoppingListItem = GetShoppingListItemByPublicId(itemPublicId);
                    if (shoppingListItem == null)
                    {
                        throw new ShoppingListItemNotFoundException($"Shopping list item not found: {itemPublicId}");
                    }

                    var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
                    if (shoppingList == null)
                    {
                        throw new ShoppingListNotFoundException();
                    }

                    if (shoppingList.UserId != userId.Value &&
                        (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
                    {
                        throw new ShoppingListAccessDeniedException();
                    }

                    var trackedItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                    if (trackedItem == null)
                    {
                        throw new ShoppingListItemNotFoundException();
                    }

                    trackedItem.DeleteRecord(userId.Value);
                    context.ShoppingListItems.Update(trackedItem);

                    // Record activity for each deleted item
                    try
                    {
                        var itemName = !string.IsNullOrWhiteSpace(shoppingListItem.CustomName) 
                            ? shoppingListItem.CustomName 
                            : (shoppingListItem.ProductId.HasValue ? productFunctions.GetProductById(shoppingListItem.ProductId.Value)?.Name : null) ?? "Unknown";
                        
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ShoppingListItemDelete,
                            shoppingListItem.Id,
                            $"{shoppingList.Name} - {itemName}",
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ShoppingListItemDelete activity for shopping list item {shoppingListItem.PublicId}");
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted {request.ItemPublicIds.Count} shopping list items");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete multiple shopping list items for user {userId.Value}");
                throw;
            }
        }

        public async Task<List<ShoppingListItemInfo>> QuickPurchaseMultipleShoppingListItemsAsync(QuickPurchaseMultipleShoppingListItemsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                throw new BadRequestException("At least one item is required");
            }

            var familyId = SessionInfo.GetFamilyId();
            var locationFunctions = new LocationFunctions();
            var productFunctions = new ProductFunctions();
            var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var results = new List<ShoppingListItemInfo>();

                foreach (var itemRequest in request.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var shoppingListItem = GetShoppingListItemByPublicId(itemRequest.ShoppingListItemPublicId);
                    if (shoppingListItem == null)
                    {
                        throw new ShoppingListItemNotFoundException($"Shopping list item not found: {itemRequest.ShoppingListItemPublicId}");
                    }

                    if (!shoppingListItem.ProductId.HasValue)
                    {
                        throw new InvalidShoppingListItemException($"Cannot quick purchase a custom item: {itemRequest.ShoppingListItemPublicId}");
                    }

                    var shoppingList = GetShoppingListById(shoppingListItem.ShoppingListId);
                    if (shoppingList == null)
                    {
                        throw new ShoppingListNotFoundException();
                    }

                    if (shoppingList.UserId != userId.Value &&
                        (!familyId.HasValue || shoppingList.FamilyId != familyId.Value))
                    {
                        throw new ShoppingListAccessDeniedException();
                    }

                    var product = productFunctions.GetProductById(shoppingListItem.ProductId);
                    if (product == null)
                    {
                        throw new ProductNotFoundException("Product not found");
                    }

                    int? storageLocationId = null;
                    if (itemRequest.StorageLocationPublicId.HasValue)
                    {
                        var storageLocation = locationFunctions.GetStorageLocationByPublicId(itemRequest.StorageLocationPublicId.Value);
                        if (storageLocation == null)
                        {
                            throw new StorageLocationNotFoundException($"Storage location not found: {itemRequest.StorageLocationPublicId}");
                        }
                        storageLocationId = storageLocation.Id;
                    }

                    int? shoppingLocationId = shoppingListItem.ShoppingLocationId;
                    var currency = itemRequest.Currency ?? userProfile?.DefaultCurrency;

                    var trackedShoppingListItem = await context.ShoppingListItems.FindAsync([shoppingListItem.Id], cancellationToken);
                    if (trackedShoppingListItem == null)
                    {
                        throw new ShoppingListItemNotFoundException();
                    }

                    trackedShoppingListItem.PurchasedAt = itemRequest.PurchasedAt;

                    var inventoryItem = new Entities.Product.ProductInventoryItem
                    {
                        ProductId = product.Id,
                        UserId = itemRequest.IsSharedWithFamily && familyId.HasValue ? null : userId.Value,
                        FamilyId = itemRequest.IsSharedWithFamily && familyId.HasValue ? familyId : null,
                        StorageLocationId = storageLocationId,
                        CurrentQuantity = itemRequest.Quantity,
                        Unit = shoppingListItem.Unit,
                        ExpirationAt = itemRequest.ExpirationAt
                    };

                    context.ProductInventoryItems.Add(inventoryItem);
                    await context.SaveChangesAsync(cancellationToken);

                    if (itemRequest.Price.HasValue || shoppingLocationId.HasValue)
                    {
                        var purchaseInfo = new Entities.Product.ProductPurchaseInfo
                        {
                            ProductInventoryItemId = inventoryItem.Id,
                            PurchasedAt = itemRequest.PurchasedAt,
                            OriginalQuantity = itemRequest.Quantity,
                            Price = itemRequest.Price,
                            Currency = currency,
                            ShoppingLocationId = shoppingLocationId
                        };

                        context.ProductPurchaseInfos.Add(purchaseInfo);
                    }

                    // Record activity for each purchased item
                    try
                    {
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ShoppingListItemPurchase,
                            shoppingListItem.Id,
                            $"{shoppingList.Name} - {product.Name}",
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ShoppingListItemPurchase activity for shopping list item {shoppingListItem.PublicId}");
                    }

                    results.Add(new ShoppingListItemInfo
                    {
                        PublicId = trackedShoppingListItem.PublicId,
                        ShoppingListPublicId = shoppingList.PublicId,
                        ProductPublicId = product.PublicId,
                        ShoppingLocationPublicId = shoppingLocationId.HasValue ? locationFunctions.GetShoppingLocationById(shoppingLocationId)?.PublicId : null,
                        CustomName = trackedShoppingListItem.CustomName,
                        Quantity = trackedShoppingListItem.Quantity,
                        Unit = trackedShoppingListItem.Unit,
                        Note = trackedShoppingListItem.Note,
                        PurchasedAt = trackedShoppingListItem.PurchasedAt,
                        DeadlineAt = trackedShoppingListItem.DeadlineAt,
                        DueAt = trackedShoppingListItem.DueAt
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} quick purchased {results.Count} shopping list items");

                return results;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to quick purchase multiple shopping list items for user {userId.Value}");
                throw;
            }
        }
        #endregion

        #region ShoppingList Methods
        public PagedResult<ShoppingListInfo> GetAllShoppingLists(PaginationRequest pagination)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            List<ShoppingList> shoppingLists;

            if (Inited)
            {
                shoppingLists = _shoppingListCache.Values
                    .Where(s => s.UserId == userId.Value ||
                               (familyId.HasValue && s.FamilyId == familyId.Value))
                    .OrderByDescending(s => s.Id)
                    .ToList();
            }
            else
            {
                var context = new HomassyDbContext();
                shoppingLists = context.ShoppingLists
                    .Where(s => s.UserId == userId.Value ||
                               (familyId.HasValue && s.FamilyId == familyId.Value))
                    .OrderByDescending(s => s.Id)
                    .ToList();
            }

            // Apply search filter if SearchText is provided
            if (!string.IsNullOrWhiteSpace(pagination.SearchText))
            {
                var normalizedSearch = pagination.SearchText.NormalizeForSearch();
                shoppingLists = shoppingLists.Where(s =>
                    s.Name.NormalizeForSearch().Contains(normalizedSearch)
                ).ToList();
            }

            var shoppingListInfos = shoppingLists.Select(s => new ShoppingListInfo
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Description = s.Description,
                Color = s.Color
            });

            return shoppingListInfos.ToPagedResult(pagination);
        }
        #endregion
    }
}
