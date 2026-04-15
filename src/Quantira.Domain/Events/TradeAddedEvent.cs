using Quantira.Domain.Common;
using Quantira.Domain.Enums;

namespace Quantira.Domain.Events;

/// <summary>
/// Raised when a trade is successfully recorded against a portfolio position.
/// Triggers a chain of side effects in the application layer:
/// position quantity and average cost recalculation, portfolio total value
/// refresh in Redis, and a SignalR broadcast to connected dashboard clients.
/// This is the most frequently raised event in the Quantira domain.
/// </summary>
public sealed class TradeAddedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <summary>The portfolio the trade was recorded against.</summary>
    public Guid PortfolioId { get; }

    /// <summary>The asset that was bought, sold, or transferred.</summary>
    public Guid AssetId { get; }

    /// <summary>The unique identifier of the trade record itself.</summary>
    public Guid TradeId { get; }

    /// <summary>
    /// The type of trade that was recorded.
    /// Handlers use this to decide whether position recalculation is needed
    /// (e.g. <see cref="TradeType.Dividend"/> does not change quantity).
    /// </summary>
    public TradeType TradeType { get; }

    /// <summary>The quantity of the asset involved in the trade.</summary>
    public decimal Quantity { get; }

    /// <summary>The per-unit price at which the trade was executed.</summary>
    public decimal Price { get; }

    /// <summary>The currency the trade price is denominated in.</summary>
    public string PriceCurrency { get; }

    public TradeAddedEvent(
        Guid portfolioId,
        Guid assetId,
        Guid tradeId,
        TradeType tradeType,
        decimal quantity,
        decimal price,
        string priceCurrency)
    {
        PortfolioId = portfolioId;
        AssetId = assetId;
        TradeId = tradeId;
        TradeType = tradeType;
        Quantity = quantity;
        Price = price;
        PriceCurrency = priceCurrency;
    }
}