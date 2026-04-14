using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Application.Common.Interfaces;

/// <summary>
/// Abstracts all external market data retrieval from the application layer.
/// The infrastructure implementation uses <c>MarketDataProviderFactory</c>
/// to route each request to the correct provider based on asset type:
/// stocks → Yahoo Finance, crypto → Binance, commodities → GoldApi/EIA.
/// All methods first check Redis for a cached value before hitting
/// the external API to stay within rate limits.
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Returns the latest market price for the given symbol.
    /// Reads from Redis cache first (TTL: 30 seconds).
    /// Falls back to the external provider on a cache miss.
    /// </summary>
    Task<decimal> GetCurrentPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full latest price snapshot for a single symbol
    /// including change, day high/low, volume and market status.
    /// </summary>
    Task<PriceLatestDto> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns latest price snapshots for multiple symbols in a single
    /// batched API call. Used by <c>MarketDataRefreshJob</c> to minimise
    /// the number of external requests per refresh cycle.
    /// </summary>
    Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns OHLCV candlestick data for the given symbol and interval
    /// within the specified date range.
    /// Data is sourced from <c>PriceHistory</c> table when available;
    /// the external provider is called only for gaps.
    /// </summary>
    Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the exchange hosting the given asset
    /// is currently in an active trading session.
    /// Result is cached in Redis for 5 minutes.
    /// </summary>
    Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}