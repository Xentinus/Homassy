using Homassy.API.Context;
using Homassy.API.Entities.Location;
using Homassy.API.Entities.Product;
using Homassy.API.Entities.User;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Product;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class ProductFunctions
    {
        private const int EXPIRING_SOON_THRESHOLD_DAYS = 14;
        
        private static readonly ConcurrentDictionary<int, Product> _productCache = new();
        private static readonly ConcurrentDictionary<int, ProductCustomization> _customizationCache = new();
        private static readonly ConcurrentDictionary<int, ProductInventoryItem> _inventoryItemCache = new();
        private static readonly ConcurrentDictionary<int, ProductPurchaseInfo> _purchaseInfoCache = new();
        private static readonly ConcurrentDictionary<int, List<ProductConsumptionLog>> _consumptionLogCache = new();
        public static bool Inited = false;

        #region Cache Management
        #region Cache Initialization
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();

            try
            {
                // Load all product-related data
                var products = await context.Products.ToListAsync(cancellationToken);
                var customizations = await context.ProductCustomizations.ToListAsync(cancellationToken);
                var inventoryItems = await context.ProductInventoryItems.ToListAsync(cancellationToken);
                var purchaseInfos = await context.ProductPurchaseInfos.ToListAsync(cancellationToken);
                var consumptionLogs = await context.ProductConsumptionLogs.ToListAsync(cancellationToken);

                foreach (var product in products)
                {
                    _productCache[product.Id] = product;
                }

                foreach (var customization in customizations)
                {
                    _customizationCache[customization.Id] = customization;
                }

                foreach (var item in inventoryItems)
                {
                    _inventoryItemCache[item.Id] = item;
                }

                foreach (var purchase in purchaseInfos)
                {
                    _purchaseInfoCache[purchase.Id] = purchase;
                }

                // Populate consumption logs cache (grouped by inventory item)
                var groupedLogs = consumptionLogs.GroupBy(log => log.ProductInventoryItemId);
                foreach (var group in groupedLogs)
                {
                    _consumptionLogCache[group.Key] = group.ToList();
                }

                Inited = true;
                Log.Information($"Initialized product cache with {products.Count} products, {customizations.Count} customizations, {inventoryItems.Count} inventory items, {purchaseInfos.Count} purchases, and {consumptionLogs.Count} consumption logs.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize product cache.");
                throw;
            }
        }
        #endregion

        #region Cache Refreshing
        public async Task RefreshProductCacheAsync(int productId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var product = await context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
                var existsInCache = _productCache.ContainsKey(productId);

                if (product != null && existsInCache)
                {
                    _productCache[productId] = product;
                    Log.Debug($"Refreshed product {productId} in cache.");
                }
                else if (product != null && !existsInCache)
                {
                    _productCache[productId] = product;
                    Log.Debug($"Added product {productId} to cache.");
                }
                else if (product == null && existsInCache)
                {
                    _productCache.TryRemove(productId, out _);
                    Log.Debug($"Removed deleted product {productId} from cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for product {productId}.");
                throw;
            }
        }

        public async Task RefreshProductCustomizationCacheAsync(int customizationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var customization = await context.ProductCustomizations.FirstOrDefaultAsync(c => c.Id == customizationId, cancellationToken);
                var existsInCache = _customizationCache.ContainsKey(customizationId);

                if (customization != null && existsInCache)
                {
                    _customizationCache[customizationId] = customization;
                    Log.Debug($"Refreshed customization {customizationId} in cache.");
                }
                else if (customization != null && !existsInCache)
                {
                    _customizationCache[customizationId] = customization;
                    Log.Debug($"Added customization {customizationId} to cache.");
                }
                else if (customization == null && existsInCache)
                {
                    _customizationCache.TryRemove(customizationId, out _);
                    Log.Debug($"Removed deleted customization {customizationId} from cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for customization {customizationId}.");
                throw;
            }
        }

        public async Task RefreshInventoryItemCacheAsync(int inventoryItemId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var item = await context.ProductInventoryItems.FirstOrDefaultAsync(i => i.Id == inventoryItemId, cancellationToken);
                var existsInCache = _inventoryItemCache.ContainsKey(inventoryItemId);

                if (item != null && existsInCache)
                {
                    _inventoryItemCache[inventoryItemId] = item;
                    Log.Debug($"Refreshed inventory item {inventoryItemId} in cache.");
                }
                else if (item != null && !existsInCache)
                {
                    _inventoryItemCache[inventoryItemId] = item;
                    Log.Debug($"Added inventory item {inventoryItemId} to cache.");
                }
                else if (item == null && existsInCache)
                {
                    _inventoryItemCache.TryRemove(inventoryItemId, out _);
                    Log.Debug($"Removed deleted inventory item {inventoryItemId} from cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for inventory item {inventoryItemId}.");
                throw;
            }
        }

        public async Task RefreshPurchaseInfoCacheAsync(int purchaseInfoId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var purchase = await context.ProductPurchaseInfos.FirstOrDefaultAsync(p => p.Id == purchaseInfoId, cancellationToken);
                var existsInCache = _purchaseInfoCache.ContainsKey(purchaseInfoId);

                if (purchase != null && existsInCache)
                {
                    _purchaseInfoCache[purchaseInfoId] = purchase;
                    Log.Debug($"Refreshed purchase info {purchaseInfoId} in cache.");
                }
                else if (purchase != null && !existsInCache)
                {
                    _purchaseInfoCache[purchaseInfoId] = purchase;
                    Log.Debug($"Added purchase info {purchaseInfoId} to cache.");
                }
                else if (purchase == null && existsInCache)
                {
                    _purchaseInfoCache.TryRemove(purchaseInfoId, out _);
                    Log.Debug($"Removed deleted purchase info {purchaseInfoId} from cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for purchase info {purchaseInfoId}.");
                throw;
            }
        }

        public async Task RefreshConsumptionLogsCacheAsync(int inventoryItemId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var logs = await context.ProductConsumptionLogs
                    .Where(log => log.ProductInventoryItemId == inventoryItemId)
                    .ToListAsync(cancellationToken);

                if (logs.Any())
                {
                    _consumptionLogCache[inventoryItemId] = logs;
                    Log.Debug($"Refreshed consumption logs for inventory item {inventoryItemId} in cache ({logs.Count} logs).");
                }
                else
                {
                    _consumptionLogCache.TryRemove(inventoryItemId, out _);
                    Log.Debug($"Removed consumption logs for inventory item {inventoryItemId} from cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for consumption logs of inventory item {inventoryItemId}.");
                throw;
            }
        }
        #endregion

        #region Cache Getters - By Internal Id
        public Product? GetProductById(int? productId)
        {
            if (productId == null) return null;

            if (Inited && _productCache.TryGetValue((int)productId, out var product))
            {
                return product;
            }

            var context = new HomassyDbContext();
            return context.Products.FirstOrDefault(p => p.Id == productId);
        }

        public ProductInventoryItem? GetInventoryItemById(int? inventoryItemId)
        {
            if (inventoryItemId == null) return null;

            if (Inited && _inventoryItemCache.TryGetValue((int)inventoryItemId, out var item))
            {
                return item;
            }

            var context = new HomassyDbContext();
            return context.ProductInventoryItems.FirstOrDefault(i => i.Id == inventoryItemId);
        }

        public ProductCustomization? GetCustomizationById(int? customizationId)
        {
            if (customizationId == null) return null;

            if (Inited && _customizationCache.TryGetValue((int)customizationId, out var customization))
            {
                return customization;
            }

            var context = new HomassyDbContext();
            return context.ProductCustomizations.FirstOrDefault(c => c.Id == customizationId);
        }
        #endregion

        #region Cache Getters - By PublicId
        public Product? GetProductByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var product = _productCache.Values.FirstOrDefault(p => p.PublicId == publicId);
                if (product != null) return product;
            }

            var context = new HomassyDbContext();
            return context.Products.FirstOrDefault(p => p.PublicId == publicId);
        }

        public ProductInventoryItem? GetInventoryItemByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var item = _inventoryItemCache.Values.FirstOrDefault(i => i.PublicId == publicId);
                if (item != null) return item;
            }

            var context = new HomassyDbContext();
            return context.ProductInventoryItems.FirstOrDefault(i => i.PublicId == publicId);
        }

        public ProductCustomization? GetCustomizationByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var customization = _customizationCache.Values.FirstOrDefault(c => c.PublicId == publicId);
                if (customization != null) return customization;
            }

            var context = new HomassyDbContext();
            return context.ProductCustomizations.FirstOrDefault(c => c.PublicId == publicId);
        }

        public ProductPurchaseInfo? GetPurchaseInfoByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var purchase = _purchaseInfoCache.Values.FirstOrDefault(p => p.PublicId == publicId);
                if (purchase != null) return purchase;
            }

            var context = new HomassyDbContext();
            return context.ProductPurchaseInfos.FirstOrDefault(p => p.PublicId == publicId);
        }

        public ProductConsumptionLog? GetConsumptionLogByPublicId(Guid publicId)
        {
            if (Inited)
            {
                foreach (var logs in _consumptionLogCache.Values)
                {
                    var log = logs.FirstOrDefault(l => l.PublicId == publicId);
                    if (log != null) return log;
                }
            }

            var context = new HomassyDbContext();
            return context.ProductConsumptionLogs.FirstOrDefault(l => l.PublicId == publicId);
        }
        #endregion

        #region Cache Getters - Complex Queries
        public ProductCustomization? GetCustomizationByProductAndUser(int productId, int userId)
        {
            if (Inited)
            {
                return _customizationCache.Values
                    .FirstOrDefault(c => c.ProductId == productId &&
                                        (c.UserId == userId));
            }

            var context = new HomassyDbContext();
            return context.ProductCustomizations
                .FirstOrDefault(c => c.ProductId == productId &&
                                    (c.UserId == userId));
        }

        public ProductPurchaseInfo? GetPurchaseInfoByInventoryItemId(int inventoryItemId)
        {
            if (Inited)
            {
                return _purchaseInfoCache.Values
                    .FirstOrDefault(p => p.ProductInventoryItemId == inventoryItemId);
            }

            var context = new HomassyDbContext();
            return context.ProductPurchaseInfos
                .FirstOrDefault(p => p.ProductInventoryItemId == inventoryItemId);
        }

        public List<ProductConsumptionLog> GetConsumptionLogsByInventoryItemId(int inventoryItemId)
        {
            if (Inited && _consumptionLogCache.TryGetValue(inventoryItemId, out var logs))
            {
                return logs;
            }

            var context = new HomassyDbContext();
            return context.ProductConsumptionLogs
                .Where(log => log.ProductInventoryItemId == inventoryItemId)
                .OrderBy(log => log.ConsumedAt)
                .ToList();
        }
        #endregion

        #region Cache Getters - Multiple Items
        public List<ProductInventoryItem> GetInventoryItemsByProductId(int productId, bool includeConsumed = false)
        {
            if (Inited)
            {
                var items = _inventoryItemCache.Values
                    .Where(i => i.ProductId == productId);

                if (!includeConsumed)
                {
                    items = items.Where(i => !i.IsFullyConsumed);
                }

                return items.ToList();
            }

            var context = new HomassyDbContext();
            var query = context.ProductInventoryItems
                .Where(i => i.ProductId == productId);

            if (!includeConsumed)
            {
                query = query.Where(i => !i.IsFullyConsumed);
            }

            return query.ToList();
        }

        public List<ProductInventoryItem> GetInventoryItemsByUserAndFamily(int userId, int? familyId)
        {
            if (Inited)
            {
                return _inventoryItemCache.Values
                    .Where(i => !i.IsFullyConsumed &&
                               (i.UserId == userId || (familyId.HasValue && i.FamilyId == familyId)))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ProductInventoryItems
                .Where(i => !i.IsFullyConsumed &&
                           (i.UserId == userId || (familyId.HasValue && i.FamilyId == familyId)))
                .ToList();
        }

        public List<Product> GetProductsByUserAndFamily(int userId, int? familyId)
        {
            var inventoryItems = GetInventoryItemsByUserAndFamily(userId, familyId);
            var productIds = inventoryItems.Select(i => i.ProductId).Distinct().ToList();

            if (Inited)
            {
                return _productCache.Values
                    .Where(p => !p.IsDeleted && productIds.Contains(p.Id))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.Products
                .Where(p => !p.IsDeleted && productIds.Contains(p.Id))
                .ToList();
        }

        public List<ProductCustomization> GetCustomizationsByUser(int userId, int? familyId)
        {
            if (Inited)
            {
                return _customizationCache.Values
                    .Where(c => c.UserId == userId)
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ProductCustomizations
                .Where(c => c.UserId == userId)
                .ToList();
        }
        #endregion
        #endregion

        #region Product Methods
        public PagedResult<ProductInfo> GetAllProducts(PaginationRequest pagination)
        {
            var userId = SessionInfo.GetUserId();
            List<Product> products;

            if (Inited)
            {
                products = _productCache.Values
                    .ToList();
            }
            else
            {
                var context = new HomassyDbContext();
                products = context.Products
                    .ToList();
            }

            // Apply search filter if SearchText is provided
            if (!string.IsNullOrWhiteSpace(pagination.SearchText))
            {
                var normalizedSearch = pagination.SearchText.NormalizeForSearch();
                products = products.Where(p =>
                    p.Name.NormalizeForSearch().Contains(normalizedSearch) ||
                    p.Brand.NormalizeForSearch().Contains(normalizedSearch) ||
                    p.Barcode.NormalizeForSearch().Contains(normalizedSearch)
                ).ToList();
            }

            var productInfos = products.Select(p =>
            {
                var customization = userId.HasValue ? GetCustomizationByProductAndUser(p.Id, userId.Value) : null;

                return new ProductInfo
                {
                    PublicId = p.PublicId,
                    Name = p.Name,
                    Brand = p.Brand,
                    Category = p.Category,
                    Barcode = p.Barcode,
                    ProductPictureBase64 = p.ProductPictureBase64,
                    IsEatable = p.IsEatable,
                    IsFavorite = customization?.IsFavorite ?? false
                };
            });

            return productInfos.ToPagedResult(pagination);
        }

        public async Task<ProductInfo> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var product = new Product
                {
                    Name = request.Name.Trim(),
                    Brand = request.Brand.Trim(),
                    Category = request.Category?.Trim(),
                    Barcode = request.Barcode?.Trim(),
                    IsEatable = request.IsEatable
                };

                context.Products.Add(product);
                await context.SaveChangesAsync(cancellationToken);

                bool isFavorite = false;

                // Create ProductCustomization if Notes or IsFavorite are set
                if (!string.IsNullOrWhiteSpace(request.Notes) || request.IsFavorite)
                {
                    var customization = new ProductCustomization
                    {
                        ProductId = product.Id,
                        UserId = userId,
                        Notes = request.Notes?.Trim(),
                        IsFavorite = request.IsFavorite
                    };

                    context.ProductCustomizations.Add(customization);
                    await context.SaveChangesAsync(cancellationToken);

                    isFavorite = request.IsFavorite;

                    Log.Information($"User {userId} created product {product.Id} (PublicId: {product.PublicId}) with customization");
                }
                else
                {
                    Log.Information($"User {userId} created product {product.Id} (PublicId: {product.PublicId}) without customization");
                }

                await transaction.CommitAsync(cancellationToken);

                // Record activity
                try
                {
                    var familyId = SessionInfo.GetFamilyId();
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductCreate,
                        product.Id,
                        product.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductCreate activity for product {product.Name}");
                }

                return new ProductInfo
                {
                    PublicId = product.PublicId,
                    Name = product.Name,
                    Brand = product.Brand,
                    Category = product.Category,
                    Barcode = product.Barcode,
                    ProductPictureBase64 = product.ProductPictureBase64,
                    IsEatable = product.IsEatable,
                    IsFavorite = isFavorite
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create product for user {userId}");
                throw;
            }
        }

        public async Task<ProductInfo> UpdateProductAsync(Guid productPublicId, UpdateProductRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var product = GetProductByPublicId(productPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                bool hasChanges = false;

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    product.Name = request.Name.Trim();
                    hasChanges = true;
                }

                if (!string.IsNullOrWhiteSpace(request.Brand))
                {
                    product.Brand = request.Brand.Trim();
                    hasChanges = true;
                }

                if (request.Category != null)
                {
                    product.Category = string.IsNullOrWhiteSpace(request.Category)
                        ? null
                        : request.Category.Trim();
                    hasChanges = true;
                }

                if (request.Barcode != null)
                {
                    product.Barcode = string.IsNullOrWhiteSpace(request.Barcode)
                        ? null
                        : request.Barcode.Trim();
                    hasChanges = true;
                }

                if (request.IsEatable.HasValue)
                {
                    product.IsEatable = request.IsEatable.Value;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    context.Products.Update(product);
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information($"User {userId} updated product {product.Id} (PublicId: {product.PublicId})");
                }
                else
                {
                    Log.Debug($"User {userId} attempted to update product {product.Id} but no changes were made");
                }

                await transaction.CommitAsync(cancellationToken);

                // Record activity only if changes were made
                if (hasChanges)
                {
                    try
                    {
                        var familyId = SessionInfo.GetFamilyId();
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ProductUpdate,
                            product.Id,
                            product.Name,
                            null,
                            null,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ProductUpdate activity for product {product.Name}");
                    }
                }

                var customization = GetCustomizationByProductAndUser(product.Id, userId.Value);

                return new ProductInfo
                {
                    PublicId = product.PublicId,
                    Name = product.Name,
                    Brand = product.Brand,
                    Category = product.Category,
                    Barcode = product.Barcode,
                    ProductPictureBase64 = product.ProductPictureBase64,
                    IsEatable = product.IsEatable,
                    IsFavorite = customization?.IsFavorite ?? false
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to update product {productPublicId} for user {userId}");
                throw;
            }
        }

        public async Task DeleteProductAsync(Guid productPublicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var product = GetProductByPublicId(productPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                product.DeleteRecord(userId.Value);

                context.Products.Update(product);
                await context.SaveChangesAsync(cancellationToken);

                Log.Information($"User {userId} deleted product {product.Id} (PublicId: {product.PublicId})");
                await transaction.CommitAsync(cancellationToken);

                // Record activity
                try
                {
                    var familyId = SessionInfo.GetFamilyId();
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductDelete,
                        product.Id,
                        product.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductDelete activity for product {product.Name}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete product {productPublicId} for user {userId}");
                throw;
            }
        }

        public async Task<ProductInfo> ToggleFavoriteAsync(Guid productPublicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var product = GetProductByPublicId(productPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var customization = GetCustomizationByProductAndUser(product.Id, userId.Value);                

                bool newFavoriteStatus;

                if (customization == null)
                {
                    customization = new ProductCustomization
                    {
                        ProductId = product.Id,
                        UserId = userId.Value,
                        IsFavorite = true
                    };

                    context.ProductCustomizations.Add(customization);
                    newFavoriteStatus = true;

                    Log.Information($"User {userId} created customization for product {product.Id} with favorite status true");
                }
                else
                {
                    customization.IsFavorite = !customization.IsFavorite;
                    context.ProductCustomizations.Update(customization);
                    newFavoriteStatus = customization.IsFavorite;

                    Log.Information($"User {userId} toggled favorite status for product {product.Id} to {newFavoriteStatus}");
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ProductInfo
                {
                    PublicId = product.PublicId,
                    Name = product.Name,
                    Brand = product.Brand,
                    Category = product.Category,
                    Barcode = product.Barcode,
                    ProductPictureBase64 = product.ProductPictureBase64,
                    IsEatable = product.IsEatable,
                    IsFavorite = newFavoriteStatus
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to toggle favorite for product {productPublicId} for user {userId}");
                throw;
            }
        }

        public DetailedProductInfo? GetDetailedProductInfo(Guid productPublicId)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var product = GetProductByPublicId(productPublicId);
            if (product == null)
            {
                return null;
            }

            var customization = GetCustomizationByProductAndUser(product.Id, userId.Value);
            var inventoryItems = GetInventoryItemsByProductId(product.Id, includeConsumed: false);
            var locationFunctions = new LocationFunctions();

            var inventoryItemInfos = inventoryItems.Select(item =>
            {
                var purchaseInfo = GetPurchaseInfoByInventoryItemId(item.Id);
                var consumptionLogs = GetConsumptionLogsByInventoryItemId(item.Id);

                return new InventoryItemInfo
                {
                    PublicId = item.PublicId,
                    CurrentQuantity = item.CurrentQuantity,
                    Unit = item.Unit,
                    ExpirationAt = item.ExpirationAt,
                    StorageLocation = item.StorageLocationId.HasValue
                        ? (locationFunctions.GetStorageLocationById(item.StorageLocationId.Value) is var storageLocation && storageLocation != null
                            ? new LocationInfo
                            {
                                PublicId = storageLocation.PublicId,
                                Name = storageLocation.Name
                            }
                            : null)
                        : null,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                            ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocation && shoppingLocation != null
                                ? new LocationInfo
                                {
                                    PublicId = shoppingLocation.PublicId,
                                    Name = shoppingLocation.Name
                                }
                                : null)
                            : null
                    } : null,
                    ConsumptionLogs = consumptionLogs.Select(log =>
                    {
                        var user = log.UserId.HasValue ? new UserFunctions().GetUserById(log.UserId.Value) : null;

                        return new ConsumptionLogInfo
                        {
                            PublicId = log.PublicId,
                            UserName = user?.Name,
                            ConsumedQuantity = log.ConsumedQuantity,
                            RemainingQuantity = log.RemainingQuantity,
                            ConsumedAt = log.ConsumedAt
                        };
                    }).ToList()
                };
            }).ToList();

            return new DetailedProductInfo
            {
                PublicId = product.PublicId,
                Name = product.Name,
                Brand = product.Brand,
                Category = product.Category,
                Barcode = product.Barcode,
                ProductPictureBase64 = product.ProductPictureBase64,
                IsEatable = product.IsEatable,
                IsFavorite = customization?.IsFavorite ?? false,
                InventoryItems = inventoryItemInfos
            };
        }

        public PagedResult<DetailedProductInfo> GetAllDetailedProductsForUser(PaginationRequest pagination)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();

            var products = GetProductsByUserAndFamily(userId.Value, familyId);

            // Apply search filter if SearchText is provided
            if (!string.IsNullOrWhiteSpace(pagination.SearchText))
            {
                var normalizedSearch = pagination.SearchText.NormalizeForSearch();
                products = products.Where(p =>
                    p.Name.NormalizeForSearch().Contains(normalizedSearch) ||
                    p.Brand.NormalizeForSearch().Contains(normalizedSearch) ||
                    p.Barcode.NormalizeForSearch().Contains(normalizedSearch)
                ).ToList();
            }

            var detailedProductInfos = products.Select(product =>
            {
                var customization = GetCustomizationByProductAndUser(product.Id, userId.Value);
                var allInventoryItems = GetInventoryItemsByProductId(product.Id, includeConsumed: false);

                var userInventoryItems = allInventoryItems
                    .Where(item => item.UserId == userId.Value ||
                                  (familyId.HasValue && item.FamilyId == familyId.Value))
                    .ToList();

                var locationFunctions = new LocationFunctions();

                var inventoryItemInfos = userInventoryItems.Select(item =>
                {
                    var purchaseInfo = GetPurchaseInfoByInventoryItemId(item.Id);
                    var consumptionLogs = GetConsumptionLogsByInventoryItemId(item.Id);

                    return new InventoryItemInfo
                    {
                        PublicId = item.PublicId,
                        CurrentQuantity = item.CurrentQuantity,
                        Unit = item.Unit,
                        ExpirationAt = item.ExpirationAt,
                        StorageLocation = item.StorageLocationId.HasValue
                            ? (locationFunctions.GetStorageLocationById(item.StorageLocationId.Value) is var storageLocation && storageLocation != null
                                ? new LocationInfo
                                {
                                    PublicId = storageLocation.PublicId,
                                    Name = storageLocation.Name
                                }
                                : null)
                            : null,
                        PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                        {
                            PublicId = purchaseInfo.PublicId,
                            PurchasedAt = purchaseInfo.PurchasedAt,
                            OriginalQuantity = purchaseInfo.OriginalQuantity,
                            Price = purchaseInfo.Price,
                            Currency = purchaseInfo.Currency,
                            ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                                ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocation && shoppingLocation != null
                                    ? new LocationInfo
                                    {
                                        PublicId = shoppingLocation.PublicId,
                                        Name = shoppingLocation.Name
                                    }
                                    : null)
                                : null
                        } : null,
                        ConsumptionLogs = consumptionLogs.Select(log =>
                        {
                            var user = log.UserId.HasValue ? new UserFunctions().GetUserById(log.UserId.Value) : null;

                            return new ConsumptionLogInfo
                            {
                                PublicId = log.PublicId,
                                UserName = user?.Name,
                                ConsumedQuantity = log.ConsumedQuantity,
                                RemainingQuantity = log.RemainingQuantity,
                                ConsumedAt = log.ConsumedAt
                            };
                        }).ToList()
                    };
                }).ToList();

                return new DetailedProductInfo
                {
                    PublicId = product.PublicId,
                    Name = product.Name,
                    Brand = product.Brand,
                    Category = product.Category,
                    Barcode = product.Barcode,
                    ProductPictureBase64 = product.ProductPictureBase64,
                    IsEatable = product.IsEatable,
                    IsFavorite = customization?.IsFavorite ?? false,
                    InventoryItems = inventoryItemInfos
                };
            });

            return detailedProductInfos.ToPagedResult(pagination);
        }

        public async Task<int> GetExpiringAndExpiredInventoryCountAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                return 0;
            }

            var familyId = SessionInfo.GetFamilyId();
            var today = DateTime.UtcNow.Date;
            var expiringThreshold = today.AddDays(EXPIRING_SOON_THRESHOLD_DAYS);

            int count;

            if (Inited)
            {
                // Use cache
                count = _inventoryItemCache.Values
                    .Where(i => !i.IsDeleted && !i.IsFullyConsumed && i.ExpirationAt.HasValue)
                    .Where(i => familyId.HasValue 
                        ? (i.UserId == userId || i.FamilyId == familyId)
                        : i.UserId == userId)
                    .Count(i => i.ExpirationAt!.Value.Date <= expiringThreshold);
            }
            else
            {
                // Use database
                var context = new HomassyDbContext();
                var query = context.ProductInventoryItems
                    .AsNoTracking()
                    .Include(i => i.Product)
                    .Where(i => !i.IsDeleted && !i.Product!.IsDeleted && !i.IsFullyConsumed && i.ExpirationAt.HasValue);

                if (familyId.HasValue)
                {
                    query = query.Where(i => i.UserId == userId || i.FamilyId == familyId);
                }
                else
                {
                    query = query.Where(i => i.UserId == userId);
                }

                count = await query.CountAsync(i => i.ExpirationAt!.Value.Date <= expiringThreshold, cancellationToken);
            }

            return count;
        }
        #endregion

        #region InventoryItem Methods
        public async Task<InventoryItemInfo> CreateInventoryItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var product = GetProductByPublicId(request.ProductPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var locationFunctions = new LocationFunctions();
            int? storageLocationId = null;
            int? shoppingLocationId = null;

            if (request.StorageLocationPublicId.HasValue)
            {
                var storageLocation = locationFunctions.GetStorageLocationByPublicId(request.StorageLocationPublicId.Value);
                if (storageLocation == null)
                {
                    throw new StorageLocationNotFoundException("Storage location not found");
                }
                storageLocationId = storageLocation.Id;
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

            var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);
            var currency = request.Currency ?? userProfile?.DefaultCurrency;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var inventoryItem = new ProductInventoryItem
                {
                    ProductId = product.Id,
                    UserId = request.IsSharedWithFamily && familyId.HasValue ? null : userId.Value,
                    FamilyId = request.IsSharedWithFamily && familyId.HasValue ? familyId : null,
                    StorageLocationId = storageLocationId,
                    CurrentQuantity = request.Quantity,
                    Unit = request.Unit,
                    ExpirationAt = request.ExpirationAt
                };

                context.ProductInventoryItems.Add(inventoryItem);
                await context.SaveChangesAsync(cancellationToken);

                ProductPurchaseInfo? purchaseInfo = null;
                if (request.Price.HasValue || request.ShoppingLocationPublicId.HasValue || !string.IsNullOrWhiteSpace(request.ReceiptNumber))
                {
                    purchaseInfo = new ProductPurchaseInfo
                    {
                        ProductInventoryItemId = inventoryItem.Id,
                        OriginalQuantity = request.Quantity,
                        Price = request.Price,
                        Currency = currency,
                        ShoppingLocationId = shoppingLocationId,
                        ReceiptNumber = request.ReceiptNumber?.Trim()
                    };

                    context.ProductPurchaseInfos.Add(purchaseInfo);
                    await context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} created inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId}) for product {product.Id}");

                // Record activity
                try
                {
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductInventoryCreate,
                        inventoryItem.Id,
                        product.Name,
                        inventoryItem.Unit,
                        inventoryItem.CurrentQuantity,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductInventoryCreate activity for inventory item {inventoryItem.PublicId}");
                }

                return new InventoryItemInfo
                {
                    PublicId = inventoryItem.PublicId,
                    CurrentQuantity = inventoryItem.CurrentQuantity,
                    Unit = inventoryItem.Unit,
                    ExpirationAt = inventoryItem.ExpirationAt,
                    StorageLocation = inventoryItem.StorageLocationId.HasValue
                        ? (locationFunctions.GetStorageLocationById(inventoryItem.StorageLocationId.Value) is var storageLocation && storageLocation != null
                            ? new LocationInfo
                            {
                                PublicId = storageLocation.PublicId,
                                Name = storageLocation.Name
                            }
                            : null)
                        : null,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                            ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocation && shoppingLocation != null
                                ? new LocationInfo
                                {
                                    PublicId = shoppingLocation.PublicId,
                                    Name = shoppingLocation.Name
                                }
                                : null)
                            : null
                    } : null,
                    ConsumptionLogs = new List<ConsumptionLogInfo>()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create inventory item for user {userId}");
                throw;
            }
        }

        public async Task<InventoryItemInfo> QuickAddInventoryItemAsync(QuickAddInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var product = GetProductByPublicId(request.ProductPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var inventoryItem = new ProductInventoryItem
                {
                    ProductId = product.Id,
                    UserId = request.IsSharedWithFamily && familyId.HasValue ? null : userId.Value,
                    FamilyId = request.IsSharedWithFamily && familyId.HasValue ? familyId : null,
                    CurrentQuantity = request.Quantity,
                    Unit = request.Unit
                };

                context.ProductInventoryItems.Add(inventoryItem);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} quick-added inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId}) for product {product.Id}");

                // Record activity
                try
                {
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductInventoryCreate,
                        inventoryItem.Id,
                        product.Name,
                        inventoryItem.Unit,
                        inventoryItem.CurrentQuantity,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductInventoryCreate activity for inventory item {inventoryItem.PublicId}");
                }

                return new InventoryItemInfo
                {
                    PublicId = inventoryItem.PublicId,
                    CurrentQuantity = inventoryItem.CurrentQuantity,
                    Unit = inventoryItem.Unit,
                    ExpirationAt = inventoryItem.ExpirationAt,
                    PurchaseInfo = null,
                    ConsumptionLogs = new List<ConsumptionLogInfo>()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to quick-add inventory item for user {userId}");
                throw;
            }
        }

        public async Task<InventoryItemInfo> UpdateInventoryItemAsync(Guid inventoryItemPublicId, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var inventoryItem = GetInventoryItemByPublicId(inventoryItemPublicId);
            if (inventoryItem == null)
            {
                throw new ProductInventoryItemNotFoundException();
            }

            if (inventoryItem.UserId != userId.Value && 
                (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to update this inventory item");
            }

            var locationFunctions = new LocationFunctions();
            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                if (trackedItem == null)
                {
                    throw new ProductInventoryItemNotFoundException();
                }

                bool hasChanges = false;

                if (request.StorageLocationPublicId.HasValue)
                {
                    var storageLocation = locationFunctions.GetStorageLocationByPublicId(request.StorageLocationPublicId.Value);
                    if (storageLocation == null)
                    {
                        throw new StorageLocationNotFoundException("Storage location not found");
                    }
                    trackedItem.StorageLocationId = storageLocation.Id;
                    hasChanges = true;
                }

                if (request.Quantity.HasValue)
                {
                    trackedItem.CurrentQuantity = request.Quantity.Value;
                    hasChanges = true;
                }

                if (request.Unit.HasValue)
                {
                    trackedItem.Unit = request.Unit.Value;
                    hasChanges = true;
                }

                if (request.ExpirationAt.HasValue)
                {
                    trackedItem.ExpirationAt = request.ExpirationAt.Value;
                    hasChanges = true;
                }

                if (request.IsSharedWithFamily.HasValue)
                {
                    if (request.IsSharedWithFamily.Value && familyId.HasValue)
                    {
                        trackedItem.UserId = null;
                        trackedItem.FamilyId = familyId;
                    }
                    else
                    {
                        trackedItem.UserId = userId.Value;
                        trackedItem.FamilyId = null;
                    }
                    hasChanges = true;
                }

                var purchaseInfo = GetPurchaseInfoByInventoryItemId(inventoryItem.Id);
                bool hasPurchaseChanges = request.Price.HasValue || request.Currency.HasValue || 
                                          request.ShoppingLocationPublicId.HasValue || request.ReceiptNumber != null;

                if (hasPurchaseChanges)
                {
                    if (purchaseInfo == null)
                    {
                        var userProfile = new UserFunctions().GetUserProfileByUserId(userId.Value);
                        purchaseInfo = new ProductPurchaseInfo
                        {
                            ProductInventoryItemId = inventoryItem.Id,
                            OriginalQuantity = trackedItem.CurrentQuantity,
                            Price = request.Price,
                            Currency = request.Currency ?? userProfile?.DefaultCurrency,
                            ReceiptNumber = request.ReceiptNumber?.Trim()
                        };

                        if (request.ShoppingLocationPublicId.HasValue)
                        {
                            var shoppingLocation = locationFunctions.GetShoppingLocationByPublicId(request.ShoppingLocationPublicId.Value);
                            if (shoppingLocation == null)
                            {
                                throw new ShoppingLocationNotFoundException("Shopping location not found");
                            }
                            purchaseInfo.ShoppingLocationId = shoppingLocation.Id;
                        }

                        context.ProductPurchaseInfos.Add(purchaseInfo);
                    }
                    else
                    {
                        var trackedPurchase = await context.ProductPurchaseInfos.FindAsync([purchaseInfo.Id], cancellationToken);
                        if (trackedPurchase != null)
                        {
                            if (request.Price.HasValue)
                                trackedPurchase.Price = request.Price.Value;
                            if (request.Currency.HasValue)
                                trackedPurchase.Currency = request.Currency.Value;
                            if (request.ReceiptNumber != null)
                                trackedPurchase.ReceiptNumber = string.IsNullOrWhiteSpace(request.ReceiptNumber) ? null : request.ReceiptNumber.Trim();
                            if (request.ShoppingLocationPublicId.HasValue)
                            {
                                var shoppingLocation = locationFunctions.GetShoppingLocationByPublicId(request.ShoppingLocationPublicId.Value);
                                if (shoppingLocation == null)
                                {
                                    throw new ShoppingLocationNotFoundException("Shopping location not found");
                                }
                                trackedPurchase.ShoppingLocationId = shoppingLocation.Id;
                            }
                            purchaseInfo = trackedPurchase;
                        }
                    }
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information($"User {userId} updated inventory item {trackedItem.Id} (PublicId: {trackedItem.PublicId})");
                }

                await transaction.CommitAsync(cancellationToken);

                // Record activity only if changes were made
                if (hasChanges)
                {
                    try
                    {
                        var product = GetProductById(trackedItem.ProductId);
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ProductInventoryUpdate,
                            trackedItem.Id,
                            product?.Name ?? "Unknown",
                            trackedItem.Unit,
                            trackedItem.CurrentQuantity,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ProductInventoryUpdate activity for inventory item {trackedItem.PublicId}");
                    }
                }

                var consumptionLogs = GetConsumptionLogsByInventoryItemId(trackedItem.Id);

                return new InventoryItemInfo
                {
                    PublicId = trackedItem.PublicId,
                    CurrentQuantity = trackedItem.CurrentQuantity,
                    Unit = trackedItem.Unit,
                    ExpirationAt = trackedItem.ExpirationAt,
                    StorageLocation = trackedItem.StorageLocationId.HasValue
                        ? (locationFunctions.GetStorageLocationById(trackedItem.StorageLocationId.Value) is var storageLocationRef && storageLocationRef != null
                            ? new LocationInfo
                            {
                                PublicId = storageLocationRef.PublicId,
                                Name = storageLocationRef.Name
                            }
                            : null)
                        : null,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                            ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocationRef && shoppingLocationRef != null
                                ? new LocationInfo
                                {
                                    PublicId = shoppingLocationRef.PublicId,
                                    Name = shoppingLocationRef.Name
                                }
                                : null)
                            : null
                    } : null,
                    ConsumptionLogs = consumptionLogs.Select(log =>
                    {
                        var user = log.UserId.HasValue ? new UserFunctions().GetUserById(log.UserId.Value) : null;
                        return new ConsumptionLogInfo
                        {
                            PublicId = log.PublicId,
                            UserName = user?.Name,
                            ConsumedQuantity = log.ConsumedQuantity,
                            RemainingQuantity = log.RemainingQuantity,
                            ConsumedAt = log.ConsumedAt
                        };
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to update inventory item {inventoryItemPublicId} for user {userId}");
                throw;
            }
        }

        public async Task DeleteInventoryItemAsync(Guid inventoryItemPublicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var inventoryItem = GetInventoryItemByPublicId(inventoryItemPublicId);
            if (inventoryItem == null)
            {
                throw new ProductInventoryItemNotFoundException();
            }

            if (inventoryItem.UserId != userId.Value && 
                (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to delete this inventory item");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                if (trackedItem == null)
                {
                    throw new ProductInventoryItemNotFoundException();
                }

                trackedItem.DeleteRecord(userId.Value);

                context.ProductInventoryItems.Update(trackedItem);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} deleted inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId})");

                // Record activity
                try
                {
                    var product = GetProductById(inventoryItem.ProductId);
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductInventoryDelete,
                        inventoryItem.Id,
                        product?.Name ?? "Unknown",
                        inventoryItem.Unit,
                        inventoryItem.CurrentQuantity,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductInventoryDelete activity for inventory item {inventoryItem.PublicId}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete inventory item {inventoryItemPublicId} for user {userId}");
                throw;
            }
        }

        public async Task<InventoryItemInfo> ConsumeInventoryItemAsync(Guid inventoryItemPublicId, ConsumeInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var inventoryItem = GetInventoryItemByPublicId(inventoryItemPublicId);
            if (inventoryItem == null)
            {
                throw new ProductInventoryItemNotFoundException();
            }

            if (inventoryItem.UserId != userId.Value && 
                (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to consume this inventory item");
            }

            if (inventoryItem.IsFullyConsumed)
            {
                throw new BadRequestException("This inventory item is already fully consumed");
            }

            if (request.Quantity > inventoryItem.CurrentQuantity)
            {
                throw new BadRequestException($"Cannot consume more than available quantity ({inventoryItem.CurrentQuantity})");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                if (trackedItem == null)
                {
                    throw new ProductInventoryItemNotFoundException();
                }

                var remainingQuantity = trackedItem.CurrentQuantity - request.Quantity;

                var consumptionLog = new ProductConsumptionLog
                {
                    ProductInventoryItemId = trackedItem.Id,
                    UserId = userId.Value,
                    ConsumedQuantity = request.Quantity,
                    RemainingQuantity = remainingQuantity
                };

                context.ProductConsumptionLogs.Add(consumptionLog);

                trackedItem.CurrentQuantity = remainingQuantity;
                if (remainingQuantity <= 0)
                {
                    trackedItem.IsFullyConsumed = true;
                    trackedItem.FullyConsumedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} consumed {request.Quantity} from inventory item {trackedItem.Id} (PublicId: {trackedItem.PublicId}), remaining: {remainingQuantity}");

                // Record activity
                try
                {
                    var product = GetProductById(trackedItem.ProductId);
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductInventoryDecrease,
                        trackedItem.Id,
                        product?.Name ?? "Unknown",
                        trackedItem.Unit,
                        request.Quantity,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductInventoryDecrease activity for inventory item {trackedItem.PublicId}");
                }

                var purchaseInfo = GetPurchaseInfoByInventoryItemId(trackedItem.Id);
                var consumptionLogs = GetConsumptionLogsByInventoryItemId(trackedItem.Id);
                var locationFunctions = new LocationFunctions();

                return new InventoryItemInfo
                {
                    PublicId = trackedItem.PublicId,
                    CurrentQuantity = trackedItem.CurrentQuantity,
                    Unit = trackedItem.Unit,
                    ExpirationAt = trackedItem.ExpirationAt,
                    StorageLocation = trackedItem.StorageLocationId.HasValue
                        ? (locationFunctions.GetStorageLocationById(trackedItem.StorageLocationId.Value) is var storageLocation && storageLocation != null
                            ? new LocationInfo
                            {
                                PublicId = storageLocation.PublicId,
                                Name = storageLocation.Name
                            }
                            : null)
                        : null,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                            ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocation && shoppingLocation != null
                                ? new LocationInfo
                                {
                                    PublicId = shoppingLocation.PublicId,
                                    Name = shoppingLocation.Name
                                }
                                : null)
                            : null
                    } : null,
                    ConsumptionLogs = consumptionLogs.Select(log =>
                    {
                        var user = log.UserId.HasValue ? new UserFunctions().GetUserById(log.UserId.Value) : null;
                        return new ConsumptionLogInfo
                        {
                            PublicId = log.PublicId,
                            UserName = user?.Name,
                            ConsumedQuantity = log.ConsumedQuantity,
                            RemainingQuantity = log.RemainingQuantity,
                            ConsumedAt = log.ConsumedAt
                        };
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to consume inventory item {inventoryItemPublicId} for user {userId}");
                throw;
            }
        }

        public async Task<List<InventoryItemInfo>> QuickAddMultipleInventoryItemsAsync(QuickAddMultipleInventoryItemsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var locationFunctions = new LocationFunctions();

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

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = new List<InventoryItemInfo>();

                foreach (var item in request.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var product = GetProductByPublicId(item.ProductPublicId);
                    if (product == null)
                    {
                        throw new ProductNotFoundException($"Product with PublicId {item.ProductPublicId} not found");
                    }

                    var inventoryItem = new ProductInventoryItem
                    {
                        ProductId = product.Id,
                        UserId = request.IsSharedWithFamily && familyId.HasValue ? null : userId.Value,
                        FamilyId = request.IsSharedWithFamily && familyId.HasValue ? familyId : null,
                        StorageLocationId = storageLocationId,
                        CurrentQuantity = item.Quantity,
                        Unit = item.Unit
                    };

                    context.ProductInventoryItems.Add(inventoryItem);
                    await context.SaveChangesAsync(cancellationToken);

                    result.Add(new InventoryItemInfo
                    {
                        PublicId = inventoryItem.PublicId,
                        CurrentQuantity = inventoryItem.CurrentQuantity,
                        Unit = inventoryItem.Unit,
                        ExpirationAt = inventoryItem.ExpirationAt,
                        PurchaseInfo = null,
                        ConsumptionLogs = new List<ConsumptionLogInfo>()
                    });
                }

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} quick-added {request.Items.Count} inventory items" +
                    (storageLocationId.HasValue ? $" to storage location {storageLocationId}" : ""));

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to quick-add multiple inventory items for user {userId}");
                throw;
            }
        }

        public async Task<List<InventoryItemInfo>> MoveInventoryItemsAsync(MoveInventoryItemsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var locationFunctions = new LocationFunctions();

            var storageLocation = locationFunctions.GetStorageLocationByPublicId(request.StorageLocationPublicId);
            if (storageLocation == null)
            {
                throw new StorageLocationNotFoundException("Storage location not found");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var result = new List<InventoryItemInfo>();

                foreach (var inventoryItemPublicId in request.InventoryItemPublicIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var inventoryItem = GetInventoryItemByPublicId(inventoryItemPublicId);
                    if (inventoryItem == null)
                    {
                        throw new ProductInventoryItemNotFoundException($"Inventory item with PublicId {inventoryItemPublicId} not found");
                    }

                    if (inventoryItem.UserId != userId.Value &&
                        (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
                    {
                        throw new UnauthorizedException($"You don't have permission to move inventory item {inventoryItemPublicId}");
                    }

                    var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                    if (trackedItem == null)
                    {
                        throw new ProductInventoryItemNotFoundException();
                    }

                    trackedItem.StorageLocationId = storageLocation.Id;

                    var purchaseInfo = GetPurchaseInfoByInventoryItemId(trackedItem.Id);
                    var consumptionLogs = GetConsumptionLogsByInventoryItemId(trackedItem.Id);

                    result.Add(new InventoryItemInfo
                    {
                        PublicId = trackedItem.PublicId,
                        CurrentQuantity = trackedItem.CurrentQuantity,
                        Unit = trackedItem.Unit,
                        ExpirationAt = trackedItem.ExpirationAt,
                        StorageLocation = trackedItem.StorageLocationId.HasValue
                            ? (locationFunctions.GetStorageLocationById(trackedItem.StorageLocationId.Value) is var storageLocationRef && storageLocationRef != null
                                ? new LocationInfo
                                {
                                    PublicId = storageLocationRef.PublicId,
                                    Name = storageLocationRef.Name
                                }
                                : null)
                            : null,
                        PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                        {
                            PublicId = purchaseInfo.PublicId,
                            PurchasedAt = purchaseInfo.PurchasedAt,
                            OriginalQuantity = purchaseInfo.OriginalQuantity,
                            Price = purchaseInfo.Price,
                            Currency = purchaseInfo.Currency,
                            ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                                ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocationRef && shoppingLocationRef != null
                                    ? new LocationInfo
                                    {
                                        PublicId = shoppingLocationRef.PublicId,
                                        Name = shoppingLocationRef.Name
                                    }
                                    : null)
                                : null
                        } : null,
                        ConsumptionLogs = consumptionLogs.Select(log =>
                        {
                            var user = log.UserId.HasValue ? new UserFunctions().GetUserById(log.UserId.Value) : null;
                            return new ConsumptionLogInfo
                            {
                                PublicId = log.PublicId,
                                UserName = user?.Name,
                                ConsumedQuantity = log.ConsumedQuantity,
                                RemainingQuantity = log.RemainingQuantity,
                                ConsumedAt = log.ConsumedAt
                            };
                        }).ToList()
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} moved {request.InventoryItemPublicIds.Count} inventory items to storage location {storageLocation.Id}");

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to move inventory items for user {userId}");
                throw;
            }
        }

        public async Task DeleteMultipleInventoryItemsAsync(DeleteMultipleInventoryItemsRequest request, CancellationToken cancellationToken = default)
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

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var itemPublicId in request.ItemPublicIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var inventoryItem = GetInventoryItemByPublicId(itemPublicId);
                    if (inventoryItem == null)
                    {
                        throw new ProductInventoryItemNotFoundException($"Inventory item not found: {itemPublicId}");
                    }

                    if (inventoryItem.UserId != userId.Value &&
                        (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
                    {
                        throw new UnauthorizedException($"You don't have permission to delete inventory item {itemPublicId}");
                    }

                    var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                    if (trackedItem == null)
                    {
                        throw new ProductInventoryItemNotFoundException();
                    }

                    trackedItem.DeleteRecord(userId.Value);
                    context.ProductInventoryItems.Update(trackedItem);

                    // Record activity for each deleted item
                    try
                    {
                        var product = GetProductById(inventoryItem.ProductId);
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ProductInventoryDelete,
                            inventoryItem.Id,
                            product?.Name ?? "Unknown",
                            inventoryItem.Unit,
                            inventoryItem.CurrentQuantity,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ProductInventoryDelete activity for inventory item {inventoryItem.PublicId}");
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted {request.ItemPublicIds.Count} inventory items");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete multiple inventory items for user {userId.Value}");
                throw;
            }
        }

        public async Task<List<InventoryItemInfo>> ConsumeMultipleInventoryItemsAsync(ConsumeMultipleInventoryItemsRequest request, CancellationToken cancellationToken = default)
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

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var results = new List<InventoryItemInfo>();

                foreach (var itemRequest in request.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var inventoryItem = GetInventoryItemByPublicId(itemRequest.InventoryItemPublicId);
                    if (inventoryItem == null)
                    {
                        throw new ProductInventoryItemNotFoundException($"Inventory item not found: {itemRequest.InventoryItemPublicId}");
                    }

                    if (inventoryItem.UserId != userId.Value &&
                        (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
                    {
                        throw new UnauthorizedException($"You don't have permission to consume inventory item {itemRequest.InventoryItemPublicId}");
                    }

                    if (inventoryItem.IsFullyConsumed)
                    {
                        throw new BadRequestException($"Inventory item {itemRequest.InventoryItemPublicId} is already fully consumed");
                    }

                    if (itemRequest.Quantity > inventoryItem.CurrentQuantity)
                    {
                        throw new BadRequestException($"Cannot consume more than available quantity ({inventoryItem.CurrentQuantity}) for item {itemRequest.InventoryItemPublicId}");
                    }

                    var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                    if (trackedItem == null)
                    {
                        throw new ProductInventoryItemNotFoundException();
                    }

                    var remainingQuantity = trackedItem.CurrentQuantity - itemRequest.Quantity;

                    var consumptionLog = new ProductConsumptionLog
                    {
                        ProductInventoryItemId = trackedItem.Id,
                        UserId = userId.Value,
                        ConsumedQuantity = itemRequest.Quantity,
                        RemainingQuantity = remainingQuantity
                    };

                    context.ProductConsumptionLogs.Add(consumptionLog);

                    trackedItem.CurrentQuantity = remainingQuantity;
                    if (remainingQuantity <= 0)
                    {
                        trackedItem.IsFullyConsumed = true;
                        trackedItem.FullyConsumedAt = DateTime.UtcNow;
                    }

                    // Record activity for each consumed item
                    try
                    {
                        var product = GetProductById(trackedItem.ProductId);
                        await new ActivityFunctions().RecordActivityAsync(
                            userId.Value,
                            familyId,
                            Enums.ActivityType.ProductInventoryDecrease,
                            trackedItem.Id,
                            product?.Name ?? "Unknown",
                            trackedItem.Unit,
                            itemRequest.Quantity,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to record ProductInventoryDecrease activity for inventory item {trackedItem.PublicId}");
                    }

                    var purchaseInfo = GetPurchaseInfoByInventoryItemId(trackedItem.Id);

                    results.Add(new InventoryItemInfo
                    {
                        PublicId = trackedItem.PublicId,
                        CurrentQuantity = trackedItem.CurrentQuantity,
                        Unit = trackedItem.Unit,
                        ExpirationAt = trackedItem.ExpirationAt,
                        StorageLocation = trackedItem.StorageLocationId.HasValue
                            ? (locationFunctions.GetStorageLocationById(trackedItem.StorageLocationId.Value) is var storageLocationRef && storageLocationRef != null
                                ? new LocationInfo
                                {
                                    PublicId = storageLocationRef.PublicId,
                                    Name = storageLocationRef.Name
                                }
                                : null)
                            : null,
                        PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                        {
                            PublicId = purchaseInfo.PublicId,
                            PurchasedAt = purchaseInfo.PurchasedAt,
                            OriginalQuantity = purchaseInfo.OriginalQuantity,
                            Price = purchaseInfo.Price,
                            Currency = purchaseInfo.Currency,
                            ShoppingLocation = purchaseInfo.ShoppingLocationId.HasValue
                                ? (locationFunctions.GetShoppingLocationById(purchaseInfo.ShoppingLocationId.Value) is var shoppingLocationRef && shoppingLocationRef != null
                                    ? new LocationInfo
                                    {
                                        PublicId = shoppingLocationRef.PublicId,
                                        Name = shoppingLocationRef.Name
                                    }
                                    : null)
                                : null
                        } : null,
                        ConsumptionLogs = new List<ConsumptionLogInfo>()
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} consumed {request.Items.Count} inventory items");

                return results;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to consume multiple inventory items for user {userId.Value}");
                throw;
            }
        }

        public async Task<SplitInventoryItemResponse> SplitInventoryItemAsync(Guid inventoryItemPublicId, SplitInventoryItemRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var inventoryItem = GetInventoryItemByPublicId(inventoryItemPublicId);

            if (inventoryItem == null)
            {
                throw new ProductInventoryItemNotFoundException($"Inventory item not found: {inventoryItemPublicId}");
            }

            // Verify ownership
            if (inventoryItem.UserId != userId.Value &&
                (!familyId.HasValue || inventoryItem.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException($"You don't have permission to split inventory item {inventoryItemPublicId}");
            }

            // Validate quantity
            if (request.Quantity <= 0)
            {
                throw new BadRequestException("Quantity must be greater than 0");
            }

            if (request.Quantity >= inventoryItem.CurrentQuantity)
            {
                throw new BadRequestException($"Split quantity must be less than current quantity ({inventoryItem.CurrentQuantity})");
            }

            // Minimum remaining quantity is 0.1
            var remainingQuantity = inventoryItem.CurrentQuantity - request.Quantity;
            if (remainingQuantity < 0.1m)
            {
                throw new BadRequestException("Remaining quantity must be at least 0.1");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Get tracked entity
                var trackedItem = await context.ProductInventoryItems.FindAsync([inventoryItem.Id], cancellationToken);
                if (trackedItem == null)
                {
                    throw new ProductInventoryItemNotFoundException();
                }

                // Create new inventory item with split quantity
                var newItem = new ProductInventoryItem
                {
                    ProductId = trackedItem.ProductId,
                    CurrentQuantity = request.Quantity,
                    Unit = trackedItem.Unit,
                    ExpirationAt = trackedItem.ExpirationAt,
                    StorageLocationId = trackedItem.StorageLocationId,
                    FamilyId = trackedItem.FamilyId,
                    UserId = trackedItem.FamilyId.HasValue ? null : trackedItem.UserId,
                    IsFullyConsumed = false
                };

                context.ProductInventoryItems.Add(newItem);
                await context.SaveChangesAsync(cancellationToken); // Save to get new item ID

                // Optionally copy purchase info (proportional)
                var purchaseInfo = GetPurchaseInfoByInventoryItemId(trackedItem.Id);
                if (purchaseInfo != null)
                {
                    var ratio = request.Quantity / trackedItem.CurrentQuantity;
                    var newPurchaseInfo = new ProductPurchaseInfo
                    {
                        ProductInventoryItemId = newItem.Id,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = request.Quantity,
                        ShoppingLocationId = purchaseInfo.ShoppingLocationId,
                        ReceiptNumber = purchaseInfo.ReceiptNumber,
                        Currency = purchaseInfo.Currency
                    };

                    // Prorate price if exists
                    if (purchaseInfo.Price.HasValue)
                    {
                        newPurchaseInfo.Price = (int)(purchaseInfo.Price.Value * ratio);
                    }

                    context.ProductPurchaseInfos.Add(newPurchaseInfo);
                }

                // Update original item quantity
                trackedItem.CurrentQuantity = remainingQuantity;

                // Create consumption log for audit trail (records the split)
                var consumptionLog = new ProductConsumptionLog
                {
                    ProductInventoryItemId = trackedItem.Id,
                    UserId = userId.Value,
                    ConsumedQuantity = request.Quantity,
                    RemainingQuantity = remainingQuantity,
                    ConsumedAt = DateTime.UtcNow
                };

                context.ProductConsumptionLogs.Add(consumptionLog);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Refresh cache for both items
                await RefreshInventoryItemCacheAsync(trackedItem.Id, cancellationToken);
                await RefreshInventoryItemCacheAsync(newItem.Id, cancellationToken);

                // Record activity
                try
                {
                    var product = GetProductById(trackedItem.ProductId);
                    await new ActivityFunctions().RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductInventoryDecrease,
                        trackedItem.Id,
                        product?.Name ?? "Unknown",
                        trackedItem.Unit,
                        request.Quantity,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductInventoryDecrease activity for split operation on {trackedItem.PublicId}");
                }

                // Load full details for response
                var locationFunctions = new LocationFunctions();

                var originalItemPurchaseInfo = GetPurchaseInfoByInventoryItemId(trackedItem.Id);
                var originalConsumptionLogs = GetConsumptionLogsByInventoryItemId(trackedItem.Id);
                var originalItem = new InventoryItemInfo
                {
                    PublicId = trackedItem.PublicId,
                    CurrentQuantity = trackedItem.CurrentQuantity,
                    Unit = trackedItem.Unit,
                    ExpirationAt = trackedItem.ExpirationAt,
                    StorageLocation = trackedItem.StorageLocationId.HasValue
                        ? (locationFunctions.GetStorageLocationById(trackedItem.StorageLocationId.Value) is var storageLocationRef && storageLocationRef != null
                            ? new LocationInfo
                            {
                                PublicId = storageLocationRef.PublicId,
                                Name = storageLocationRef.Name
                            }
                            : null)
                        : null,
                    PurchaseInfo = originalItemPurchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = originalItemPurchaseInfo.PublicId,
                        PurchasedAt = originalItemPurchaseInfo.PurchasedAt,
                        OriginalQuantity = originalItemPurchaseInfo.OriginalQuantity,
                        Price = originalItemPurchaseInfo.Price,
                        Currency = originalItemPurchaseInfo.Currency,
                        ShoppingLocation = originalItemPurchaseInfo.ShoppingLocationId.HasValue
                            ? (locationFunctions.GetShoppingLocationById(originalItemPurchaseInfo.ShoppingLocationId.Value) is var shoppingLocationRef && shoppingLocationRef != null
                                ? new LocationInfo
                                {
                                    PublicId = shoppingLocationRef.PublicId,
                                    Name = shoppingLocationRef.Name
                                }
                                : null)
                            : null
                    } : null,
                    ConsumptionLogs = originalConsumptionLogs.Select(log =>
                    {
                        var user = log.UserId.HasValue ? new UserFunctions().GetUserById(log.UserId.Value) : null;

                        return new ConsumptionLogInfo
                        {
                            PublicId = log.PublicId,
                            UserName = user?.Name,
                            ConsumedQuantity = log.ConsumedQuantity,
                            RemainingQuantity = log.RemainingQuantity,
                            ConsumedAt = log.ConsumedAt
                        };
                    }).ToList()
                };

                var newItemPurchaseInfo = GetPurchaseInfoByInventoryItemId(newItem.Id);
                var newItemInfo = new InventoryItemInfo
                {
                    PublicId = newItem.PublicId,
                    CurrentQuantity = newItem.CurrentQuantity,
                    Unit = newItem.Unit,
                    ExpirationAt = newItem.ExpirationAt,
                    StorageLocation = newItem.StorageLocationId.HasValue
                        ? (locationFunctions.GetStorageLocationById(newItem.StorageLocationId.Value) is var newStorageLocationRef && newStorageLocationRef != null
                            ? new LocationInfo
                            {
                                PublicId = newStorageLocationRef.PublicId,
                                Name = newStorageLocationRef.Name
                            }
                            : null)
                        : null,
                    PurchaseInfo = newItemPurchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = newItemPurchaseInfo.PublicId,
                        PurchasedAt = newItemPurchaseInfo.PurchasedAt,
                        OriginalQuantity = newItemPurchaseInfo.OriginalQuantity,
                        Price = newItemPurchaseInfo.Price,
                        Currency = newItemPurchaseInfo.Currency,
                        ShoppingLocation = newItemPurchaseInfo.ShoppingLocationId.HasValue
                            ? (locationFunctions.GetShoppingLocationById(newItemPurchaseInfo.ShoppingLocationId.Value) is var newShoppingLocationRef && newShoppingLocationRef != null
                                ? new LocationInfo
                                {
                                    PublicId = newShoppingLocationRef.PublicId,
                                    Name = newShoppingLocationRef.Name
                                }
                                : null)
                            : null
                    } : null,
                    ConsumptionLogs = new List<ConsumptionLogInfo>() // New item has no consumption logs yet
                };

                var response = new SplitInventoryItemResponse
                {
                    OriginalItem = originalItem,
                    NewItem = newItemInfo
                };

                Log.Information($"User {userId.Value} split inventory item {inventoryItemPublicId}: {request.Quantity} {trackedItem.Unit}");

                return response;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to split inventory item {inventoryItemPublicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task<List<ProductInfo>> CreateMultipleProductsAsync(CreateMultipleProductsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (request.Products == null || request.Products.Count == 0)
            {
                throw new BadRequestException("At least one product is required");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var results = new List<ProductInfo>();

                foreach (var productRequest in request.Products)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var product = new Product
                    {
                        Name = productRequest.Name.Trim(),
                        Brand = productRequest.Brand.Trim(),
                        Category = productRequest.Category?.Trim(),
                        Barcode = productRequest.Barcode?.Trim(),
                        IsEatable = productRequest.IsEatable
                    };

                    context.Products.Add(product);
                    await context.SaveChangesAsync(cancellationToken);

                    bool isFavorite = false;

                    if (!string.IsNullOrWhiteSpace(productRequest.Notes) || productRequest.IsFavorite)
                    {
                        var customization = new ProductCustomization
                        {
                            ProductId = product.Id,
                            UserId = userId,
                            Notes = productRequest.Notes?.Trim(),
                            IsFavorite = productRequest.IsFavorite
                        };

                        context.ProductCustomizations.Add(customization);
                        isFavorite = productRequest.IsFavorite;
                    }

                    results.Add(new ProductInfo
                    {
                        PublicId = product.PublicId,
                        Name = product.Name,
                        Brand = product.Brand,
                        Category = product.Category,
                        Barcode = product.Barcode,
                        ProductPictureBase64 = product.ProductPictureBase64,
                        IsEatable = product.IsEatable,
                        IsFavorite = isFavorite
                    });
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created {results.Count} products");

                return results;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create multiple products for user {userId.Value}");
                throw;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Converts a Product entity to ProductInfo DTO.
        /// </summary>
        public ProductInfo? ConvertToProductInfo(Product? product, int? userId = null)
        {
            if (product == null) return null;

            var customization = userId.HasValue ? GetCustomizationByProductAndUser(product.Id, userId.Value) : null;

            return new ProductInfo
            {
                PublicId = product.PublicId,
                Name = product.Name,
                Brand = product.Brand,
                Category = product.Category,
                Barcode = product.Barcode,
                ProductPictureBase64 = product.ProductPictureBase64,
                IsEatable = product.IsEatable,
                IsFavorite = customization?.IsFavorite ?? false
            };
        }
        #endregion
    }
}