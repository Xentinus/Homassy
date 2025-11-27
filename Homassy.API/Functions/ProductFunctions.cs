using Homassy.API.Context;
using Homassy.API.Entities;
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
        #endregion
    }
}
