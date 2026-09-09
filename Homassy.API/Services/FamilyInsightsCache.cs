using System.Collections.Concurrent;

namespace Homassy.API.Services;

// PROCESS-LOCAL BY DESIGN, exactly like R4's ShoppingListPresence connection registry
// (Homassy.API/Hubs/ShoppingListPresence.cs). Every entry lives only in this instance's memory:
// a multi-instance deployment would have each instance computing - and holding - its own copy of
// the same family's aggregates, and an InvalidateFamily call on one instance would leave every
// other instance still serving its own stale copy for up to the rest of that entry's TTL. Making
// this cache coherent across replicas needs the SignalR backplane that issue #68 adds, so
// instances can tell each other "family X changed, drop your copy" - that is out of scope here.
// Do not paper over this with Redis or any other external store: the app runs single-instance
// today, so this is a complete and correct picture of the cache as it stands, not an oversight.
/// <summary>
/// Per-family, per-key TTL cache backing the R5 "Insight" aggregation endpoints. Every insight
/// endpoint reads <c>SessionInfo.GetFamilyId()</c> and folds it into the key it asks
/// <see cref="GetOrAddAsync{T}"/> for, which is what keeps one family's aggregates from ever
/// being handed to another: the same <c>key</c> string under two different family ids is always
/// two independent entries, never a shared bucket.
///
/// <para>
/// Registered as a singleton (see <c>Program.cs</c>, next to <see cref="StatisticsService"/>), so
/// every method here runs against arbitrary concurrent requests and must be safe for that.
/// </para>
/// </summary>
public sealed class FamilyInsightsCache
{
    /// <summary>
    /// One cached value: when it expires, and the task that produces it. The task is wrapped in
    /// <see cref="Lazy{T}"/> rather than started eagerly, so that
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/>'s atomic add-or-update can hand the exact
    /// same entry back to every caller racing to create it - only the caller whose entry actually
    /// wins the dictionary write ever has its factory delegate run, because every other racing
    /// caller's own (unwon) <see cref="Lazy{T}"/> is discarded before its <c>Value</c> is ever
    /// touched, and <see cref="Lazy{T}"/> itself guarantees the factory runs exactly once even
    /// when several threads do call <c>Value</c> on the one that won.
    /// </summary>
    private sealed class CacheEntry
    {
        public CacheEntry(DateTime expiresAtUtc, Lazy<Task<object>> lazyTask)
        {
            ExpiresAtUtc = expiresAtUtc;
            LazyTask = lazyTask;
        }

        public DateTime ExpiresAtUtc { get; }
        public Lazy<Task<object>> LazyTask { get; }
    }

    /// <summary>
    /// Keyed by (family, key) so isolation is structural rather than a check someone can forget
    /// to make: two different family ids can never share a bucket, even for the identical
    /// <c>key</c> string, because they are different dictionary keys entirely.
    /// </summary>
    private readonly ConcurrentDictionary<(int FamilyId, string Key), CacheEntry> _entries = new();

    /// <summary>
    /// Returns the cached value for (<paramref name="familyId"/>, <paramref name="key"/>) if one
    /// exists and has not yet expired; otherwise runs <paramref name="factory"/>, caches the
    /// result for <paramref name="ttl"/>, and returns it.
    ///
    /// <para>
    /// <b>Single-flight.</b> When N callers race for the same missing (or expired) key, exactly
    /// one factory invocation happens. Every caller's attempt to install an entry goes through one
    /// atomic dictionary add-or-update: whichever caller's entry the dictionary actually keeps is
    /// the entry every other racing caller is handed back too - a losing caller's own candidate
    /// entry is simply discarded, unused, before its <see cref="Lazy{T}"/> is ever evaluated. All
    /// callers then await the *same* <see cref="Lazy{T}"/>, which guarantees its factory delegate
    /// runs exactly once no matter how many threads read it concurrently. No lock is held across
    /// the factory call - the atomicity comes from the dictionary's own add-or-update plus
    /// <see cref="Lazy{T}"/>, not from a <see langword="lock"/> in this method.
    /// </para>
    ///
    /// <para>
    /// <b>A throwing factory is never cached.</b> If <paramref name="factory"/> faults (or is
    /// cancelled), the entry it produced is removed from the dictionary before the exception is
    /// rethrown to every caller awaiting it, so the *next* call for the same key gets a fresh
    /// attempt instead of the same failure replayed for the rest of the TTL. The removal is a
    /// compare-and-remove against the exact failed entry (by reference), so it can never evict a
    /// fresher entry a concurrent caller has since installed - e.g. after another thread already
    /// performed this same cleanup, or after a subsequent <see cref="Set{T}"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">
    /// The cached reference type. Constrained to <see langword="class"/> so the internal
    /// <see cref="object"/>-typed storage is always a plain reference cast on the way out, never a
    /// boxing allocation.
    /// </typeparam>
    public async Task<T> GetOrAddAsync<T>(int familyId, string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var cacheKey = (FamilyId: familyId, Key: key);
        var now = DateTime.UtcNow;

        var entry = _entries.AddOrUpdate(
            cacheKey,
            addValueFactory: _ => CreateEntry(now, ttl, factory, ct),
            updateValueFactory: (_, existing) => now < existing.ExpiresAtUtc ? existing : CreateEntry(now, ttl, factory, ct));

        try
        {
            return (T)await entry.LazyTask.Value.ConfigureAwait(false);
        }
        catch
        {
            // Drop exactly this failed entry - not "whatever is there now" - so a concurrent
            // caller that has already installed a fresher one (another thread's own retry, or a
            // Set()) is never clobbered by this cleanup.
            _entries.TryRemove(new KeyValuePair<(int FamilyId, string Key), CacheEntry>(cacheKey, entry));
            throw;
        }
    }

    /// <summary>
    /// Drops every cached entry belonging to <paramref name="familyId"/>, regardless of key.
    /// Entries belonging to every other family are left untouched.
    /// </summary>
    public void InvalidateFamily(int familyId)
    {
        foreach (var pair in _entries)
        {
            if (pair.Key.FamilyId == familyId)
            {
                _entries.TryRemove(pair);
            }
        }
    }

    /// <summary>
    /// Unconditionally installs <paramref name="value"/> as the cached entry for
    /// (<paramref name="familyId"/>, <paramref name="key"/>), replacing whatever was there -
    /// including a still-running factory from a concurrent <see cref="GetOrAddAsync{T}"/> call,
    /// whose own callers are unaffected and still receive its result once it completes. A
    /// subsequent <see cref="GetOrAddAsync{T}"/> for the same key sees this value as a normal
    /// cache hit until <paramref name="ttl"/> elapses.
    /// </summary>
    public void Set<T>(int familyId, string key, TimeSpan ttl, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var cacheKey = (FamilyId: familyId, Key: key);
        _entries[cacheKey] = new CacheEntry(DateTime.UtcNow.Add(ttl), new Lazy<Task<object>>(() => Task.FromResult((object)value)));
    }

    private static CacheEntry CreateEntry<T>(DateTime now, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct) where T : class
    {
        return new CacheEntry(now.Add(ttl), new Lazy<Task<object>>(() => InvokeFactoryAsync(factory, ct)));
    }

    /// <summary>
    /// Adapts the caller's typed factory into the <see cref="object"/>-returning shape the shared
    /// dictionary stores. Being an <see langword="async"/> method, it always returns a
    /// <see cref="Task"/> - even when <paramref name="factory"/> itself throws synchronously -
    /// rather than letting that exception escape into the <see cref="Lazy{T}"/> construction this
    /// method's caller runs inside of. That distinction matters: <see cref="Lazy{T}"/>'s default
    /// thread-safety mode caches a synchronous valueFactory exception and replays it forever,
    /// which is exactly the "poisoned entry" this class must not produce. Funnelling every outcome
    /// through a <see cref="Task"/> instead means the only place a failure is ever remembered is
    /// the task itself, which <see cref="GetOrAddAsync{T}"/> explicitly detects and evicts.
    /// </summary>
    private static async Task<object> InvokeFactoryAsync<T>(Func<CancellationToken, Task<T>> factory, CancellationToken ct) where T : class
    {
        return await factory(ct).ConfigureAwait(false);
    }
}
