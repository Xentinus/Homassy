using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Homassy.API.Infrastructure.Caching
{
    /// <summary>
    /// Hooks <see cref="CacheWriteThrough"/> onto EF's save pipeline. Two interceptors rather than
    /// one because EF's save and transaction hooks come from two base classes; the decisions all
    /// live in <see cref="CacheWriteThrough"/>.
    /// </summary>
    public sealed class CacheWriteThroughSaveInterceptor : SaveChangesInterceptor
    {
        private readonly CacheWriteThrough _writeThrough;

        public CacheWriteThroughSaveInterceptor(CacheWriteThrough writeThrough)
        {
            _writeThrough = writeThrough;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                _writeThrough.Stage(eventData.Context);
            }

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                _writeThrough.Stage(eventData.Context);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            if (eventData.Context != null)
            {
                _writeThrough.OnSaved(eventData.Context);
            }

            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                await _writeThrough.OnSavedAsync(eventData.Context, cancellationToken);
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            if (eventData.Context != null)
            {
                _writeThrough.Discard(eventData.Context);
            }

            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                _writeThrough.Discard(eventData.Context);
            }

            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }
    }
}
