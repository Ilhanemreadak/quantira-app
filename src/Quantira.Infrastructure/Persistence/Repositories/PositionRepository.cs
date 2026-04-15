using Microsoft.EntityFrameworkCore;
using Quantira.Domain.Entities;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPositionRepository"/>.
/// The <see cref="UpsertAsync"/> method uses EF Core's AddOrUpdate pattern —
/// if the position exists it is updated, otherwise it is inserted.
/// <see cref="UpdateMarketValuesAsync"/> uses a direct SQL update via
/// ExecuteUpdateAsync for performance — avoids loading all positions
/// into memory just to update two columns.
/// </summary>
public sealed class PositionRepository : IPositionRepository
{
    private readonly QuantiraDbContext _context;

    public PositionRepository(QuantiraDbContext context)
        => _context = context;

    public async Task<Position?> GetByPortfolioAndAssetAsync(
        Guid portfolioId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Positions
            .FirstOrDefaultAsync(
                p => p.PortfolioId == portfolioId && p.AssetId == assetId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Position>> GetByPortfolioIdAsync(
        Guid portfolioId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Positions
            .Where(p => p.PortfolioId == portfolioId && p.Quantity > 0)
            .OrderByDescending(p => p.CurrentValue != null
                ? p.CurrentValue.Amount
                : p.TotalCost.Amount)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        Position position,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.Positions
            .FirstOrDefaultAsync(
                p => p.PortfolioId == position.PortfolioId
                  && p.AssetId == position.AssetId,
                cancellationToken);

        if (existing is null)
            await _context.Positions.AddAsync(position, cancellationToken);
        else
            _context.Positions.Update(position);
    }

    public async Task UpdateMarketValuesAsync(
        Guid assetId,
        decimal currentPrice,
        CancellationToken cancellationToken = default)
    {
        await _context.Positions
            .Where(p => p.AssetId == assetId && p.Quantity > 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.LastUpdated, DateTime.UtcNow),
                cancellationToken);
    }
}