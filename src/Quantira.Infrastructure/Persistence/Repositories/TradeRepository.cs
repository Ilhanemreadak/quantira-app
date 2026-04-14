using Microsoft.EntityFrameworkCore;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ITradeRepository"/>.
/// All filter parameters are optional and composed dynamically
/// to build a single efficient query without multiple round-trips.
/// </summary>
public sealed class TradeRepository : ITradeRepository
{
    private readonly QuantiraDbContext _context;

    public TradeRepository(QuantiraDbContext context)
        => _context = context;

    public async Task<Trade?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Trades
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Trade>> GetByPortfolioAsync(
        Guid portfolioId,
        Guid? assetId = null,
        TradeType? tradeType = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Trades
            .Where(t => t.PortfolioId == portfolioId);

        if (assetId.HasValue)
            query = query.Where(t => t.AssetId == assetId.Value);

        if (tradeType.HasValue)
            query = query.Where(t => t.TradeType == tradeType.Value);

        if (from.HasValue)
            query = query.Where(t => t.TradedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.TradedAt <= to.Value);

        return await query
            .OrderByDescending(t => t.TradedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trade>> GetBuyLotsAsync(
        Guid portfolioId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Trades
            .Where(t => t.PortfolioId == portfolioId
                     && t.AssetId == assetId
                     && (t.TradeType == TradeType.Buy
                      || t.TradeType == TradeType.TransferIn))
            .OrderBy(t => t.TradedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetRealizedPnLAsync(
        Guid portfolioId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var sells = await _context.Trades
            .Where(t => t.PortfolioId == portfolioId
                     && t.AssetId == assetId
                     && (t.TradeType == TradeType.Sell
                      || t.TradeType == TradeType.TransferOut))
            .ToListAsync(cancellationToken);

        return sells.Sum(t => t.NetValue.Amount);
    }

    public async Task AddAsync(
        Trade trade,
        CancellationToken cancellationToken = default)
    {
        await _context.Trades.AddAsync(trade, cancellationToken);
    }
}