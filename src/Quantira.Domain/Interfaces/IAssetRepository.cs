using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Domain.Interfaces;

/// <summary>
/// Defines the persistence contract for the <see cref="Asset"/> aggregate.
/// Assets are shared reference data — they are not owned by any user or portfolio.
/// Reads are heavily cached in Redis by the infrastructure implementation
/// because asset metadata changes infrequently.
/// </summary>
public interface IAssetRepository
{
    /// <summary>
    /// Retrieves an asset by its unique identifier.
    /// Returns <c>null</c> if not found or inactive.
    /// </summary>
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an asset by its ticker symbol (case-insensitive).
    /// Returns <c>null</c> if the symbol is not tracked in Quantira.
    /// Used during trade entry to resolve user-typed symbols to asset records.
    /// </summary>
    Task<Asset?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active assets of the given type.
    /// Used by <c>MarketDataRefreshJob</c> to build the batch price request.
    /// </summary>
    Task<IReadOnlyList<Asset>> GetByTypeAsync(AssetType assetType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all currently active assets across all types.
    /// Used for bulk operations such as the nightly price history archival job.
    /// </summary>
    Task<IReadOnlyList<Asset>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if an asset with the given symbol already exists.
    /// Used to prevent duplicate asset records during seeding or manual creation.
    /// </summary>
    Task<bool> ExistsBySymbolAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new asset to the change tracker.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task AddAsync(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the given asset as modified in the change tracker.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    void Update(Asset asset);
}