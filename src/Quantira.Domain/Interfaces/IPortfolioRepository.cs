using Quantira.Domain.Entities;

namespace Quantira.Domain.Interfaces;

/// <summary>
/// Defines the persistence contract for the <see cref="Portfolio"/> aggregate.
/// All methods operate on the full aggregate including its child
/// <c>Position</c> collection unless otherwise stated.
/// Implemented by <c>PortfolioRepository</c> in the infrastructure layer
/// using EF Core with global query filters applied for soft-delete and
/// row-level security.
/// </summary>
public interface IPortfolioRepository
{
    /// <summary>
    /// Retrieves a portfolio by its unique identifier.
    /// Returns <c>null</c> if not found or soft-deleted.
    /// Does not load child positions — use
    /// <see cref="GetWithPositionsAsync"/> when position data is needed.
    /// </summary>
    Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a portfolio together with its current positions and
    /// the associated asset metadata for each position.
    /// Used by summary and valuation queries that need full position detail.
    /// </summary>
    Task<Portfolio?> GetWithPositionsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active portfolios belonging to the given user,
    /// ordered by <c>IsDefault</c> descending then by <c>CreatedAt</c> ascending.
    /// </summary>
    Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if the user already has a portfolio with the given name.
    /// Used by <c>CreatePortfolioCommandValidator</c> to enforce unique names per user.
    /// </summary>
    Task<bool> ExistsAsync(Guid userId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new portfolio aggregate to the change tracker.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task AddAsync(Portfolio portfolio, CancellationToken cancellationToken = default);
}