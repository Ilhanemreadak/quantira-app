namespace Quantira.Domain.Enums;

/// <summary>
/// Represents the current trading session status of a financial exchange.
/// Stored in Redis with a short TTL and checked by <c>MarketDataRefreshJob</c>
/// to skip unnecessary API calls when markets are closed.
/// Also surfaced to the frontend so the UI can display a visual indicator
/// of whether displayed prices are live or from the last closed session.
/// </summary>
public enum MarketStatus
{
    /// <summary>Regular trading session is active. Prices update in real time.</summary>
    Open = 1,

    /// <summary>Exchange is closed (weekend, holiday, or outside trading hours).</summary>
    Closed = 2,

    /// <summary>
    /// Pre-market trading session is active (applicable to US exchanges).
    /// Liquidity is lower and price movements may be more volatile.
    /// </summary>
    PreMarket = 3,

    /// <summary>
    /// After-hours trading session is active (applicable to US exchanges).
    /// Similar liquidity characteristics to pre-market.
    /// </summary>
    AfterHours = 4
}