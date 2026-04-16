using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators;

/// <summary>
/// Contract for all technical indicator implementations.
/// Each indicator is a self-contained calculation unit registered
/// in DI via auto-discovery. Adding a new indicator requires only
/// creating a new class that implements this interface — the engine
/// picks it up automatically via IEnumerable{IIndicator} injection.
/// </summary>
public interface IIndicator
{
    /// <summary>Unique identifier used in API requests (e.g. "RSI").</summary>
    string Name { get; }

    /// <summary>Human-readable display name.</summary>
    string DisplayName { get; }

    /// <summary>Short description of what the indicator measures.</summary>
    string Description { get; }

    /// <summary>UI grouping category (e.g. "Momentum", "Trend", "Volatility").</summary>
    string Category { get; }

    /// <summary>Minimum number of candles required for a valid calculation.</summary>
    int MinimumPeriod { get; }

    /// <summary>Default parameters shown in the indicator settings panel.</summary>
    Dictionary<string, string> DefaultParameters { get; }

    /// <summary>Asset types this indicator supports.</summary>
    IReadOnlyList<AssetType> SupportedAssetTypes { get; }

    /// <summary>
    /// Performs the indicator calculation on the given OHLCV data.
    /// </summary>
    /// <param name="candles">
    /// Ordered list of OHLCV candles, oldest first.
    /// Must contain at least <see cref="MinimumPeriod"/> items.
    /// </param>
    /// <param name="parameters">
    /// Key-value overrides for indicator parameters.
    /// Missing keys fall back to <see cref="DefaultParameters"/>.
    /// </param>
    IndicatorResult Calculate(
        IReadOnlyList<OhlcvDto> candles,
        Dictionary<string, string>? parameters = null);
}

/// <summary>
/// The raw output of an indicator calculation before it is mapped
/// to <see cref="IndicatorResultDto"/>.
/// </summary>
public sealed class IndicatorResult
{
    /// <summary>Primary value series aligned to the input candles by time.</summary>
    public IReadOnlyList<IndicatorDataPoint> Values { get; init; } = [];

    /// <summary>
    /// Optional secondary series for multi-output indicators.
    /// MACD returns "Signal" and "Histogram" here.
    /// Bollinger returns "Upper" and "Lower" here.
    /// </summary>
    public Dictionary<string, IReadOnlyList<IndicatorDataPoint>>?
        AdditionalSeries
    { get; init; }
}