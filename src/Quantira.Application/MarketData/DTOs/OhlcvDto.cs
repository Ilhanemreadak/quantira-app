namespace Quantira.Application.MarketData.DTOs;

/// <summary>
/// Represents a single OHLCV candlestick data point.
/// Returned by <c>GetPriceHistoryQueryHandler</c> and consumed
/// by the TradingView Lightweight Charts component on the frontend.
/// Field names follow the TradingView CandlestickData interface
/// to minimize client-side mapping.
/// </summary>
/// <param name="Time">
/// UTC period start timestamp as Unix epoch seconds.
/// TradingView requires epoch time in its data series.
/// </param>
/// <param name="Open">Opening price for the period.</param>
/// <param name="High">Highest price reached during the period.</param>
/// <param name="Low">Lowest price reached during the period.</param>
/// <param name="Close">Closing price for the period.</param>
/// <param name="Volume">Total traded volume during the period.</param>
public sealed record OhlcvDto(
    long Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);