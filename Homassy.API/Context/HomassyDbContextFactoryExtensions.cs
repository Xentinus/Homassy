using Microsoft.EntityFrameworkCore;

namespace Homassy.API.Context;

/// <summary>
/// The read half of the two-rule context lifetime the Functions layer follows: a context is
/// created per operation from <see cref="IDbContextFactory{TContext}"/> and disposed with it.
/// </summary>
public static class HomassyDbContextFactoryExtensions
{
    /// <summary>
    /// A context for an operation that only reads.
    /// </summary>
    /// <remarks>
    /// Queries on it do not populate the change tracker, so materialised rows are not kept
    /// alive by an identity map they will never be saved through. That matters most on the
    /// list endpoints and the bulk cache loads, which read whole tables and then map
    /// everything to DTOs — tracking roughly doubles the allocation per row for nothing.
    ///
    /// Identity resolution is kept, so a row that appears more than once in a query (through
    /// an <c>Include</c>, say) still materialises as one object, exactly as a tracking query
    /// would return it. Saving through this context throws rather than silently writing
    /// nothing.
    /// </remarks>
    public static HomassyDbContext CreateForReading(this IDbContextFactory<HomassyDbContext> factory)
    {
        var context = factory.CreateDbContext();
        context.MarkReadOnly();
        return context;
    }
}
