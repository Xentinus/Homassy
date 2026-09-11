using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog;
using System.Runtime.CompilerServices;

namespace Homassy.API.Infrastructure.Caching
{
    /// <summary>
    /// Refreshes this instance's in-memory caches on the commit that changed a row, so a request
    /// cannot read back its own stale write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Functions layer reads through static caches (<c>_productCache</c> and friends) and only
    /// falls back to the database on a miss. Nothing on the write path used to touch them: the sole
    /// refresh trigger was <see cref="CacheManagementService"/>, driven by the database's
    /// <c>TableRecordChanges</c> trigger over LISTEN/NOTIFY with a 5-second poll behind it. That is
    /// the right mechanism for telling <em>another</em> instance, but it is asynchronous, and its
    /// processing pass is guarded by a <c>WaitAsync(0)</c> that drops any notification arriving
    /// while another pass is running - that change then waits for the next notification or the poll.
    /// </para>
    /// <para>
    /// The hazard this narrows is not simply "the cache is behind". A refresh <em>reads</em> the row
    /// and then <em>installs</em> what it read, with nothing ordering those two steps against a
    /// concurrent write: a refresh whose read landed before some later commit can install that
    /// pre-commit row afterwards, and the cache then serves a value older than the database until
    /// the next change to the same row. Refreshing on the commit itself means the committed value is
    /// written to the cache at a point where it cannot be older than what is in the database.
    /// </para>
    /// <para>
    /// It is done centrally rather than at the ~110 call sites that save, because a discipline that
    /// has to be remembered at 110 places is one that will be missed. EF's interceptors are the one
    /// place every write goes through - the in-process counterpart of the per-row database trigger.
    /// </para>
    /// <para>
    /// <b>What this does not do.</b> It narrows the window above; it does not close it. A refresh
    /// already in flight, having read before this commit, can still install its stale row after the
    /// write-through has run - closing that needs the read-and-install serialised against the write,
    /// not merely scheduled earlier. Nor does it address <c>UpdateProductAsync</c> and its like
    /// mutating the cached instance they were handed <em>before</em> the commit, which leaves the
    /// cache holding values a rollback never committed. And it is <em>not</em> established as the
    /// cause of the intermittent
    /// <c>PriceInsightTests.GetPriceHistory_MassAndCountableUnits_ReturnsTwoBasisGroupsNeverBlended</c>
    /// failure that led here: that could not be reproduced locally, with or without this change.
    /// </para>
    /// <para>
    /// <b>Why the commit and not the save.</b> The refresh re-reads the row on a fresh connection
    /// (<c>CreateForReading</c>), so running it between <c>SaveChangesAsync</c> and
    /// <c>CommitAsync</c> - which is where most of the Functions layer writes - would read the
    /// pre-transaction row and cache exactly the staleness it is meant to remove. Entries are
    /// therefore collected while saving, resolved to ids once the save has assigned them, and
    /// applied when the transaction commits. A save outside an explicit transaction has already
    /// committed by the time it reports, and is applied straight away.
    /// </para>
    /// <para>
    /// A failed refresh is logged and swallowed. The write is committed and the caller is owed its
    /// answer; the cache is a cache, and <see cref="CacheManagementService"/> is still behind this
    /// as the catch-up path.
    /// </para>
    /// </remarks>
    public sealed class CacheWriteThrough
    {
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// Per-context state. Weak keys because the owner of the context decides its lifetime: a
        /// transaction that is disposed without either a commit or a rollback leaves entries behind,
        /// and they must not pin the context.
        /// </summary>
        private readonly ConditionalWeakTable<DbContext, ContextState> _states = new();

        public CacheWriteThrough(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        private sealed class ContextState
        {
            /// <summary>Entries seen while saving. Held as entries, not ids: an inserted row has no id yet.</summary>
            public List<EntityEntry> Staged { get; } = [];

            /// <summary>Rows saved inside a transaction that has not committed yet.</summary>
            public HashSet<CacheRefreshKey> AwaitingCommit { get; } = [];
        }

        private readonly record struct CacheRefreshKey(string TableName, int RecordId);

        /// <summary>
        /// Note the entities about to be written. Called before the save, because that is the last
        /// moment their <see cref="EntityState"/> still says what is happening to them.
        /// </summary>
        public void Stage(DbContext context)
        {
            var state = _states.GetOrCreateValue(context);

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                {
                    continue;
                }

                var table = entry.Metadata.GetTableName();
                if (table == null || !EntityCacheRefresher.CachedTables.Contains(table))
                {
                    continue;
                }

                state.Staged.Add(entry);
            }
        }

        /// <summary>
        /// The save has reported. Resolve the staged entries to ids and either apply them now or
        /// hold them for the commit.
        /// </summary>
        public async Task OnSavedAsync(DbContext context, CancellationToken cancellationToken = default)
        {
            var keys = Harvest(context);
            if (keys.Count == 0)
            {
                return;
            }

            // An explicit transaction is still open, so the rows are not visible to the connection
            // the refresh reads on. Hold them until it commits.
            if (context.Database.CurrentTransaction != null)
            {
                var state = _states.GetOrCreateValue(context);
                foreach (var key in keys)
                {
                    state.AwaitingCommit.Add(key);
                }
                return;
            }

            await RefreshAsync(keys, cancellationToken);
        }

        /// <summary>Synchronous counterpart of <see cref="OnSavedAsync"/>.</summary>
        public void OnSaved(DbContext context)
        {
            OnSavedAsync(context).GetAwaiter().GetResult();
        }

        /// <summary>The transaction committed: everything it wrote is now readable, so apply it.</summary>
        public async Task OnCommittedAsync(DbContext context, CancellationToken cancellationToken = default)
        {
            if (!_states.TryGetValue(context, out var state) || state.AwaitingCommit.Count == 0)
            {
                return;
            }

            var keys = state.AwaitingCommit.ToArray();
            state.AwaitingCommit.Clear();

            await RefreshAsync(keys, cancellationToken);
        }

        /// <summary>Synchronous counterpart of <see cref="OnCommittedAsync"/>.</summary>
        public void OnCommitted(DbContext context)
        {
            OnCommittedAsync(context).GetAwaiter().GetResult();
        }

        /// <summary>
        /// The save or the transaction failed. Drop everything it had collected - refreshing a row
        /// that was never committed would only re-read the copy already in the cache, but it would
        /// also be a database round-trip per row on the error path.
        /// </summary>
        public void Discard(DbContext context)
        {
            if (!_states.TryGetValue(context, out var state))
            {
                return;
            }

            state.Staged.Clear();
            state.AwaitingCommit.Clear();
        }

        /// <summary>
        /// Turn the staged entries into <c>(table, id)</c> pairs, now that inserts have their id,
        /// and clear the staging list. Deduplicated: several entries of one row are one refresh.
        /// </summary>
        private IReadOnlyCollection<CacheRefreshKey> Harvest(DbContext context)
        {
            if (!_states.TryGetValue(context, out var state) || state.Staged.Count == 0)
            {
                return [];
            }

            var keys = new HashSet<CacheRefreshKey>();

            foreach (var entry in state.Staged)
            {
                var table = entry.Metadata.GetTableName();
                if (table == null)
                {
                    continue;
                }

                // Read the id off the entity rather than the entry: a deleted entry is detached by
                // the time the save reports, and its properties are no longer readable through it.
                if (entry.Entity is not Entities.Common.BaseEntity entity || entity.Id == 0)
                {
                    continue;
                }

                keys.Add(new CacheRefreshKey(table, entity.Id));
            }

            state.Staged.Clear();
            return keys;
        }

        private async Task RefreshAsync(IReadOnlyCollection<CacheRefreshKey> keys, CancellationToken cancellationToken)
        {
            // The Functions layer is scoped, and a write can be committed from a background worker
            // with no ambient scope to borrow.
            using var scope = _scopeFactory.CreateScope();

            foreach (var key in keys)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await EntityCacheRefresher.RefreshAsync(key.TableName, key.RecordId, scope.ServiceProvider);
                }
                catch (Exception ex)
                {
                    // Never fail a committed write over a cache refresh. CacheManagementService
                    // will pick this row up from TableRecordChanges.
                    Log.Error(ex, "Write-through cache refresh failed for {TableName} record {RecordId}", key.TableName, key.RecordId);
                }
            }
        }
    }
}
