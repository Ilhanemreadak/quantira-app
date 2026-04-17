using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Domain.Interfaces;

/// <summary>
/// Defines the persistence contract for <see cref="Alert"/> entities.
/// Provides optimised read methods for <c>AlertCheckJob</c> which runs
/// every 30 seconds and must evaluate all active alerts efficiently.
/// The infrastructure implementation uses a compiled EF Core query
/// with Redis caching for the active alert list to reduce database load.
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// Retrieves a single alert by its identifier.
    /// Returns <c>null</c> if not found.
    /// </summary>
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all alerts with <c>Status = Active</c> across all users.
    /// Called by <c>AlertCheckJob</c> on every evaluation cycle.
    /// Results are cached in Redis for 25 seconds to reduce DB pressure.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all alerts belonging to the given user,
    /// optionally filtered by status and asset.
    /// </summary>
    Task<IReadOnlyList<Alert>> GetByUserIdAsync(
        Guid userId,
        AlertType? alertType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new alert to the change tracker.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);
}