using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Domain.Interfaces;

/// <summary>
/// Defines the persistence contract for <see cref="Trade"/> entities.
/// Trades are immutable financial records — once persisted they are never
/// updated or deleted, only soft-deleted for compliance purposes.
/// The repository exposes specialised read methods to support
/// FIFO/LIFO cost calculations and P&amp;L reporting without loading
/// entire trade histories into memory.
/// </summary>
public interface ITradeRepository
{
    /// <summary>
    /// Retrieves a single trade by its identifier.
    /// Returns <c>null</c> if not found.
    /// </summary>
    Task<Trade?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of trades for the given portfolio,
    /// optionally filtered by asset, trade type, and date range.
    /// Ordered by <c>TradedAt</c> descending by default.
    /// </summary>
    Task<IReadOnlyList<Trade>> GetByPortfolioAsync(
        Guid portfolioId,
        Guid? assetId = null,
        TradeType? tradeType = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns buy trades for a specific asset in a portfolio ordered
    /// by <c>TradedAt</c> ascending (oldest first).
    /// Used by the FIFO cost calculation engine to determine which lots
    /// are consumed first when a sell trade is recorded.
    /// </summary>
    Task<IReadOnlyList<Trade>> GetBuyLotsAsync(
        Guid portfolioId,
        Guid assetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the total realized P&amp;L for a specific asset
    /// within a portfolio directly in the database to avoid loading
    /// all trade records into memory.
    /// </summary>
    Task<decimal> GetRealizedPnLAsync(
        Guid portfolioId,
        Guid assetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new trade record to the change tracker.
    /// Trades are append-only — this is the only write method.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task AddAsync(Trade trade, CancellationToken cancellationToken = default);
}