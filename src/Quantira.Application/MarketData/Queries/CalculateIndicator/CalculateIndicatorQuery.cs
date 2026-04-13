using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Application.MarketData.DTOs;

namespace Quantira.Application.MarketData.Queries.CalculateIndicator;

/// <summary>
/// Query to calculate a technical indicator for a given symbol and interval.
/// The indicator engine checks Redis for a cached result first.
/// Cache duration is 5 minutes — short enough to reflect intraday price
/// changes while avoiding redundant recalculations on every chart render.
/// </summary>
/// <param name="Symbol">Asset ticker symbol.</param>
/// <param name="IndicatorName">
/// Registered indicator name (e.g. "RSI", "MACD", "BOLLINGER").
/// </param>
/// <param name="Interval">Candlestick interval used for the calculation.</param>
/// <param name="Parameters">
/// Optional key-value configuration overrides.
/// Defaults are applied for any omitted parameters.
/// </param>
public sealed record CalculateIndicatorQuery(
    string Symbol,
    string IndicatorName,
    string Interval,
    Dictionary<string, string>? Parameters = null
) : IRequest<IndicatorResultDto>, ICacheableQuery
{
    public string CacheKey =>
        $"quantira:indicator:{Symbol.ToUpperInvariant()}:{IndicatorName}:{Interval}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}