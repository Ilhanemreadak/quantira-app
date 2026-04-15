using Quantira.Domain.Common;

namespace Quantira.Domain.Events;

/// <summary>
/// Raised by <c>MarketDataRefreshJob</c> after a new price is written
/// to Redis for a tracked asset. Consumed by multiple handlers in parallel:
/// the SignalR hub handler broadcasts the new price to all subscribed dashboard
/// clients, the alert check handler evaluates active price alerts against
/// the new value, and the position value handler refreshes unrealized P&amp;L
/// for any portfolio holding this asset.
/// High-frequency event — raised every 15 seconds per active asset.
/// </summary>
public sealed class AssetPriceUpdatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <summary>The asset whose price was updated.</summary>
    public Guid AssetId { get; }

    /// <summary>The ticker symbol of the asset (e.g. "THYAO", "BTC").</summary>
    public string Symbol { get; }

    /// <summary>The new market price.</summary>
    public decimal NewPrice { get; }

    /// <summary>The previous market price. Used to calculate change percentage.</summary>
    public decimal PreviousPrice { get; }

    /// <summary>The currency the price is denominated in.</summary>
    public string Currency { get; }

    /// <summary>
    /// Absolute price change: <see cref="NewPrice"/> minus <see cref="PreviousPrice"/>.
    /// </summary>
    public decimal Change => NewPrice - PreviousPrice;

    /// <summary>
    /// Percentage change relative to <see cref="PreviousPrice"/>.
    /// Returns zero when previous price is zero to avoid division by zero.
    /// </summary>
    public decimal ChangePercentage => PreviousPrice == 0
        ? 0m
        : (NewPrice - PreviousPrice) / PreviousPrice * 100m;

    public AssetPriceUpdatedEvent(
        Guid assetId,
        string symbol,
        decimal newPrice,
        decimal previousPrice,
        string currency)
    {
        AssetId = assetId;
        Symbol = symbol;
        NewPrice = newPrice;
        PreviousPrice = previousPrice;
        Currency = currency;
    }
}