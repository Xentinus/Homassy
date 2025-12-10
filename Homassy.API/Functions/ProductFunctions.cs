using Homassy.API.Context;
using Homassy.API.Entities.Location;
using Homassy.API.Entities.Product;
using Homassy.API.Entities.User;
using Homassy.API.Exceptions;
using Homassy.API.Models.Product;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class ProductFunctions
    {
        private static readonly ConcurrentDictionary<int, Product> _productCache = new();
        private static readonly ConcurrentDictionary<int, ProductCustomization> _customizationCache = new();
        private static readonly ConcurrentDictionary<int, ProductInventoryItem> _inventoryItemCache = new();
        private static readonly ConcurrentDictionary<int, ProductPurchaseInfo> _purchaseInfoCache = new();
        private static readonly ConcurrentDictionary<int, List<ProductConsumptionLog>> _consumptionLogCache = new();
        public static bool Inited = false;

        #region Cache Management
        #region Cache Initialization
        public async Task InitializeCacheAsync()
        {
            var context = new HomassyDbContext();

            try
            {
                // Load all product-related data
                var products = await context.Products.ToListAsync();
                var customizations = await context.ProductCustomizations.ToListAsync();
                var inventoryItems = await context.ProductInventoryItems.ToListAsync();
                var purchaseInfos = await context.ProductPurchaseInfos.ToListAsync();
                var consumptionLogs = await context.ProductConsumptionLogs.ToListAsync();

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
        public async Task RefreshProductCacheAsync(int productId)
        {
            try
            {
                var context = new HomassyDbContext();
                var product = await context.Products.FirstOrDefaultAsync(p => p.Id == productId);
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

        public async Task RefreshProductCustomizationCacheAsync(int customizationId)
        {
            try
            {
                var context = new HomassyDbContext();
                var customization = await context.ProductCustomizations.FirstOrDefaultAsync(c => c.Id == customizationId);
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

        public async Task RefreshInventoryItemCacheAsync(int inventoryItemId)
        {
            try
            {
                var context = new HomassyDbContext();
                var item = await context.ProductInventoryItems.FirstOrDefaultAsync(i => i.Id == inventoryItemId);
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

        public async Task RefreshPurchaseInfoCacheAsync(int purchaseInfoId)
        {
            try
            {
                var context = new HomassyDbContext();
                var purchase = await context.ProductPurchaseInfos.FirstOrDefaultAsync(p => p.Id == purchaseInfoId);
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

        public async Task RefreshConsumptionLogsCacheAsync(int inventoryItemId)
        {
            try
            {
                var context = new HomassyDbContext();
                var logs = await context.ProductConsumptionLogs
                    .Where(log => log.ProductInventoryItemId == inventoryItemId)
                    .ToListAsync();

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
        public List<ProductInfo> GetAllProducts()
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

            return products.Select(p =>
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
            }).ToList();
        }

        public async Task<ProductInfo> CreateProductAsync(CreateProductRequest request)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var product = new Product
                {
                    Name = request.Name.Trim(),
                    Brand = request.Brand.Trim(),
                    Category = request.Category?.Trim(),
                    Barcode = request.Barcode?.Trim(),
                    ProductPictureBase64 = request.ProductPictureBase64,
                    IsEatable = request.IsEatable
                };

                context.Products.Add(product);
                await context.SaveChangesAsync();

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
                    await context.SaveChangesAsync();

                    isFavorite = request.IsFavorite;

                    Log.Information($"User {userId} created product {product.Id} (PublicId: {product.PublicId}) with customization");
                }
                else
                {
                    Log.Information($"User {userId} created product {product.Id} (PublicId: {product.PublicId}) without customization");
                }

                await transaction.CommitAsync();

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
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to create product for user {userId}");
                throw;
            }
        }

        public async Task<ProductInfo> UpdateProductAsync(Guid productPublicId, UpdateProductRequest request)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

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

                if (request.ProductPictureBase64 != null)
                {
                    product.ProductPictureBase64 = string.IsNullOrWhiteSpace(request.ProductPictureBase64)
                        ? null
                        : request.ProductPictureBase64;
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
                    await context.SaveChangesAsync();
                    Log.Information($"User {userId} updated product {product.Id} (PublicId: {product.PublicId})");
                }
                else
                {
                    Log.Debug($"User {userId} attempted to update product {product.Id} but no changes were made");
                }

                await transaction.CommitAsync();

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
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to update product {productPublicId} for user {userId}");
                throw;
            }
        }

        public async Task DeleteProductAsync(Guid productPublicId)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                product.DeleteRecord(userId.Value);

                context.Products.Update(product);
                await context.SaveChangesAsync();

                Log.Information($"User {userId} deleted product {product.Id} (PublicId: {product.PublicId})");
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to delete product {productPublicId} for user {userId}");
                throw;
            }
        }

        public async Task<ProductInfo> ToggleFavoriteAsync(Guid productPublicId)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

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

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

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
                await transaction.RollbackAsync();
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
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocationId = purchaseInfo.ShoppingLocationId
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

        public List<DetailedProductInfo> GetAllDetailedProductsForUser()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();

            var products = GetProductsByUserAndFamily(userId.Value, familyId);

            return products.Select(product =>
            {
                var customization = GetCustomizationByProductAndUser(product.Id, userId.Value);
                var allInventoryItems = GetInventoryItemsByProductId(product.Id, includeConsumed: false);

                var userInventoryItems = allInventoryItems
                    .Where(item => item.UserId == userId.Value ||
                                  (familyId.HasValue && item.FamilyId == familyId.Value))
                    .ToList();

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
                        PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                        {
                            PublicId = purchaseInfo.PublicId,
                            PurchasedAt = purchaseInfo.PurchasedAt,
                            OriginalQuantity = purchaseInfo.OriginalQuantity,
                            Price = purchaseInfo.Price,
                            Currency = purchaseInfo.Currency,
                            ShoppingLocationId = purchaseInfo.ShoppingLocationId
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
            }).ToList();
        }
        #endregion

        #region InventoryItem Methods
        public async Task<InventoryItemInfo> CreateInventoryItemAsync(CreateInventoryItemRequest request)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

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
                await context.SaveChangesAsync();

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
                    await context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                Log.Information($"User {userId} created inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId}) for product {product.Id}");

                return new InventoryItemInfo
                {
                    PublicId = inventoryItem.PublicId,
                    CurrentQuantity = inventoryItem.CurrentQuantity,
                    Unit = inventoryItem.Unit,
                    ExpirationAt = inventoryItem.ExpirationAt,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocationId = purchaseInfo.ShoppingLocationId
                    } : null,
                    ConsumptionLogs = new List<ConsumptionLogInfo>()
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to create inventory item for user {userId}");
                throw;
            }
        }

        public async Task<InventoryItemInfo> QuickAddInventoryItemAsync(QuickAddInventoryItemRequest request)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

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
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                Log.Information($"User {userId} quick-added inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId}) for product {product.Id}");

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
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to quick-add inventory item for user {userId}");
                throw;
            }
        }

        public async Task<InventoryItemInfo> UpdateInventoryItemAsync(Guid inventoryItemPublicId, UpdateInventoryItemRequest request)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var trackedItem = await context.ProductInventoryItems.FindAsync(inventoryItem.Id);
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
                        var trackedPurchase = await context.ProductPurchaseInfos.FindAsync(purchaseInfo.Id);
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
                    await context.SaveChangesAsync();
                    Log.Information($"User {userId} updated inventory item {trackedItem.Id} (PublicId: {trackedItem.PublicId})");
                }

                await transaction.CommitAsync();

                var consumptionLogs = GetConsumptionLogsByInventoryItemId(trackedItem.Id);

                return new InventoryItemInfo
                {
                    PublicId = trackedItem.PublicId,
                    CurrentQuantity = trackedItem.CurrentQuantity,
                    Unit = trackedItem.Unit,
                    ExpirationAt = trackedItem.ExpirationAt,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocationId = purchaseInfo.ShoppingLocationId
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
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to update inventory item {inventoryItemPublicId} for user {userId}");
                throw;
            }
        }

        public async Task DeleteInventoryItemAsync(Guid inventoryItemPublicId)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var trackedItem = await context.ProductInventoryItems.FindAsync(inventoryItem.Id);
                if (trackedItem == null)
                {
                    throw new ProductInventoryItemNotFoundException();
                }

                trackedItem.DeleteRecord(userId.Value);

                context.ProductInventoryItems.Update(trackedItem);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                Log.Information($"User {userId} deleted inventory item {inventoryItem.Id} (PublicId: {inventoryItem.PublicId})");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to delete inventory item {inventoryItemPublicId} for user {userId}");
                throw;
            }
        }

        public async Task<InventoryItemInfo> ConsumeInventoryItemAsync(Guid inventoryItemPublicId, ConsumeInventoryItemRequest request)
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
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var trackedItem = await context.ProductInventoryItems.FindAsync(inventoryItem.Id);
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

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                Log.Information($"User {userId} consumed {request.Quantity} from inventory item {trackedItem.Id} (PublicId: {trackedItem.PublicId}), remaining: {remainingQuantity}");

                var purchaseInfo = GetPurchaseInfoByInventoryItemId(trackedItem.Id);
                var consumptionLogs = GetConsumptionLogsByInventoryItemId(trackedItem.Id);

                return new InventoryItemInfo
                {
                    PublicId = trackedItem.PublicId,
                    CurrentQuantity = trackedItem.CurrentQuantity,
                    Unit = trackedItem.Unit,
                    ExpirationAt = trackedItem.ExpirationAt,
                    PurchaseInfo = purchaseInfo != null ? new PurchaseInfo
                    {
                        PublicId = purchaseInfo.PublicId,
                        PurchasedAt = purchaseInfo.PurchasedAt,
                        OriginalQuantity = purchaseInfo.OriginalQuantity,
                        Price = purchaseInfo.Price,
                        Currency = purchaseInfo.Currency,
                        ShoppingLocationId = purchaseInfo.ShoppingLocationId
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
                await transaction.RollbackAsync();
                Log.Error(ex, $"Failed to consume inventory item {inventoryItemPublicId} for user {userId}");
                throw;
            }
        }
        #endregion
    }
}