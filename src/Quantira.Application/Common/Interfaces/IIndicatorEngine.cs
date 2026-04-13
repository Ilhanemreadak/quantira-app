using Quantira.Application.Common.Models;

namespace Quantira.Application.Common.Interfaces;

/// <summary>
/// Abstracts technical indicator calculation from the application layer.
/// The infrastructure implementation auto-registers all <c>IIndicator</c>
/// implementations via DI and routes calculation requests to the correct
/// one by name. Results are cached in Redis with a configurable TTL to
/// avoid redundant recalculations within the same price refresh cycle.
/// </summary>
public interface IIndicatorEngine
{
    /// <summary>
    /// Calculates a single technical indicator for the given symbol and interval.
    /// Checks Redis for a cached result before running the calculation.
    /// </summary>
    /// <param name="symbol">Asset ticker symbol (e.g. "THYAO", "BTC").</param>
    /// <param name="indicatorName">
    /// The indicator identifier (e.g. "RSI", "MACD", "BOLLINGER").
    /// Must match a registered <c>IIndicator.Name</c>.
    /// </param>
    /// <param name="interval">
    /// Candlestick interval (e.g. "1d", "1h", "15m").
    /// </param>
    /// <param name="parameters">
    /// Optional key-value pairs for indicator configuration
    /// (e.g. <c>period=14</c> for RSI, <c>fast=12,slow=26</c> for MACD).
    /// Defaults are applied for any omitted parameters.
    /// </param>
    Task<IndicatorResultDto> CalculateAsync(
        string symbol,
        string indicatorName,
        string interval,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates multiple indicators for the same symbol in a single call.
    /// Price history is fetched once and reused across all calculations.
    /// </summary>
    Task<IReadOnlyList<IndicatorResultDto>> CalculateBatchAsync(
        string symbol,
        IEnumerable<IndicatorRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the metadata of all registered indicators including their
    /// name, description, required parameters and supported asset types.
    /// Used to populate the indicator selector in the frontend.
    /// </summary>
    IReadOnlyList<IndicatorMetadata> GetAvailableIndicators();
}