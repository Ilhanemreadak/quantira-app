using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Application.MarketData.DTOs;

namespace Quantira.Application.MarketData.Queries.GetPriceHistory;

/// <summary>
/// Query to retrieve OHLCV candlestick data for a given symbol and interval.
/// Used by the chart component to render price history with technical indicators.
/// Results are cached for 5 minutes for intraday intervals and 60 minutes
/// for daily and above, since historical data does not change frequently.
/// </summary>
/// <param name="Symbol">Asset ticker symbol.</param>
/// <param name="Interval">
/// Candlestick interval. Supported values: "1m", "5m", "15m", "1h", "1d", "1wk", "1mo".
/// </param>
/// <param name="From">UTC start of the requested date range.</param>
/// <param name="To">UTC end of the requested date range.</param>
public sealed record GetPriceHistoryQuery(
    string Symbol,
    string Interval,
    DateTime From,
    DateTime To
) : IRequest<IReadOnlyList<OhlcvDto>>, ICacheableQuery
{
    private static readonly HashSet<string> IntradayIntervals = ["1m", "5m", "15m", "1h"];

    public string CacheKey =>
        $"quantira:pricehistory:{Symbol.ToUpperInvariant()}:{Interval}:{From:yyyyMMdd}:{To:yyyyMMdd}";

    public TimeSpan? CacheDuration =>
        IntradayIntervals.Contains(Interval)
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromMinutes(60);
}