namespace Quantira.Application.MarketData.DTOs;

/// <summary>
/// Carries the result of a technical indicator calculation.
/// Returned by <c>CalculateIndicatorQueryHandler</c> and consumed
/// by the chart overlay renderer on the frontend.
/// </summary>
/// <param name="Symbol">Asset the indicator was calculated for.</param>
/// <param name="IndicatorName">The name of the calculated indicator.</param>
/// <param name="Interval">The candlestick interval used.</param>
/// <param name="Values">
/// Ordered list of (timestamp, value) pairs aligned to the OHLCV series.
/// Multi-line indicators (e.g. MACD with signal and histogram) use
/// additional named series in <see cref="AdditionalSeries"/>.
/// </param>
/// <param name="AdditionalSeries">
/// Named secondary series for multi-output indicators.
/// Example: MACD returns "Signal" and "Histogram" here.
/// </param>
/// <param name="CalculatedAt">UTC timestamp of when the result was computed.</param>
public sealed record IndicatorResultDto(
    string Symbol,
    string IndicatorName,
    string Interval,
    IReadOnlyList<IndicatorDataPoint> Values,
    Dictionary<string, IReadOnlyList<IndicatorDataPoint>>? AdditionalSeries,
    DateTime CalculatedAt);

/// <summary>A single time-value pair within an indicator series.</summary>
/// <param name="Time">Unix epoch seconds aligned to the OHLCV series.</param>
/// <param name="Value">The indicator value at this point in time.</param>
public sealed record IndicatorDataPoint(long Time, decimal? Value);