using Homassy.API.Context;
using Homassy.API.Entities.Location;
using Homassy.API.Exceptions;
using Homassy.API.Extensions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Location;
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
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            var context = new HomassyDbContext();
            var shoppingLocations = await context.ShoppingLocations.ToListAsync(cancellationToken);
            var storageLocations = await context.StorageLocations.ToListAsync(cancellationToken);

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

        public async Task RefreshShoppingLocationCacheAsync(int shoppingLocationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var shoppingLocation = await context.ShoppingLocations.FirstOrDefaultAsync(sl => sl.Id == shoppingLocationId, cancellationToken);
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

        public async Task RefreshStorageLocationCacheAsync(int storageLocationId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = new HomassyDbContext();
                var storageLocation = await context.StorageLocations.FirstOrDefaultAsync(sl => sl.Id == storageLocationId, cancellationToken);
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
        #endregion

        #region Cache Getters - By Internal Id
        public ShoppingLocation? GetShoppingLocationById(int? shoppingLocationId)
        {
            if (shoppingLocationId == null) return null;

            if (Inited && _shoppingLocationCache.TryGetValue((int)shoppingLocationId, out var shoppingLocation))
            {
                return shoppingLocation;
            }

            var context = new HomassyDbContext();
            return context.ShoppingLocations.FirstOrDefault(sl => sl.Id == shoppingLocationId);
        }

        public StorageLocation? GetStorageLocationById(int? storageLocationId)
        {
            if (storageLocationId == null) return null;

            if (Inited && _storageLocationCache.TryGetValue((int)storageLocationId, out var storageLocation))
            {
                return storageLocation;
            }

            var context = new HomassyDbContext();
            return context.StorageLocations.FirstOrDefault(sl => sl.Id == storageLocationId);
        }
        #endregion

        #region Cache Getters - By PublicId
        public ShoppingLocation? GetShoppingLocationByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var shoppingLocation = _shoppingLocationCache.Values.FirstOrDefault(sl => sl.PublicId == publicId);
                if (shoppingLocation != null) return shoppingLocation;
            }

            var context = new HomassyDbContext();
            return context.ShoppingLocations.FirstOrDefault(sl => sl.PublicId == publicId);
        }

        public StorageLocation? GetStorageLocationByPublicId(Guid publicId)
        {
            if (Inited)
            {
                var storageLocation = _storageLocationCache.Values.FirstOrDefault(sl => sl.PublicId == publicId);
                if (storageLocation != null) return storageLocation;
            }

            var context = new HomassyDbContext();
            return context.StorageLocations.FirstOrDefault(sl => sl.PublicId == publicId);
        }
        #endregion

        #region Cache Getters - Multiple Items
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
                    .Where(sl => missingIds.Contains(sl.Id))
                    .ToList();

                result.AddRange(dbStorageLocations);
            }

            return result;
        }

        public List<ShoppingLocation> GetShoppingLocationsByUserAndFamily(int userId, int? familyId)
        {
            if (Inited)
            {
                return _shoppingLocationCache.Values
                    .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.ShoppingLocations
                .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
                .ToList();
        }

        public List<StorageLocation> GetStorageLocationsByUserAndFamily(int userId, int? familyId)
        {
            if (Inited)
            {
                return _storageLocationCache.Values
                    .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
                    .ToList();
            }

            var context = new HomassyDbContext();
            return context.StorageLocations
                .Where(sl => sl.UserId == userId || (familyId.HasValue && sl.FamilyId == familyId))
                .ToList();
        }
        #endregion

        #region Location Methods
        public List<ShoppingLocationInfo> GetAllShoppingLocations()
        {
            return GetAllShoppingLocations(new PaginationRequest { ReturnAll = true }).Items;
        }

        public PagedResult<ShoppingLocationInfo> GetAllShoppingLocations(PaginationRequest pagination)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var shoppingLocations = GetShoppingLocationsByUserAndFamily(userId.Value, familyId);

            var shoppingLocationInfos = shoppingLocations.Select(s => new ShoppingLocationInfo
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Description = s.Description,
                Address = s.Address,
                City = s.City,
                PostalCode = s.PostalCode,
                Country = s.Country,
                Website = s.Website,
                GoogleMaps = s.GoogleMaps,
                Color = s.Color,
                IsSharedWithFamily = s.FamilyId.HasValue
            })
            .OrderBy(s => s.Name);

            return shoppingLocationInfos.ToPagedResult(pagination);
        }

        public List<StorageLocationInfo> GetAllStorageLocations()
        {
            return GetAllStorageLocations(new PaginationRequest { ReturnAll = true }).Items;
        }

        public PagedResult<StorageLocationInfo> GetAllStorageLocations(PaginationRequest pagination)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            var storageLocations = GetStorageLocationsByUserAndFamily(userId.Value, familyId);

            var storageLocationInfos = storageLocations.Select(s => new StorageLocationInfo
            {
                PublicId = s.PublicId,
                Name = s.Name,
                Description = s.Description,
                Color = s.Color,
                IsSharedWithFamily = s.FamilyId.HasValue
            })
            .OrderBy(s => s.Name);

            return storageLocationInfos.ToPagedResult(pagination);
        }

        public async Task<ShoppingLocationInfo> CreateShoppingLocationAsync(ShoppingLocationRequest request, CancellationToken cancellationToken = default)
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
                var shoppingLocation = new ShoppingLocation
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    Color = request.Color?.Trim(),
                    Address = request.Address?.Trim(),
                    City = request.City?.Trim(),
                    PostalCode = request.PostalCode?.Trim(),
                    Country = request.Country?.Trim(),
                    Website = request.Website?.Trim(),
                    GoogleMaps = request.GoogleMaps?.Trim(),
                    UserId = userId.Value,
                    FamilyId = request.IsSharedWithFamily == true && familyId.HasValue ? familyId : null
                };

                context.ShoppingLocations.Add(shoppingLocation);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created shopping location {shoppingLocation.Id} (PublicId: {shoppingLocation.PublicId}), shared with family: {request.IsSharedWithFamily}");

                return new ShoppingLocationInfo
                {
                    PublicId = shoppingLocation.PublicId,
                    Name = shoppingLocation.Name,
                    Description = shoppingLocation.Description,
                    Color = shoppingLocation.Color,
                    Address = shoppingLocation.Address,
                    City = shoppingLocation.City,
                    PostalCode = shoppingLocation.PostalCode,
                    Country = shoppingLocation.Country,
                    Website = shoppingLocation.Website,
                    GoogleMaps = shoppingLocation.GoogleMaps,
                    IsSharedWithFamily = shoppingLocation.FamilyId.HasValue
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create shopping location for user {userId.Value}");
                throw;
            }
        }

        public async Task<ShoppingLocationInfo> UpdateShoppingLocationAsync(Guid shoppingLocationPublicId, ShoppingLocationRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingLocation = GetShoppingLocationByPublicId(shoppingLocationPublicId);
            if (shoppingLocation == null)
            {
                throw new ShoppingLocationNotFoundException("Shopping location not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingLocation.UserId != userId.Value &&
                (!familyId.HasValue || shoppingLocation.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to update this shopping location");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedLocation = await context.ShoppingLocations.FindAsync([shoppingLocation.Id], cancellationToken);
                if (trackedLocation == null)
                {
                    throw new ShoppingLocationNotFoundException("Shopping location not found");
                }

                bool hasChanges = false;

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    trackedLocation.Name = request.Name.Trim();
                    hasChanges = true;
                }

                if (request.Description != null)
                {
                    trackedLocation.Description = string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim();
                    hasChanges = true;
                }

                if (request.Color != null)
                {
                    trackedLocation.Color = string.IsNullOrWhiteSpace(request.Color)
                        ? null
                        : request.Color.Trim();
                    hasChanges = true;
                }

                if (request.Address != null)
                {
                    trackedLocation.Address = string.IsNullOrWhiteSpace(request.Address)
                        ? null
                        : request.Address.Trim();
                    hasChanges = true;
                }

                if (request.City != null)
                {
                    trackedLocation.City = string.IsNullOrWhiteSpace(request.City)
                        ? null
                        : request.City.Trim();
                    hasChanges = true;
                }

                if (request.PostalCode != null)
                {
                    trackedLocation.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode)
                        ? null
                        : request.PostalCode.Trim();
                    hasChanges = true;
                }

                if (request.Country != null)
                {
                    trackedLocation.Country = string.IsNullOrWhiteSpace(request.Country)
                        ? null
                        : request.Country.Trim();
                    hasChanges = true;
                }

                if (request.Website != null)
                {
                    trackedLocation.Website = string.IsNullOrWhiteSpace(request.Website)
                        ? null
                        : request.Website.Trim();
                    hasChanges = true;
                }

                if (request.GoogleMaps != null)
                {
                    trackedLocation.GoogleMaps = request.GoogleMaps;
                    hasChanges = true;
                }

                if (request.IsSharedWithFamily.HasValue)
                {
                    trackedLocation.FamilyId = request.IsSharedWithFamily.Value && familyId.HasValue
                        ? familyId
                        : null;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information($"User {userId.Value} updated shopping location {trackedLocation.Id} (PublicId: {trackedLocation.PublicId})");
                }
                else
                {
                    Log.Debug($"User {userId.Value} attempted to update shopping location {trackedLocation.Id} but no changes were made");
                }

                await transaction.CommitAsync(cancellationToken);

                return new ShoppingLocationInfo
                {
                    PublicId = trackedLocation.PublicId,
                    Name = trackedLocation.Name,
                    Description = trackedLocation.Description,
                    Color = trackedLocation.Color,
                    Address = trackedLocation.Address,
                    City = trackedLocation.City,
                    PostalCode = trackedLocation.PostalCode,
                    Country = trackedLocation.Country,
                    Website = trackedLocation.Website,
                    GoogleMaps = trackedLocation.GoogleMaps,
                    IsSharedWithFamily = trackedLocation.FamilyId.HasValue
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to update shopping location {shoppingLocationPublicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task<StorageLocationInfo> CreateStorageLocationAsync(StorageLocationRequest request, CancellationToken cancellationToken = default)
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
                var storageLocation = new StorageLocation
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    Color = request.Color?.Trim(),
                    IsFreezer = request.IsFreezer ?? false,
                    UserId = userId.Value,
                    FamilyId = request.IsSharedWithFamily == true && familyId.HasValue ? familyId : null
                };

                context.StorageLocations.Add(storageLocation);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created storage location {storageLocation.Id} (PublicId: {storageLocation.PublicId}), shared with family: {request.IsSharedWithFamily}");

                return new StorageLocationInfo
                {
                    PublicId = storageLocation.PublicId,
                    Name = storageLocation.Name,
                    Description = storageLocation.Description,
                    Color = storageLocation.Color,
                    IsFreezer = storageLocation.IsFreezer,
                    IsSharedWithFamily = storageLocation.FamilyId.HasValue
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create storage location for user {userId.Value}");
                throw;
            }
        }

        public async Task<StorageLocationInfo> UpdateStorageLocationAsync(Guid storageLocationPublicId, StorageLocationRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var storageLocation = GetStorageLocationByPublicId(storageLocationPublicId);
            if (storageLocation == null)
            {
                throw new StorageLocationNotFoundException("Storage location not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            if (storageLocation.UserId != userId.Value &&
                (!familyId.HasValue || storageLocation.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to update this storage location");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedLocation = await context.StorageLocations.FindAsync([storageLocation.Id], cancellationToken);
                if (trackedLocation == null)
                {
                    throw new StorageLocationNotFoundException("Storage location not found");
                }

                bool hasChanges = false;

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    trackedLocation.Name = request.Name.Trim();
                    hasChanges = true;
                }

                if (request.Description != null)
                {
                    trackedLocation.Description = string.IsNullOrWhiteSpace(request.Description)
                        ? null
                        : request.Description.Trim();
                    hasChanges = true;
                }

                if (request.Color != null)
                {
                    trackedLocation.Color = string.IsNullOrWhiteSpace(request.Color)
                        ? null
                        : request.Color.Trim();
                    hasChanges = true;
                }

                if (request.IsFreezer.HasValue)
                {
                    trackedLocation.IsFreezer = request.IsFreezer.Value;
                    hasChanges = true;
                }

                if (request.IsSharedWithFamily.HasValue)
                {
                    trackedLocation.FamilyId = request.IsSharedWithFamily.Value && familyId.HasValue
                        ? familyId
                        : null;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    Log.Information($"User {userId.Value} updated storage location {trackedLocation.Id} (PublicId: {trackedLocation.PublicId})");
                }
                else
                {
                    Log.Debug($"User {userId.Value} attempted to update storage location {trackedLocation.Id} but no changes were made");
                }

                await transaction.CommitAsync(cancellationToken);

                return new StorageLocationInfo
                {
                    PublicId = trackedLocation.PublicId,
                    Name = trackedLocation.Name,
                    Description = trackedLocation.Description,
                    Color = trackedLocation.Color,
                    IsFreezer = trackedLocation.IsFreezer,
                    IsSharedWithFamily = trackedLocation.FamilyId.HasValue
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to update storage location {storageLocationPublicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteShoppingLocationAsync(Guid shoppingLocationPublicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var shoppingLocation = GetShoppingLocationByPublicId(shoppingLocationPublicId);
            if (shoppingLocation == null)
            {
                throw new ShoppingLocationNotFoundException("Shopping location not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            if (shoppingLocation.UserId != userId.Value &&
                (!familyId.HasValue || shoppingLocation.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to delete this shopping location");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedLocation = await context.ShoppingLocations.FindAsync([shoppingLocation.Id], cancellationToken);
                if (trackedLocation == null)
                {
                    throw new ShoppingLocationNotFoundException("Shopping location not found");
                }

                trackedLocation.DeleteRecord(userId.Value);

                context.ShoppingLocations.Update(trackedLocation);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted shopping location {shoppingLocation.Id} (PublicId: {shoppingLocation.PublicId})");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete shopping location {shoppingLocationPublicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteStorageLocationAsync(Guid storageLocationPublicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var storageLocation = GetStorageLocationByPublicId(storageLocationPublicId);
            if (storageLocation == null)
            {
                throw new StorageLocationNotFoundException("Storage location not found");
            }

            var familyId = SessionInfo.GetFamilyId();
            if (storageLocation.UserId != userId.Value &&
                (!familyId.HasValue || storageLocation.FamilyId != familyId.Value))
            {
                throw new UnauthorizedException("You don't have permission to delete this storage location");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedLocation = await context.StorageLocations.FindAsync([storageLocation.Id], cancellationToken);
                if (trackedLocation == null)
                {
                    throw new StorageLocationNotFoundException("Storage location not found");
                }

                trackedLocation.DeleteRecord(userId.Value);

                context.StorageLocations.Update(trackedLocation);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted storage location {storageLocation.Id} (PublicId: {storageLocation.PublicId})");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete storage location {storageLocationPublicId} for user {userId.Value}");
                throw;
            }
        }

        public async Task<List<StorageLocationInfo>> CreateMultipleStorageLocationsAsync(List<StorageLocationRequest> requests, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (requests == null || requests.Count == 0)
            {
                throw new BadRequestException("At least one storage location is required");
            }

            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    throw new BadRequestException("Name is required for all storage locations");
                }
            }

            var familyId = SessionInfo.GetFamilyId();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var storageLocations = new List<StorageLocation>();

                foreach (var request in requests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var storageLocation = new StorageLocation
                    {
                        Name = request.Name!.Trim(),
                        Description = request.Description?.Trim(),
                        Color = request.Color?.Trim(),
                        IsFreezer = request.IsFreezer ?? false,
                        UserId = userId.Value,
                        FamilyId = request.IsSharedWithFamily == true && familyId.HasValue ? familyId : null
                    };

                    storageLocations.Add(storageLocation);
                }

                context.StorageLocations.AddRange(storageLocations);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created {storageLocations.Count} storage locations");

                return storageLocations.Select(sl => new StorageLocationInfo
                {
                    PublicId = sl.PublicId,
                    Name = sl.Name,
                    Description = sl.Description,
                    Color = sl.Color,
                    IsFreezer = sl.IsFreezer,
                    IsSharedWithFamily = sl.FamilyId.HasValue
                }).ToList();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create multiple storage locations for user {userId.Value}");
                throw;
            }
        }

        public async Task<List<ShoppingLocationInfo>> CreateMultipleShoppingLocationsAsync(List<ShoppingLocationRequest> requests, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (requests == null || requests.Count == 0)
            {
                throw new BadRequestException("At least one shopping location is required");
            }

            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    throw new BadRequestException("Name is required for all shopping locations");
                }
            }

            var familyId = SessionInfo.GetFamilyId();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var shoppingLocations = new List<ShoppingLocation>();

                foreach (var request in requests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var shoppingLocation = new ShoppingLocation
                    {
                        Name = request.Name!.Trim(),
                        Description = request.Description?.Trim(),
                        Color = request.Color?.Trim(),
                        Address = request.Address?.Trim(),
                        City = request.City?.Trim(),
                        PostalCode = request.PostalCode?.Trim(),
                        Country = request.Country?.Trim(),
                        Website = request.Website?.Trim(),
                        GoogleMaps = request.GoogleMaps?.Trim(),
                        UserId = userId.Value,
                        FamilyId = request.IsSharedWithFamily == true && familyId.HasValue ? familyId : null
                    };

                    shoppingLocations.Add(shoppingLocation);
                }

                context.ShoppingLocations.AddRange(shoppingLocations);
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created {shoppingLocations.Count} shopping locations");

                return shoppingLocations.Select(sl => new ShoppingLocationInfo
                {
                    PublicId = sl.PublicId,
                    Name = sl.Name,
                    Description = sl.Description,
                    Color = sl.Color,
                    Address = sl.Address,
                    City = sl.City,
                    PostalCode = sl.PostalCode,
                    Country = sl.Country,
                    Website = sl.Website,
                    GoogleMaps = sl.GoogleMaps,
                    IsSharedWithFamily = sl.FamilyId.HasValue
                }).ToList();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to create multiple shopping locations for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteMultipleStorageLocationsAsync(DeleteMultipleStorageLocationsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (request.LocationPublicIds == null || request.LocationPublicIds.Count == 0)
            {
                throw new BadRequestException("At least one location is required");
            }

            var familyId = SessionInfo.GetFamilyId();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var locationPublicId in request.LocationPublicIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var storageLocation = GetStorageLocationByPublicId(locationPublicId);
                    if (storageLocation == null)
                    {
                        throw new StorageLocationNotFoundException($"Storage location not found: {locationPublicId}");
                    }

                    if (storageLocation.UserId != userId.Value &&
                        (!familyId.HasValue || storageLocation.FamilyId != familyId.Value))
                    {
                        throw new UnauthorizedException($"You don't have permission to delete storage location {locationPublicId}");
                    }

                    var trackedLocation = await context.StorageLocations.FindAsync([storageLocation.Id], cancellationToken);
                    if (trackedLocation == null)
                    {
                        throw new StorageLocationNotFoundException("Storage location not found");
                    }

                    trackedLocation.DeleteRecord(userId.Value);
                    context.StorageLocations.Update(trackedLocation);
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted {request.LocationPublicIds.Count} storage locations");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete multiple storage locations for user {userId.Value}");
                throw;
            }
        }

        public async Task DeleteMultipleShoppingLocationsAsync(DeleteMultipleShoppingLocationsRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            if (request.LocationPublicIds == null || request.LocationPublicIds.Count == 0)
            {
                throw new BadRequestException("At least one location is required");
            }

            var familyId = SessionInfo.GetFamilyId();

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var locationPublicId in request.LocationPublicIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var shoppingLocation = GetShoppingLocationByPublicId(locationPublicId);
                    if (shoppingLocation == null)
                    {
                        throw new ShoppingLocationNotFoundException($"Shopping location not found: {locationPublicId}");
                    }

                    if (shoppingLocation.UserId != userId.Value &&
                        (!familyId.HasValue || shoppingLocation.FamilyId != familyId.Value))
                    {
                        throw new UnauthorizedException($"You don't have permission to delete shopping location {locationPublicId}");
                    }

                    var trackedLocation = await context.ShoppingLocations.FindAsync([shoppingLocation.Id], cancellationToken);
                    if (trackedLocation == null)
                    {
                        throw new ShoppingLocationNotFoundException("Shopping location not found");
                    }

                    trackedLocation.DeleteRecord(userId.Value);
                    context.ShoppingLocations.Update(trackedLocation);
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} deleted {request.LocationPublicIds.Count} shopping locations");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete multiple shopping locations for user {userId.Value}");
                throw;
            }
        }
        #endregion
    }
}