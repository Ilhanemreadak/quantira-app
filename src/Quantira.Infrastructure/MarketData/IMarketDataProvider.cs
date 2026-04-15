using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.MarketData;

/// <summary>
/// Contract that all external market data provider adapters must implement.
/// Each provider handles a specific subset of asset types and exchanges.
/// <see cref="MarketDataProviderFactory"/> uses <see cref="CanHandle"/>
/// to route requests to the correct provider at runtime.
/// Adding a new data source requires only a new class implementing this
/// interface and registering it in DI — no other changes needed.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Short display name used in logs and diagnostics.
    /// Examples: "Yahoo Finance", "Binance", "GoldApi".
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Returns <c>true</c> when this provider can supply data for
    /// the given asset type and exchange combination.
    /// </summary>
    bool CanHandle(AssetType assetType, string? exchange = null);

    /// <summary>Returns the latest price snapshot for a single symbol.</summary>
    Task<PriceLatestDto> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns latest price snapshots for multiple symbols in a single
    /// batched request to minimise API round-trips.
    /// </summary>
    Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>Returns OHLCV candlestick data for the given symbol and interval.</summary>
    Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the given exchange is in an active trading session.
    /// </summary>
    Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default);
}