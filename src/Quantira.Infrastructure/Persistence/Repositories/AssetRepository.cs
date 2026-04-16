using Microsoft.EntityFrameworkCore;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAssetRepository"/>.
/// Symbol lookups use a case-insensitive comparison via
/// <c>EF.Functions.Like</c> to handle user-typed symbols in any case.
/// </summary>
public sealed class AssetRepository : IAssetRepository
{
    private readonly QuantiraDbContext _context;

    public AssetRepository(QuantiraDbContext context)
        => _context = context;

    public async Task<Asset?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Assets
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Asset?> GetBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        return await _context.Assets
            .FirstOrDefaultAsync(
                a => a.Symbol == normalized,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> GetByTypeAsync(
        AssetType assetType,
        CancellationToken cancellationToken = default)
    {
        return await _context.Assets
            .Where(a => a.AssetType == assetType && a.IsActive)
            .OrderBy(a => a.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Assets
            .Where(a => a.IsActive)
            .OrderBy(a => a.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> GetBySymbolsAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbols = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (normalizedSymbols.Count == 0)
            return [];

        return await _context.Assets
            .Where(a => a.IsActive && normalizedSymbols.Contains(a.Symbol))
            .OrderBy(a => a.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var normalized = symbol.Trim().ToUpperInvariant();

        return await _context.Assets
            .AnyAsync(a => a.Symbol == normalized, cancellationToken);
    }

    public async Task AddAsync(
        Asset asset,
        CancellationToken cancellationToken = default)
    {
        await _context.Assets.AddAsync(asset, cancellationToken);
    }

    public void Update(Asset asset)
    {
        _context.Assets.Update(asset);
    }
}