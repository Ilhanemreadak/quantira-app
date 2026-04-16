using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Exceptions;

namespace Quantira.Infrastructure.Indicators;

/// <summary>
/// Routes indicator calculation requests to the correct
/// <see cref="IIndicator"/> implementation. All registered indicators
/// are injected via <c>IEnumerable{IIndicator}</c> — adding a new
/// indicator class automatically makes it available here and in the
/// frontend selector without any registration changes.
/// Results are cached in Redis via the <c>CachingBehavior</c> pipeline
/// at the query layer — this engine always performs a fresh calculation.
/// </summary>
public sealed class IndicatorEngine : IIndicatorEngine
{
    private readonly IMarketDataService _marketDataService;
    private readonly IReadOnlyDictionary<string, IIndicator> _indicators;
    private readonly ILogger<IndicatorEngine> _logger;

    public IndicatorEngine(
        IEnumerable<IIndicator> indicators,
        IMarketDataService marketDataService,
        ILogger<IndicatorEngine> logger)
    {
        _marketDataService = marketDataService;
        _indicators = indicators.ToDictionary(
            i => i.Name.ToUpperInvariant(),
            i => i);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IndicatorResultDto> CalculateAsync(
        string symbol,
        string indicatorName,
        string interval,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var key = indicatorName.ToUpperInvariant();

        if (!_indicators.TryGetValue(key, out var indicator))
            throw new DomainException(
                $"Indicator '{indicatorName}' is not registered. " +
                $"Available: {string.Join(", ", _indicators.Keys)}");

        var candles = await FetchCandlesAsync(
            symbol, interval, indicator.MinimumPeriod * 3,
            cancellationToken);

        if (candles.Count < indicator.MinimumPeriod)
            throw new DomainException(
                $"Not enough candle data for {indicatorName}. " +
                $"Need {indicator.MinimumPeriod}, got {candles.Count}.");

        _logger.LogDebug(
            "[IndicatorEngine] Calculating {Indicator} for {Symbol} " +
            "interval={Interval} candles={Count}",
            indicatorName, symbol, interval, candles.Count);

        var result = indicator.Calculate(candles, parameters);

        return new IndicatorResultDto(
            Symbol: symbol,
            IndicatorName: indicatorName,
            Interval: interval,
            Values: result.Values,
            AdditionalSeries: result.AdditionalSeries,
            CalculatedAt: DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IndicatorResultDto>> CalculateBatchAsync(
        string symbol,
        IEnumerable<IndicatorRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests.ToList();

        // Fetch candles once — reuse across all indicators.
        var maxPeriod = requestList
            .Select(r => _indicators.TryGetValue(
                r.IndicatorName.ToUpperInvariant(), out var ind)
                ? ind.MinimumPeriod : 0)
            .DefaultIfEmpty(0)
            .Max();

        var candles = await FetchCandlesAsync(
            symbol, "1d", maxPeriod * 3, cancellationToken);

        var results = new List<IndicatorResultDto>(requestList.Count);

        foreach (var request in requestList)
        {
            try
            {
                var key = request.IndicatorName.ToUpperInvariant();
                if (!_indicators.TryGetValue(key, out var indicator)) continue;

                var result = indicator.Calculate(candles, request.Parameters);

                results.Add(new IndicatorResultDto(
                    Symbol: symbol,
                    IndicatorName: request.IndicatorName,
                    Interval: request.Interval ?? "1d",
                    Values: result.Values,
                    AdditionalSeries: result.AdditionalSeries,
                    CalculatedAt: DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[IndicatorEngine] Failed to calculate {Indicator} for {Symbol}.",
                    request.IndicatorName, symbol);
            }
        }

        return results.AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IndicatorMetadata> GetAvailableIndicators()
    {
        return _indicators.Values
            .Select(i => new IndicatorMetadata(
                Name: i.Name,
                DisplayName: i.DisplayName,
                Description: i.Description,
                Category: i.Category,
                MinimumPeriod: i.MinimumPeriod,
                DefaultParameters: i.DefaultParameters,
                SupportedAssetTypes: i.SupportedAssetTypes))
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ToList()
            .AsReadOnly();
    }

    private async Task<IReadOnlyList<OhlcvDto>> FetchCandlesAsync(
        string symbol,
        string interval,
        int count,
        CancellationToken cancellationToken)
    {
        var to = DateTime.UtcNow;
        var from = interval switch
        {
            "1m" => to.AddMinutes(-count),
            "5m" => to.AddMinutes(-count * 5),
            "15m" => to.AddMinutes(-count * 15),
            "1h" => to.AddHours(-count),
            "1d" => to.AddDays(-count),
            "1wk" => to.AddDays(-count * 7),
            "1mo" => to.AddMonths(-count),
            _ => to.AddDays(-count)
        };

        return await _marketDataService.GetHistoryAsync(
            symbol, interval, from, to, cancellationToken);
    }
}