using Homassy.API.Context;
using Homassy.API.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    public class LocationFunctions
    {
        private static readonly ConcurrentDictionary<int, ShoppingLocation> _shoppingLocationCache = new();
        private static readonly ConcurrentDictionary<int, StorageLocation> _storageLocationCache = new();
        public static bool Inited = false;

        #region Cache Management
        public async Task InitializeCacheAsync()
        {
            var context = new HomassyDbContext();
            var shoppingLocations = await context.ShoppingLocations.AsNoTracking().ToListAsync();
            var storageLocations = await context.StorageLocations.AsNoTracking().ToListAsync();

            try
            {
                foreach (var shoppingLocation in shoppingLocations)
                {
                    _shoppingLocationCache[shoppingLocation.Id] = shoppingLocation;
                }

                foreach (var storageLocation in storageLocations)
                {
                    _storageLocationCache[storageLocation.Id] = storageLocation;
                }

                Inited = true;
                Log.Information($"Initialized location cache with {shoppingLocations.Count} shopping locations and {storageLocations.Count} storage locations.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize location cache.");
                throw;
            }
        }

        public async Task RefreshShoppingLocationCacheAsync(int shoppingLocationId)
        {
            try
            {
                var context = new HomassyDbContext();
                var shoppingLocation = await context.ShoppingLocations.AsNoTracking().FirstOrDefaultAsync(sl => sl.Id == shoppingLocationId);
                var existsInCache = _shoppingLocationCache.ContainsKey(shoppingLocationId);

                if (shoppingLocation != null && existsInCache)
                {
                    _shoppingLocationCache[shoppingLocationId] = shoppingLocation;
                    Log.Debug($"Refreshed shopping location {shoppingLocationId} in cache.");
                }
                else if (shoppingLocation != null && !existsInCache)
                {
                    _shoppingLocationCache[shoppingLocationId] = shoppingLocation;
                    Log.Debug($"Added shopping location {shoppingLocationId} to cache.");
                }
                else if (shoppingLocation == null && existsInCache)
                {
                    _shoppingLocationCache.TryRemove(shoppingLocationId, out _);
                    Log.Debug($"Removed deleted shopping location {shoppingLocationId} from cache.");
                }
                else
                {
                    Log.Debug($"Shopping location {shoppingLocationId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for shopping location {shoppingLocationId}.");
                throw;
            }
        }

        public async Task RefreshStorageLocationCacheAsync(int storageLocationId)
        {
            try
            {
                var context = new HomassyDbContext();
                var storageLocation = await context.StorageLocations.AsNoTracking().FirstOrDefaultAsync(sl => sl.Id == storageLocationId);
                var existsInCache = _storageLocationCache.ContainsKey(storageLocationId);

                if (storageLocation != null && existsInCache)
                {
                    _storageLocationCache[storageLocationId] = storageLocation;
                    Log.Debug($"Refreshed storage location {storageLocationId} in cache.");
                }
                else if (storageLocation != null && !existsInCache)
                {
                    _storageLocationCache[storageLocationId] = storageLocation;
                    Log.Debug($"Added storage location {storageLocationId} to cache.");
                }
                else if (storageLocation == null && existsInCache)
                {
                    _storageLocationCache.TryRemove(storageLocationId, out _);
                    Log.Debug($"Removed deleted storage location {storageLocationId} from cache.");
                }
                else
                {
                    Log.Debug($"Storage location {storageLocationId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for storage location {storageLocationId}.");
                throw;
            }
        }

        public ShoppingLocation? GetShoppingLocationById(int? shoppingLocationId)
        {
            if (shoppingLocationId == null) return null;
            ShoppingLocation? shoppingLocation = null;

            if (Inited)
            {
                _shoppingLocationCache.TryGetValue((int)shoppingLocationId, out shoppingLocation);
            }

            if (shoppingLocation == null)
            {
                var context = new HomassyDbContext();
                shoppingLocation = context.ShoppingLocations.AsNoTracking().FirstOrDefault(sl => sl.Id == shoppingLocationId);
            }

            return shoppingLocation;
        }

        public StorageLocation? GetStorageLocationById(int? storageLocationId)
        {
            if (storageLocationId == null) return null;
            StorageLocation? storageLocation = null;

            if (Inited)
            {
                _storageLocationCache.TryGetValue((int)storageLocationId, out storageLocation);
            }

            if (storageLocation == null)
            {
                var context = new HomassyDbContext();
                storageLocation = context.StorageLocations.AsNoTracking().FirstOrDefault(sl => sl.Id == storageLocationId);
            }

            return storageLocation;
        }

        public List<ShoppingLocation> GetShoppingLocationsByIds(List<int?> shoppingLocationIds)
        {
            if (shoppingLocationIds == null || !shoppingLocationIds.Any()) return new List<ShoppingLocation>();

            var validIds = shoppingLocationIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<ShoppingLocation>();

            var result = new List<ShoppingLocation>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_shoppingLocationCache.TryGetValue(id, out var shoppingLocation))
                    {
                        result.Add(shoppingLocation);
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
                var dbShoppingLocations = context.ShoppingLocations
                    .AsNoTracking()
                    .Where(sl => missingIds.Contains(sl.Id))
                    .ToList();

                result.AddRange(dbShoppingLocations);
            }

            return result;
        }

        public List<StorageLocation> GetStorageLocationsByIds(List<int?> storageLocationIds)
        {
            if (storageLocationIds == null || !storageLocationIds.Any()) return new List<StorageLocation>();

            var validIds = storageLocationIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<StorageLocation>();

            var result = new List<StorageLocation>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_storageLocationCache.TryGetValue(id, out var storageLocation))
                    {
                        result.Add(storageLocation);
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
                var dbStorageLocations = context.StorageLocations
                    .AsNoTracking()
                    .Where(sl => missingIds.Contains(sl.Id))
                    .ToList();

                result.AddRange(dbStorageLocations);
            }

            return result;
        }

        public List<ShoppingLocation> GetShoppingLocationsByFamilyId(int familyId)
        {
            if (Inited)
            {
                return _shoppingLocationCache.Values
                    .Where(sl => sl.FamilyId == familyId)
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingLocations
                .AsNoTracking()
                .Where(sl => sl.FamilyId == familyId)
                .ToList();
        }

        public List<StorageLocation> GetStorageLocationsByFamilyId(int familyId)
        {
            if (Inited)
            {
                return _storageLocationCache.Values
                    .Where(sl => sl.FamilyId == familyId)
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.StorageLocations
                .AsNoTracking()
                .Where(sl => sl.FamilyId == familyId)
                .ToList();
        }
        #endregion
    }
}
