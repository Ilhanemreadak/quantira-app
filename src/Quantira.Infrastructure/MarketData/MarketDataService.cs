using System.Collections.Concurrent;
using System.Net;
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
    private const int Provider429FailureThreshold = 3;
    private static readonly TimeSpan ProviderCooldown = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, ProviderCircuitState> ProviderCircuitStates =
        new(StringComparer.OrdinalIgnoreCase);

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

        var providerBuckets = new Dictionary<string, ProviderBatchBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            try
            {
                var provider = _factory.GetProvider(asset.AssetType, asset.Exchange);
                var providerName = provider.ProviderName;

                if (!providerBuckets.TryGetValue(providerName, out var bucket))
                {
                    bucket = new ProviderBatchBucket(provider);
                    providerBuckets[providerName] = bucket;
                }

                bucket.ProviderKeys.Add(asset.DataProviderKey ?? asset.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[MarketData] Provider resolution failed for symbol {Symbol}, assetType {AssetType}, exchange {Exchange}.",
                    asset.Symbol,
                    asset.AssetType,
                    asset.Exchange);
            }
        }

        if (providerBuckets.Count == 0)
            return [];

        var providerTasks = providerBuckets
            .Select(async pair =>
            {
                var providerName = pair.Key;
                var bucket = pair.Value;

                if (TryGetProviderCooldownRemaining(providerName, out var remaining))
                {
                    _logger.LogWarning(
                        "[MarketData] Provider {ProviderName} is in cooldown for {RemainingSeconds} seconds. Skipping request.",
                        providerName,
                        Math.Ceiling(remaining.TotalSeconds));

                    return (IReadOnlyList<PriceLatestDto>)[];
                }

                try
                {
                    var results = await bucket.Provider.GetBatchLatestAsync(
                        bucket.ProviderKeys,
                        cancellationToken);

                    ResetProvider429Failures(providerName);

                    return results;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var outcome = RegisterProvider429Failure(providerName);

                    if (outcome.CircuitOpened)
                    {
                        _logger.LogWarning(
                            "[MarketData] Provider {ProviderName} returned 429 {FailureThreshold} times consecutively. " +
                            "Circuit opened until {CooldownUntilUtc:O}.",
                            providerName,
                            Provider429FailureThreshold,
                            outcome.CooldownUntilUtc);
                    }
                    else
                    {
                        _logger.LogWarning(
                            ex,
                            "[MarketData] Provider {ProviderName} returned 429. Consecutive 429 count: {ConsecutiveCount}/{FailureThreshold}.",
                            providerName,
                            outcome.Consecutive429Count,
                            Provider429FailureThreshold);
                    }

                    return (IReadOnlyList<PriceLatestDto>)[];
                }
                catch (Exception ex)
                {
                    ResetProvider429Failures(providerName);

                    _logger.LogError(
                        ex,
                        "[MarketData] Batch latest failed for provider {ProviderName}.",
                        providerName);

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
        {
            _logger.LogWarning(
                "[MarketData] History asset not found for symbol {Symbol}",
                symbol);

            return [];
        }

        var provider = _factory.GetProvider(asset.AssetType, asset.Exchange);
        var providerKey = asset.DataProviderKey ?? symbol;

        _logger.LogInformation(
            "[MarketData] Fetching history symbol={Symbol} provider={ProviderName} providerKey={ProviderKey} interval={Interval} from={FromUtc:O} to={ToUtc:O}",
            symbol,
            provider.ProviderName,
            providerKey,
            interval,
            from.ToUniversalTime(),
            to.ToUniversalTime());

        var history = await provider.GetHistoryAsync(
            providerKey, interval, from, to, cancellationToken);

        _logger.LogInformation(
            "[MarketData] History result symbol={Symbol} provider={ProviderName} count={Count}",
            symbol,
            provider.ProviderName,
            history.Count);

        return history;
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

    private static bool TryGetProviderCooldownRemaining(
        string providerName,
        out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (!ProviderCircuitStates.TryGetValue(providerName, out var state))
            return false;

        lock (state.SyncRoot)
        {
            if (state.CooldownUntilUtc is null)
                return false;

            var now = DateTimeOffset.UtcNow;

            if (state.CooldownUntilUtc <= now)
            {
                state.CooldownUntilUtc = null;
                return false;
            }

            remaining = state.CooldownUntilUtc.Value - now;
            return true;
        }
    }

    private static ProviderCircuitOutcome RegisterProvider429Failure(string providerName)
    {
        var state = ProviderCircuitStates.GetOrAdd(providerName, _ => new ProviderCircuitState());

        lock (state.SyncRoot)
        {
            state.Consecutive429Failures++;

            if (state.Consecutive429Failures < Provider429FailureThreshold)
            {
                return new ProviderCircuitOutcome(
                    CircuitOpened: false,
                    Consecutive429Count: state.Consecutive429Failures,
                    CooldownUntilUtc: null);
            }

            var cooldownUntil = DateTimeOffset.UtcNow.Add(ProviderCooldown);
            state.Consecutive429Failures = 0;
            state.CooldownUntilUtc = cooldownUntil;

            return new ProviderCircuitOutcome(
                CircuitOpened: true,
                Consecutive429Count: Provider429FailureThreshold,
                CooldownUntilUtc: cooldownUntil);
        }
    }

    private static void ResetProvider429Failures(string providerName)
    {
        if (!ProviderCircuitStates.TryGetValue(providerName, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.Consecutive429Failures = 0;

            if (state.CooldownUntilUtc <= DateTimeOffset.UtcNow)
                state.CooldownUntilUtc = null;
        }
    }

    private sealed class ProviderBatchBucket
    {
        public ProviderBatchBucket(IMarketDataProvider provider)
        {
            Provider = provider;
        }

        public IMarketDataProvider Provider { get; }

        public List<string> ProviderKeys { get; } = [];
    }

    private sealed class ProviderCircuitState
    {
        public object SyncRoot { get; } = new();

        public int Consecutive429Failures { get; set; }

        public DateTimeOffset? CooldownUntilUtc { get; set; }
    }

    private sealed record ProviderCircuitOutcome(
        bool CircuitOpened,
        int Consecutive429Count,
        DateTimeOffset? CooldownUntilUtc);
}