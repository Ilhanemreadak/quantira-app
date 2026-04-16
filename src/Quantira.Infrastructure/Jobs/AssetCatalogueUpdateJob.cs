using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Quantira.Domain.Entities;
using Quantira.Infrastructure.Assets;
using Quantira.Infrastructure.Persistence;

namespace Quantira.Infrastructure.Jobs;

public sealed class AssetCatalogueUpdateJob
{
    private const int SymbolLookupChunkSize = 1000;
    private const int InsertChunkSize = 500;

    private readonly QuantiraDbContext _context;
    private readonly IEnumerable<IAssetProvider> _providers;
    private readonly ILogger<AssetCatalogueUpdateJob> _logger;

    public AssetCatalogueUpdateJob(
        QuantiraDbContext context,
        IEnumerable<IAssetProvider> providers,
        ILogger<AssetCatalogueUpdateJob> logger)
    {
        _context = context;
        _providers = providers;
        _logger = logger;
    }

    public async Task RunAllUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var providers = _providers.ToList();

        _logger.LogInformation(
            "[AssetCatalogueUpdateJob] Catalogue update cycle started at {StartTimeUtc}. Provider count: {ProviderCount}.",
            DateTime.UtcNow,
            providers.Count);

        foreach (var provider in providers)
        {
            var providerName = provider.GetType().Name;

            _logger.LogInformation(
                "[AssetCatalogueUpdateJob] Provider {Provider} started. Supported asset type: {AssetType}.",
                providerName,
                provider.SupportedType);

            try
            {
                var fetchedAssets = await provider.FetchAssetsAsync(cancellationToken);

                _logger.LogInformation(
                    "[AssetCatalogueUpdateJob] Provider {Provider} fetched {FetchedCount} assets.",
                    providerName,
                    fetchedAssets.Count);

                if (fetchedAssets.Count == 0)
                {
                    _logger.LogInformation(
                        "[AssetCatalogueUpdateJob] Provider {Provider} returned no assets. Skipping.",
                        providerName);
                    continue;
                }

                var fetchedBySymbol = fetchedAssets
                    .Where(asset => !string.IsNullOrWhiteSpace(asset.Symbol))
                    .GroupBy(
                        asset => asset.Symbol.Trim().ToUpperInvariant(),
                        StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.Ordinal);

                if (fetchedBySymbol.Count == 0)
                {
                    _logger.LogInformation(
                        "[AssetCatalogueUpdateJob] Provider {Provider} has no valid symbols after normalization. Skipping.",
                        providerName);
                    continue;
                }

                var existingSymbolSet = new HashSet<string>(StringComparer.Ordinal);

                foreach (var symbolChunk in fetchedBySymbol.Keys.Chunk(SymbolLookupChunkSize))
                {
                    var chunkList = symbolChunk.ToList();

                    var existingSymbols = await _context.Assets
                        .AsNoTracking()
                        .Where(asset =>
                            asset.AssetType == provider.SupportedType
                            && asset.IsActive
                            && chunkList.Contains(asset.Symbol))
                        .Select(asset => asset.Symbol)
                        .ToListAsync(cancellationToken);

                    foreach (var symbol in existingSymbols)
                        existingSymbolSet.Add(symbol);
                }

                var newAssets = fetchedBySymbol
                    .Where(pair => !existingSymbolSet.Contains(pair.Key))
                    .Select(pair => pair.Value)
                    .ToList();

                _logger.LogInformation(
                    "[AssetCatalogueUpdateJob] Provider {Provider} diff completed. FetchedDistinct: {FetchedDistinct}, ExistingMatched: {ExistingMatched}, New: {NewCount}.",
                    providerName,
                    fetchedBySymbol.Count,
                    existingSymbolSet.Count,
                    newAssets.Count);

                if (newAssets.Count == 0)
                {
                    _logger.LogInformation(
                        "[AssetCatalogueUpdateJob] Provider {Provider} has no new assets to insert.",
                        providerName);
                    continue;
                }

                await using IDbContextTransaction transaction =
                    await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    foreach (var insertChunk in newAssets.Chunk(InsertChunkSize))
                    {
                        var batch = insertChunk.ToList();

                        await _context.Assets.AddRangeAsync(batch, cancellationToken);
                        await _context.SaveChangesAsync(cancellationToken);
                        _context.ChangeTracker.Clear();
                    }

                    await transaction.CommitAsync(cancellationToken);

                    var insertedSymbols = string.Join(", ", newAssets.Select(asset => asset.Symbol));

                    _logger.LogInformation(
                        "[AssetCatalogueUpdateJob] Provider {Provider} inserted {InsertCount} new assets. Symbols: {Symbols}.",
                        providerName,
                        newAssets.Count,
                        insertedSymbols);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    _logger.LogError(
                        ex,
                        "[AssetCatalogueUpdateJob] Provider {Provider} failed while committing transaction. Continuing with next provider.",
                        providerName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[AssetCatalogueUpdateJob] Provider {Provider} failed during fetch/diff step. Continuing with next provider.",
                    providerName);
            }
        }

        _logger.LogInformation(
            "[AssetCatalogueUpdateJob] Catalogue update cycle completed in {Elapsed}.",
            sw.Elapsed);
    }
}
