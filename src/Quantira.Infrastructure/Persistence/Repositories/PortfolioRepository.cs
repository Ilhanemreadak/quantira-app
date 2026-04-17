using Microsoft.EntityFrameworkCore;
using Quantira.Domain.Entities;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPortfolioRepository"/>.
/// All queries respect the global soft-delete query filter defined in
/// <see cref="QuantiraDbContext"/> — deleted portfolios are never returned.
/// Navigation properties (Positions, Trades) are loaded explicitly
/// only when the caller requests the full aggregate via
/// <see cref="GetWithPositionsAsync"/> to avoid unnecessary data transfer
/// on lightweight list queries.
/// </summary>
public sealed class PortfolioRepository : IPortfolioRepository
{
    private readonly QuantiraDbContext _context;

    public PortfolioRepository(QuantiraDbContext context)
        => _context = context;

    public async Task<Portfolio?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Portfolios
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Portfolio?> GetWithPositionsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Portfolios
            .Include(p => p.Positions)
            .Include(p => p.Trades)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Portfolios
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await _context.Portfolios
            .AnyAsync(p => p.UserId == userId
                        && p.Name == name,
                cancellationToken);
    }

    public async Task AddAsync(
        Portfolio portfolio,
        CancellationToken cancellationToken = default)
    {
        await _context.Portfolios.AddAsync(portfolio, cancellationToken);
    }
}