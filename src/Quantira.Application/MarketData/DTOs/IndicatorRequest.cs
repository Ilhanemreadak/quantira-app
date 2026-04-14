namespace Quantira.Application.MarketData.DTOs;

/// <summary>
/// Represents a single indicator calculation request within a batch call.
/// Used by <see cref="IIndicatorEngine.CalculateBatchAsync"/> so that price
/// history is fetched once and reused across all indicators in the batch.
/// </summary>
/// <param name="IndicatorName">
/// Registered indicator name (e.g. "RSI", "MACD", "BOLLINGER").
/// Must match a registered <c>IIndicator.Name</c>.
/// </param>
/// <param name="Interval">Candlestick interval (e.g. "1d", "1h", "15m").</param>
/// <param name="Parameters">
/// Optional key-value configuration overrides for this indicator.
/// Defaults are applied for any omitted parameters.
/// </param>
public sealed record IndicatorRequest(
    string IndicatorName,
    string Interval,
    Dictionary<string, string>? Parameters = null);
