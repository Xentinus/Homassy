using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Homassy.API.Infrastructure.Caching
{
    /// <summary>
    /// The commit half of <see cref="CacheWriteThrough"/>: most of the Functions layer writes inside
    /// an explicit transaction, and the commit is the first moment another connection can read what
    /// was written.
    /// </summary>
    public sealed class CacheWriteThroughTransactionInterceptor : DbTransactionInterceptor
    {
        private readonly CacheWriteThrough _writeThrough;

        public CacheWriteThroughTransactionInterceptor(CacheWriteThrough writeThrough)
        {
            _writeThrough = writeThrough;
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            base.TransactionCommitted(transaction, eventData);

            if (eventData.Context != null)
            {
                _writeThrough.OnCommitted(eventData.Context);
            }
        }

        public override async Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);

            if (eventData.Context != null)
            {
                await _writeThrough.OnCommittedAsync(eventData.Context, cancellationToken);
            }
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        {
            Discard(eventData);
            base.TransactionRolledBack(transaction, eventData);
        }

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Discard(eventData);
            return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
        }

        public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        {
            Discard(eventData);
            base.TransactionFailed(transaction, eventData);
        }

        public override Task TransactionFailedAsync(
            DbTransaction transaction,
            TransactionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Discard(eventData);
            return base.TransactionFailedAsync(transaction, eventData, cancellationToken);
        }

        private void Discard(DbContextEventData eventData)
        {
            if (eventData.Context != null)
            {
                _writeThrough.Discard(eventData.Context);
            }
        }
    }
}
