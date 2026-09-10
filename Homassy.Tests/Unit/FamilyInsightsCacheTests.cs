using Homassy.API.Services;

namespace Homassy.Tests.Unit;

public class FamilyInsightsCacheTests
{
    /// <summary>Distinct reference-type payload: identity (<see cref="Assert.Same"/>) proves which factory invocation a result came from, not just its field values.</summary>
    private sealed class Widget
    {
        public int Id { get; init; }
    }

    #region TTL cache hit / expiry
    [Fact]
    public async Task GetOrAddAsync_SecondCallWithinTtl_ReturnsCachedInstanceWithoutInvokingFactoryAgain()
    {
        var cache = new FamilyInsightsCache();
        var callCount = 0;

        Task<Widget> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(new Widget());
        }

        var first = await cache.GetOrAddAsync(1, "key", TimeSpan.FromMinutes(5), Factory, CancellationToken.None);
        var second = await cache.GetOrAddAsync(1, "key", TimeSpan.FromMinutes(5), Factory, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrAddAsync_CallAfterTtlElapsed_InvokesFactoryAgain()
    {
        var cache = new FamilyInsightsCache();
        var callCount = 0;
        var ttl = TimeSpan.FromMilliseconds(1);

        Task<Widget> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(new Widget());
        }

        var first = await cache.GetOrAddAsync(1, "key", ttl, Factory, CancellationToken.None);

        // Comfortably past the 1ms TTL - the interface takes no injectable clock, so a short real
        // TTL plus a real (generous) delay is the straightforward way to observe expiry without
        // adding a clock abstraction the production API doesn't call for.
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        var second = await cache.GetOrAddAsync(1, "key", ttl, Factory, CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, callCount);
    }
    #endregion

    #region Family isolation
    /// <summary>
    /// The isolation property this whole milestone leans on: every insight endpoint trusts that
    /// the same cache key under two different family ids can never cross-contaminate. Beyond just
    /// checking the two results differ, this re-reads each family's own entry a second time to
    /// confirm writing family 2's entry did not clobber or reset family 1's - each factory must
    /// still have run exactly once after both families have been read twice.
    /// </summary>
    [Fact]
    public async Task GetOrAddAsync_SameKeyDifferentFamilyIds_YieldsIndependentEntries()
    {
        var cache = new FamilyInsightsCache();
        var familyOneCalls = 0;
        var familyTwoCalls = 0;

        Task<Widget> FamilyOneFactory(CancellationToken ct)
        {
            Interlocked.Increment(ref familyOneCalls);
            return Task.FromResult(new Widget { Id = 1 });
        }

        Task<Widget> FamilyTwoFactory(CancellationToken ct)
        {
            Interlocked.Increment(ref familyTwoCalls);
            return Task.FromResult(new Widget { Id = 2 });
        }

        var familyOneResult = await cache.GetOrAddAsync(1, "same-key", TimeSpan.FromMinutes(5), FamilyOneFactory, CancellationToken.None);
        var familyTwoResult = await cache.GetOrAddAsync(2, "same-key", TimeSpan.FromMinutes(5), FamilyTwoFactory, CancellationToken.None);

        Assert.Equal(1, familyOneResult.Id);
        Assert.Equal(2, familyTwoResult.Id);
        Assert.NotSame(familyOneResult, familyTwoResult);

        var familyOneResultAgain = await cache.GetOrAddAsync(1, "same-key", TimeSpan.FromMinutes(5), FamilyOneFactory, CancellationToken.None);
        var familyTwoResultAgain = await cache.GetOrAddAsync(2, "same-key", TimeSpan.FromMinutes(5), FamilyTwoFactory, CancellationToken.None);

        Assert.Same(familyOneResult, familyOneResultAgain);
        Assert.Same(familyTwoResult, familyTwoResultAgain);
        Assert.Equal(1, familyOneCalls);
        Assert.Equal(1, familyTwoCalls);
    }
    #endregion

    #region InvalidateFamily
    [Fact]
    public async Task InvalidateFamily_DropsAllOfThatFamilysKeys_LeavesOtherFamilyAlone()
    {
        var cache = new FamilyInsightsCache();
        var familyOneCallsA = 0;
        var familyOneCallsB = 0;
        var familyTwoCalls = 0;

        Task<Widget> FactoryA(CancellationToken ct) { Interlocked.Increment(ref familyOneCallsA); return Task.FromResult(new Widget()); }
        Task<Widget> FactoryB(CancellationToken ct) { Interlocked.Increment(ref familyOneCallsB); return Task.FromResult(new Widget()); }
        Task<Widget> OtherFamilyFactory(CancellationToken ct) { Interlocked.Increment(ref familyTwoCalls); return Task.FromResult(new Widget()); }

        await cache.GetOrAddAsync(1, "key-a", TimeSpan.FromMinutes(5), FactoryA, CancellationToken.None);
        await cache.GetOrAddAsync(1, "key-b", TimeSpan.FromMinutes(5), FactoryB, CancellationToken.None);
        await cache.GetOrAddAsync(2, "key-a", TimeSpan.FromMinutes(5), OtherFamilyFactory, CancellationToken.None);

        cache.InvalidateFamily(1);

        // Both of family 1's keys must recompute...
        await cache.GetOrAddAsync(1, "key-a", TimeSpan.FromMinutes(5), FactoryA, CancellationToken.None);
        await cache.GetOrAddAsync(1, "key-b", TimeSpan.FromMinutes(5), FactoryB, CancellationToken.None);
        Assert.Equal(2, familyOneCallsA);
        Assert.Equal(2, familyOneCallsB);

        // ...but family 2's entry, untouched by an invalidation that targeted a different family,
        // must still be served from cache.
        await cache.GetOrAddAsync(2, "key-a", TimeSpan.FromMinutes(5), OtherFamilyFactory, CancellationToken.None);
        Assert.Equal(1, familyTwoCalls);
    }
    #endregion

    #region Single-flight concurrency
    /// <summary>
    /// Twenty callers ask for the same not-yet-cached key at once. The factory is gated on a
    /// <see cref="TaskCompletionSource"/> that only this test controls, so whichever call wins the
    /// race to create the entry cannot finish until every other caller has had time to also reach
    /// <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/> and observe the same in-flight entry -
    /// a test that just called the method twice, back to back, would prove nothing about
    /// concurrent callers actually sharing one factory invocation.
    ///
    /// If this ever regressed from the atomic dictionary add-or-update + <see cref="Lazy{T}"/>
    /// pairing to a naive "check, then act" (look the key up, and if it's missing build and store
    /// an entry) the twenty callers that lose that race would each already have invoked the
    /// factory before discovering they lost, and <c>invocationCount</c> would land well above 1.
    /// </summary>
    [Fact]
    public async Task GetOrAddAsync_TwentyConcurrentCallersForSameKey_RunsFactoryExactlyOnce()
    {
        var cache = new FamilyInsightsCache();
        var invocationCount = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Widget> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref invocationCount);
            await release.Task;
            return new Widget();
        }

        var callerTasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => cache.GetOrAddAsync(1, "same-key", TimeSpan.FromMinutes(5), Factory, CancellationToken.None)))
            .ToArray();

        // Generous window for the thread pool to have actually started all twenty callers before
        // they are released together - not load-bearing for correctness (the dictionary's own
        // add-or-update is atomic regardless of timing), only for making sure this test would
        // reliably catch a regression rather than getting lucky on scheduling.
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        release.SetResult();

        var results = await Task.WhenAll(callerTasks);

        Assert.Equal(1, invocationCount);
        Assert.All(results, r => Assert.Same(results[0], r));
    }
    #endregion

    #region Failing factory
    /// <summary>
    /// A factory that throws must not poison the cache for the rest of the TTL. The exception
    /// itself is expected to propagate to the caller - nothing here swallows it - but the very
    /// next call for the same key must retry the factory rather than replay the same failure. A
    /// long TTL is used deliberately: with a short one, this test would pass even if the failure
    /// were served back "successfully" from a cache entry that simply happened to expire in time.
    /// </summary>
    [Fact]
    public async Task GetOrAddAsync_FactoryThrows_DoesNotCacheTheFailureAndRetriesNextCall()
    {
        var cache = new FamilyInsightsCache();
        var callCount = 0;
        var ttl = TimeSpan.FromMinutes(5);

        Task<Widget> Factory(CancellationToken ct)
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(new Widget());
        }

        var thrown = await Record.ExceptionAsync(() => cache.GetOrAddAsync(1, "key", ttl, Factory, CancellationToken.None));
        Assert.IsType<InvalidOperationException>(thrown);

        var second = await cache.GetOrAddAsync(1, "key", ttl, Factory, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(2, callCount);
    }
    #endregion

    #region Set
    [Fact]
    public async Task Set_ThenGetOrAddAsync_ReturnsTheSeededValueWithoutInvokingFactory()
    {
        var cache = new FamilyInsightsCache();
        var callCount = 0;
        var seeded = new Widget { Id = 42 };

        cache.Set(1, "key", TimeSpan.FromMinutes(5), seeded);

        Task<Widget> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult(new Widget());
        }

        var result = await cache.GetOrAddAsync(1, "key", TimeSpan.FromMinutes(5), Factory, CancellationToken.None);

        Assert.Same(seeded, result);
        Assert.Equal(0, callCount);
    }
    #endregion

    #region Sweep: CleanupExpiredEntries
    [Fact]
    public void CleanupExpiredEntries_ExpiredEntry_IsRemoved()
    {
        var cache = new FamilyInsightsCache();
        // A negative TTL expires the instant it is written - no real-time wait needed to get an
        // already-expired entry into the dictionary.
        cache.Set(1, "key", TimeSpan.FromMilliseconds(-1), new Widget());

        var removed = cache.CleanupExpiredEntries();

        Assert.Equal(1, removed);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task CleanupExpiredEntries_LiveEntry_IsNotRemoved()
    {
        var cache = new FamilyInsightsCache();
        var seeded = new Widget { Id = 7 };
        cache.Set(1, "key", TimeSpan.FromMinutes(5), seeded);

        var removed = cache.CleanupExpiredEntries();

        Assert.Equal(0, removed);
        Assert.Equal(1, cache.Count);

        // Confirm it is not just present but still the exact seeded instance - not evicted and
        // silently recreated.
        var callCount = 0;
        Task<Widget> Factory(CancellationToken ct) { Interlocked.Increment(ref callCount); return Task.FromResult(new Widget()); }
        var result = await cache.GetOrAddAsync(1, "key", TimeSpan.FromMinutes(5), Factory, CancellationToken.None);

        Assert.Same(seeded, result);
        Assert.Equal(0, callCount);
    }

    /// <summary>
    /// The property the brief for this fix round called out by name: a sweep racing a concurrent
    /// <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/> call that is itself replacing an expired
    /// entry must never delete the fresh replacement. The danger window is exactly the gap between
    /// the sweep reading a (now-expired) <c>CacheEntry</c> off the dictionary and the sweep's own
    /// removal call for it - if a concurrent <see cref="FamilyInsightsCache.GetOrAddAsync{T}"/>
    /// call swaps in a fresh entry inside that gap, a removal keyed on the dictionary key alone
    /// would delete whatever is there *now* (the fresh entry), whereas a removal keyed on the
    /// exact stale instance the sweep read is a safe no-op instead.
    ///
    /// <para>
    /// That gap is only a handful of CPU instructions wide, so a single attempt is very unlikely
    /// to land in it. Two dedicated, long-lived threads are synchronized with a two-phase
    /// <see cref="Barrier"/> instead of <c>Task.Run</c> (whose thread-pool queuing latency
    /// otherwise swamps a gap this small) and released together for many thousands of rounds, each
    /// forcing the exact same expired-entry-being-replaced scenario, so that across that volume of
    /// attempts natural CPU/OS scheduling jitter lands inside the gap - reliably enough to catch a
    /// regression, per the same design principle
    /// <c>GetOrAddAsync_TwentyConcurrentCallersForSameKey_RunsFactoryExactlyOnce</c> above uses.
    /// Correctness never depends on timing either way: with the compare-and-remove
    /// <see cref="FamilyInsightsCache.CleanupExpiredEntries"/> actually uses, the assertion below
    /// holds for every possible interleaving, not just the ones this test happens to produce - so
    /// this cannot flake against a correct implementation, no matter the round count.
    /// </para>
    ///
    /// <para>
    /// Confirmed to fail against a naive key-only removal (<c>_entries.TryRemove(pair.Key, out
    /// _)</c> in place of <c>_entries.TryRemove(pair)</c>) before this test was finalized - see
    /// the Task 6 fix-round report for the exact failure observed.
    /// </para>
    /// </summary>
    [Fact]
    public void CleanupExpiredEntries_ConcurrentWithGetOrAddAsyncReplacingAnExpiredEntry_NeverRemovesTheFreshReplacement()
    {
        var cache = new FamilyInsightsCache();
        const string key = "race-key";
        const int rounds = 20_000;
        var barrier = new Barrier(2);
        string? failure = null;

        Task<Widget> FreshFactory(CancellationToken ct) => Task.FromResult(new Widget { Id = 1 });

        // Both threads must call SignalAndWait exactly the same number of times - a Barrier
        // deadlocks the moment one side stops showing up. An early-exit-on-failure check here
        // would race the *other* thread's own loop-condition check right across the phase-2
        // release (nothing keeps that other thread from having already committed to one more
        // round by the time the first thread observes the failure), so both threads unconditionally
        // run every round and `failure` is only inspected once, after both have finished.
        var installerThread = new Thread(() =>
        {
            for (var round = 0; round < rounds; round++)
            {
                // Force the entry into "just expired" before the two threads are released
                // together, so the race below is purely about whether the sweeper's read of that
                // stale value and this replacement land on either side of the sweeper's own
                // removal call.
                cache.Set(1, key, TimeSpan.FromMilliseconds(-1), new Widget { Id = 0 });

                barrier.SignalAndWait(); // phase 1: released together into the race
                cache.GetOrAddAsync(1, key, TimeSpan.FromMinutes(5), FreshFactory, CancellationToken.None)
                    .GetAwaiter().GetResult();
                barrier.SignalAndWait(); // phase 2: this round's replacement is now fully installed
            }
        });

        var sweeperThread = new Thread(() =>
        {
            for (var round = 0; round < rounds; round++)
            {
                barrier.SignalAndWait(); // phase 1
                cache.CleanupExpiredEntries();
                barrier.SignalAndWait(); // phase 2: the installer's replacement (if any) has landed

                // Past phase 2, the installer's GetOrAddAsync call for this round has unconditionally
                // completed (Barrier guarantees both sides' pre-phase work happened-before this
                // point), so the entry must be present - a correct sweep either never touched it
                // (already fresh by the time it looked) or removed only the stale instance it
                // actually read, never the replacement installed after that read.
                if (cache.Count == 0 && failure is null)
                {
                    failure = $"Round {round}: entry for '{key}' was missing right after the installer's " +
                        "replacement completed - the sweep removed a fresh entry a concurrent GetOrAddAsync had just installed.";
                }
            }
        });

        installerThread.Start();
        sweeperThread.Start();
        installerThread.Join();
        sweeperThread.Join();

        Assert.Null(failure);
    }
    #endregion
}
