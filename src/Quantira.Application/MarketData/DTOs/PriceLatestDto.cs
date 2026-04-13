namespace Quantira.Application.MarketData.DTOs;

/// <summary>
/// Represents the latest price snapshot for a single asset.
/// Populated from Redis by <c>MarketDataRefreshJob</c> every 15 seconds
/// and broadcast to dashboard clients via SignalR.
/// </summary>
/// <param name="Symbol">Asset ticker symbol.</param>
/// <param name="Price">Current market price.</param>
/// <param name="Change">Absolute price change since previous close.</param>
/// <param name="ChangePct">Percentage change since previous close.</param>
/// <param name="DayHigh">Highest price of the current trading session.</param>
/// <param name="DayLow">Lowest price of the current trading session.</param>
/// <param name="Volume">Total volume for the current trading session.</param>
/// <param name="MarketStatus">Current trading session status.</param>
/// <param name="Timestamp">UTC timestamp of when this snapshot was taken.</param>
public sealed record PriceLatestDto(
    string Symbol,
    decimal Price,
    decimal Change,
    decimal ChangePct,
    decimal? DayHigh,
    decimal? DayLow,
    long? Volume,
    string MarketStatus,
    DateTime Timestamp);