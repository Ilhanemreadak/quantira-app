namespace Quantira.Domain.Interfaces;

/// <summary>
/// Abstracts the unit of work pattern over the underlying data store.
/// Ensures that all repository operations within a single command handler
/// are committed atomically — either all succeed or none are persisted.
/// Implemented by <c>QuantiraDbContext</c> in the infrastructure layer.
/// Command handlers that perform multiple write operations must call
/// <see cref="SaveChangesAsync"/> exactly once at the end of their execution,
/// after which any pending domain events are dispatched by the interceptor.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Persists all pending changes tracked by the current unit of work
    /// to the underlying data store within a single transaction.
    /// Dispatches domain events after a successful save.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}