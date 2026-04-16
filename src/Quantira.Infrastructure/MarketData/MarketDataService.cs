using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.MarketData;

/// <summary>
/// Application-layer implementation of <see cref="IMarketDataService"/>.
/// Coordinates between the provider factory (for external API calls),
/// the cache service (Redis), and the asset repository (for DataProviderKey lookup).
/// Acts as the single entry point for all market data reads in the application.
/// All methods follow the cache-aside pattern: check Redis first,
/// call the external provider on a miss, write the result back to Redis.
/// </summary>
public sealed class MarketDataService : IMarketDataService
{
    private readonly MarketDataProviderFactory _factory;
    private readonly IAssetRepository _assetRepository;
    private readonly ICacheService _cache;
    private readonly ILogger<MarketDataService> _logger;

    private static readonly TimeSpan PriceTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StatusTtl = TimeSpan.FromMinutes(5);

    public MarketDataService(
        MarketDataProviderFactory factory,
        IAssetRepository assetRepository,
        ICacheService cache,
        ILogger<MarketDataService> logger)
    {
        _factory = factory;
        _assetRepository = assetRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetCurrentPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestAsync(symbol, cancellationToken);
        return latest.Price;
    }

    /// <inheritdoc/>
    public async Task<PriceLatestDto> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"price:{symbol.ToUpperInvariant()}";

        var cached = await _cache.GetAsync<PriceLatestDto>(
            cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        var asset = await _assetRepository
            .GetBySymbolAsync(symbol, cancellationToken);

        if (asset is null)
        {
            _logger.LogWarning(
                "[MarketData] Asset not found for symbol {Symbol}", symbol);
            return EmptyDto(symbol);
        }

        var provider = _factory.GetProvider(asset.AssetType, asset.Exchange);
        var providerKey = asset.DataProviderKey ?? symbol;
        var result = await provider.GetLatestAsync(providerKey, cancellationToken);

        await _cache.SetAsync(cacheKey, result, PriceTtl, cancellationToken);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (symbolList.Count == 0)
            return [];

        var assets = await _assetRepository.GetBySymbolsAsync(symbolList, cancellationToken);

        if (assets.Count == 0)
            return [];

        var groupedAssets = assets
            .GroupBy(a => (a.AssetType, a.Exchange))
            .ToList();

        var providerTasks = groupedAssets
            .Select(async group =>
            {
                try
                {
                    var provider = _factory.GetProvider(group.Key.AssetType, group.Key.Exchange);
                    var providerKeys = group
                        .Select(a => a.DataProviderKey ?? a.Symbol)
                        .ToList();

                    return await provider.GetBatchLatestAsync(providerKeys, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[MarketData] Batch latest failed for provider group {AssetType}/{Exchange}.",
                        group.Key.AssetType,
                        group.Key.Exchange);

                    return (IReadOnlyList<PriceLatestDto>)[];
                }
            })
            .ToList();

        var providerResults = await Task.WhenAll(providerTasks);

        var results = providerResults
            .SelectMany(batch => batch)
            .ToList();

        var cacheTasks = results
            .Select(dto => _cache.SetAsync(
                $"price:{dto.Symbol.ToUpperInvariant()}",
                dto,
                PriceTtl,
                cancellationToken));

        await Task.WhenAll(cacheTasks);

        return results.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository
            .GetBySymbolAsync(symbol, cancellationToken);

        if (asset is null)
            return [];

        var provider = _factory.GetProvider(asset.AssetType, asset.Exchange);
        var providerKey = asset.DataProviderKey ?? symbol;

        return await provider.GetHistoryAsync(
            providerKey, interval, from, to, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"market:status:{exchange.ToUpperInvariant()}";

        var cached = await _cache.GetAsync<bool?>(cacheKey, cancellationToken);
        if (cached.HasValue) return cached.Value;

        // Use Yahoo Finance provider for market status checks.
        var provider = _factory.GetProvider(Domain.Enums.AssetType.Stock, exchange);
        var isOpen = await provider.IsMarketOpenAsync(exchange, cancellationToken);

        await _cache.SetAsync(cacheKey, isOpen, StatusTtl, cancellationToken);

        return isOpen;
    }

    private static PriceLatestDto EmptyDto(string symbol) => new(
        Symbol: symbol,
        Price: 0m,
        Change: 0m,
        ChangePct: 0m,
        DayHigh: null,
        DayLow: null,
        Volume: null,
        MarketStatus: "UNKNOWN",
        Timestamp: DateTime.UtcNow);
}