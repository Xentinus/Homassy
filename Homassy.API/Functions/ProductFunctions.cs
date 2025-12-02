using Homassy.API.Context;
using Homassy.API.Entities.Product;
using Homassy.API.Entities.User;
using Homassy.API.Models.Product;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class ProductFunctions
    {
        private static readonly ConcurrentDictionary<int, Product> _productCache = new();
        private static readonly ConcurrentDictionary<int, ProductItem> _productItemCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync()
        {
            var context = new HomassyDbContext();
            var products = await context.Products.AsNoTracking().ToListAsync();
            var productItems = await context.ProductItems.AsNoTracking().ToListAsync();

            try
            {
                foreach (var product in products)
                {
                    _productCache[product.Id] = product;
                }

                foreach (var productItem in productItems)
                {
                    _productItemCache[productItem.Id] = productItem;
                }

                Inited = true;
                Log.Information($"Initialized product cache with {products.Count} products and {productItems.Count} product items.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize product cache.");
                throw;
            }
        }

        public async Task RefreshProductCacheAsync(int productId)
        {
            try
            {
                var context = new HomassyDbContext();
                var product = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
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
                else
                {
                    Log.Debug($"Product {productId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for product {productId}.");
                throw;
            }
        }

        public async Task RefreshProductItemCacheAsync(int productItemId)
        {
            try
            {
                var context = new HomassyDbContext();
                var productItem = await context.ProductItems.AsNoTracking().FirstOrDefaultAsync(pi => pi.Id == productItemId);
                var existsInCache = _productItemCache.ContainsKey(productItemId);

                if (productItem != null && existsInCache)
                {
                    _productItemCache[productItemId] = productItem;
                    Log.Debug($"Refreshed product item {productItemId} in cache.");
                }
                else if (productItem != null && !existsInCache)
                {
                    _productItemCache[productItemId] = productItem;
                    Log.Debug($"Added product item {productItemId} to cache.");
                }
                else if (productItem == null && existsInCache)
                {
                    _productItemCache.TryRemove(productItemId, out _);
                    Log.Debug($"Removed deleted product item {productItemId} from cache.");
                }
                else
                {
                    Log.Debug($"Product item {productItemId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for product item {productItemId}.");
                throw;
            }
        }

        public Product? GetProductById(int? productId)
        {
            if (productId == null) return null;
            Product? product = null;

            if (Inited)
            {
                _productCache.TryGetValue((int)productId, out product);
            }

            if (product == null)
            {
                var context = new HomassyDbContext();
                product = context.Products.AsNoTracking().FirstOrDefault(p => p.Id == productId);
            }

            return product;
        }

        public ProductItem? GetProductItemById(int? productItemId)
        {
            if (productItemId == null) return null;
            ProductItem? productItem = null;

            if (Inited)
            {
                _productItemCache.TryGetValue((int)productItemId, out productItem);
            }

            if (productItem == null)
            {
                var context = new HomassyDbContext();
                productItem = context.ProductItems.AsNoTracking().FirstOrDefault(pi => pi.Id == productItemId);
            }

            return productItem;
        }

        public List<Product> GetProductsByIds(List<int?> productIds)
        {
            if (productIds == null || !productIds.Any()) return new List<Product>();

            var validIds = productIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<Product>();

            var result = new List<Product>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_productCache.TryGetValue(id, out var product))
                    {
                        result.Add(product);
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
                var dbProducts = context.Products
                    .AsNoTracking()
                    .Where(p => missingIds.Contains(p.Id))
                    .ToList();

                result.AddRange(dbProducts);
            }

            return result;
        }

        public List<ProductItem> GetProductItemsByIds(List<int?> productItemIds)
        {
            if (productItemIds == null || !productItemIds.Any()) return new List<ProductItem>();

            var validIds = productItemIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<ProductItem>();

            var result = new List<ProductItem>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_productItemCache.TryGetValue(id, out var productItem))
                    {
                        result.Add(productItem);
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
                var dbProductItems = context.ProductItems
                    .AsNoTracking()
                    .Where(pi => missingIds.Contains(pi.Id))
                    .ToList();

                result.AddRange(dbProductItems);
            }

            return result;
        }

        public List<ProductItem> GetProductItemsByProductId(int productId)
        {
            if (Inited)
            {
                return _productItemCache.Values
                    .Where(pi => pi.ProductId == productId)
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ProductItems
                .AsNoTracking()
                .Where(pi => pi.ProductId == productId)
                .ToList();
        }

        public List<Product> GetProductsByUserIdAndFamilyId(int userId, int? familyId)
        {
            if (Inited)
            {
                return _productCache.Values
                    .Where(p => !p.IsDeleted && (p.UserId == userId || (familyId.HasValue && p.FamilyId == familyId.Value)))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted && (p.UserId == userId || (familyId.HasValue && p.FamilyId == familyId.Value)))
                .ToList();
        }
        #endregion

        #region Product Management
        public async Task<Product> CreateProductAsync(User user, CreateProductRequest request)
        {
            var product = new Product
            {
                Name = request.Name.Trim(),
                Brand = request.Brand.Trim(),
                Category = request.Category?.Trim(),
                Barcode = request.Barcode?.Trim(),
                IsEatable = request.IsEatable,
                DefaultUnit = request.DefaultUnit,
                UserId = user.Id
            };

            // Set FamilyId only if user has a family and saveOnlyPersonal is false
            if (user.FamilyId.HasValue && !request.SaveOnlyPersonal)
            {
                product.FamilyId = user.FamilyId.Value;
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Products.Add(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return product;
        }

        public async Task UpdateProductAsync(Product product, UpdateProductRequest request, int modifiedBy)
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                product.Name = request.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Brand))
            {
                product.Brand = request.Brand.Trim();
            }

            if (request.Category != null)
            {
                product.Category = string.IsNullOrWhiteSpace(request.Category)
                    ? null
                    : request.Category.Trim();
            }

            if (request.Barcode != null)
            {
                product.Barcode = string.IsNullOrWhiteSpace(request.Barcode)
                    ? null
                    : request.Barcode.Trim();
            }

            if (request.IsEatable.HasValue)
            {
                product.IsEatable = request.IsEatable.Value;
            }

            if (request.DefaultUnit.HasValue)
            {
                product.DefaultUnit = request.DefaultUnit.Value;
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UploadProductPictureAsync(Product product, string productPictureBase64, int modifiedBy)
        {
            product.ProductPictureBase64 = productPictureBase64;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteProductPictureAsync(Product product, int modifiedBy)
        {
            product.ProductPictureBase64 = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task MakeProductPersonalAsync(Product product, int modifiedBy)
        {
            product.FamilyId = null;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AddProductToFamilyAsync(Product product, int familyId, int modifiedBy)
        {
            product.FamilyId = familyId;

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteProductAsync(Product product, int modifiedBy)
        {
            product.DeleteRekord(modifiedBy);

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Update(product);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        #endregion
    }
}
