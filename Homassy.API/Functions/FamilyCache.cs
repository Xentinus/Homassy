using Homassy.Data.Context;
using Homassy.Data.Entities.Family;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;

namespace Homassy.API.Functions
{
    /// <summary>
    /// The process-wide family cache and the reads that go through it.
    /// </summary>
    /// <remarks>
    /// Lifted out of <see cref="FamilyFunctions"/> to break the one constructor cycle in this
    /// layer (#133). <c>FamilyFunctions</c> needs <c>UserFunctions</c> and <c>UserFunctions</c>
    /// needed a family by id, so neither could take the other through its constructor and both
    /// reached for <c>new</c> instead. What <c>UserFunctions</c> actually wanted was this cache,
    /// not the family write path - so this is the third class the shared operation belongs in,
    /// rather than a <c>Lazy&lt;T&gt;</c> holding the cycle together.
    /// <para>
    /// Registered scoped, like the rest of the layer, but the dictionary is static: it is the
    /// process's cache, and its lifetime is the process. <c>CacheManagementService</c> fills it on
    /// startup and <c>EntityCacheRefresher</c> keeps it current - both through this class.
    /// </para>
    /// </remarks>
    public class FamilyCache
    {
        private static readonly ConcurrentDictionary<int, Family> _familyCache = new();

        /// <summary>Whether the cache has been loaded. A miss before this falls through to the database.</summary>
        public static bool Inited = false;

        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;

        public FamilyCache(IDbContextFactory<HomassyDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public async Task InitializeCacheAsync(CancellationToken cancellationToken = default)
        {
            using var context = _contextFactory.CreateForReading();
            var families = await context.Families
                .ToListAsync(cancellationToken);

            try
            {
                foreach (var family in families)
                {
                    _familyCache[family.Id] = family;
                }
                Inited = true;
                Log.Information($"Initialized family cache with {families.Count} families.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize family cache.");
                throw;
            }
        }

        public async Task RefreshCacheAsync(int familyId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = _contextFactory.CreateForReading();
                var family = await context.Families
                    .FirstOrDefaultAsync(f => f.Id == familyId, cancellationToken);

                var existsInCache = _familyCache.ContainsKey(familyId);

                if (family != null && existsInCache)
                {
                    _familyCache[familyId] = family;
                    Log.Debug($"Refreshed family {familyId} in cache.");
                }
                else if (family != null && !existsInCache)
                {
                    _familyCache[familyId] = family;
                    Log.Debug($"Added family {familyId} to cache.");
                }
                else if (family == null && existsInCache)
                {
                    _familyCache.TryRemove(familyId, out _);
                    Log.Debug($"Removed deleted family {familyId} from cache.");
                }
                else
                {
                    Log.Debug($"Family {familyId} not found in DB or cache.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to refresh cache for family {familyId}.");
                throw;
            }
        }

        public Family? GetFamilyById(int? familyId)
        {
            if (familyId == null) return null;
            Family? family = null;

            if (Inited)
            {
                _familyCache.TryGetValue((int)familyId, out family);
            }

            if (family == null)
            {
                using var context = _contextFactory.CreateForReading();
                family = context.Families.FirstOrDefault(f => f.Id == familyId);
            }

            return family;
        }

        public Family? GetFamilyByShareCode(string? shareCode)
        {
            if (string.IsNullOrWhiteSpace(shareCode)) return null;
            Family? family = null;

            if (Inited)
            {
                family = _familyCache.Values.FirstOrDefault(f => f.ShareCode == shareCode);
            }

            if (family == null)
            {
                using var context = _contextFactory.CreateForReading();
                family = context.Families
                    .FirstOrDefault(f => f.ShareCode == shareCode);
            }

            return family;
        }

        public List<Family> GetFamiliesByIds(List<int?> familyIds)
        {
            if (familyIds == null || !familyIds.Any()) return new List<Family>();

            var validIds = familyIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            if (!validIds.Any()) return new List<Family>();

            var result = new List<Family>();
            var missingIds = new List<int>();

            if (Inited)
            {
                foreach (var id in validIds)
                {
                    if (_familyCache.TryGetValue(id, out var family))
                    {
                        result.Add(family);
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
                using var context = _contextFactory.CreateForReading();
                var dbFamilies = context.Families
                    .Where(f => missingIds.Contains(f.Id))
                    .ToList();

                result.AddRange(dbFamilies);
            }

            return result;
        }
    }
}
