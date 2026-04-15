using Quantira.Domain.Entities;

namespace Quantira.Domain.Interfaces;

/// <summary>
/// Defines the persistence contract for <see cref="Position"/> entities.
/// Positions are child entities of the <see cref="Portfolio"/> aggregate
/// but are queried independently in several high-frequency read paths
/// (dashboard valuation, alert evaluation, P&amp;L reporting).
/// The infrastructure implementation uses MERGE statements via Dapper
/// for upsert operations to minimise round-trips during trade processing.
/// </summary>
public interface IPositionRepository
{
    /// <summary>
    /// Retrieves the position for a specific asset within a portfolio.
    /// Returns <c>null</c> if no position exists for that asset.
    /// </summary>
    Task<Position?> GetByPortfolioAndAssetAsync(
        Guid portfolioId,
        Guid assetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all current positions for the given portfolio,
    /// including associated asset metadata.
    /// Ordered by current value descending so the largest positions appear first.
    /// </summary>
    Task<IReadOnlyList<Position>> GetByPortfolioIdAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new position or updates the existing one for the given
    /// portfolio and asset combination using a single MERGE operation.
    /// Called by <c>TradeAddedEvent</c> handler after every trade.
    /// </summary>
    Task UpsertAsync(Position position, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-updates the <c>CurrentValue</c> and <c>UnrealizedPnL</c> fields
    /// for all positions that hold the given asset.
    /// Called by <c>AssetPriceUpdatedEvent</c> handler during each price refresh cycle.
    /// </summary>
    Task UpdateMarketValuesAsync(
        Guid assetId,
        decimal currentPrice,
        CancellationToken cancellationToken = default);
}