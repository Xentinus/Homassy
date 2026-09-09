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
}
