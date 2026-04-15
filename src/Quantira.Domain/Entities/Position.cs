using Quantira.Domain.Common;
using Quantira.Domain.Enums;
using Quantira.Domain.Exceptions;
using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Entities;

/// <summary>
/// Represents the current holding of a single asset within a portfolio.
/// A position is a derived entity — its state is always a function of
/// the trade history for that asset in the portfolio. It is updated
/// by <see cref="Portfolio.AddTrade"/> and must never be mutated directly
/// from outside the portfolio aggregate boundary.
/// Stores both cost basis data (from trades) and current market value
/// (refreshed by <c>AssetPriceUpdatedEvent</c> handler).
/// </summary>
public sealed class Position : Entity<Guid>
{
    /// <summary>The portfolio this position belongs to.</summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>The asset being held.</summary>
    public Guid AssetId { get; private set; }

    /// <summary>
    /// Current quantity held. Updated after every buy/sell trade.
    /// Uses 8 decimal places to support crypto assets.
    /// A zero quantity means the position is fully closed.
    /// </summary>
    public decimal Quantity { get; private set; }

    /// <summary>
    /// The weighted average cost per unit based on the portfolio's
    /// <see cref="CostMethod"/>. Recalculated on every buy trade.
    /// </summary>
    public Money AvgCostPrice { get; private set; } = default!;

    /// <summary>
    /// Total cost basis of the current position: Quantity × AvgCostPrice.
    /// Used as the denominator for unrealized P&amp;L percentage.
    /// </summary>
    public Money TotalCost { get; private set; } = default!;

    /// <summary>
    /// Current market value of the position based on the latest price
    /// from Redis. Updated by <c>UpdateMarketValuesAsync</c> in the
    /// position repository on each price refresh cycle.
    /// </summary>
    public Money? CurrentValue { get; private set; }

    /// <summary>
    /// Unrealized P&amp;L: CurrentValue minus TotalCost.
    /// <c>null</c> until the first market price is received.
    /// </summary>
    public Money? UnrealizedPnL { get; private set; }

    /// <summary>
    /// Unrealized P&amp;L as a percentage of TotalCost.
    /// <c>null</c> until the first market price is received.
    /// </summary>
    public decimal? UnrealizedPnLPct { get; private set; }

    /// <summary>UTC timestamp of the last market value refresh.</summary>
    public DateTime LastUpdated { get; private set; }

    private Position() { }

    internal static Position Create(Guid portfolioId, Guid assetId, string currency)
    {
        return new Position
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            AssetId = assetId,
            Quantity = 0m,
            AvgCostPrice = Money.Zero(Currency.From(currency)),
            TotalCost = Money.Zero(Currency.From(currency)),
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Applies a trade to this position, updating quantity and cost basis
    /// according to the portfolio's configured <see cref="CostMethod"/>.
    /// Internal — called only by <see cref="Portfolio.AddTrade"/>.
    /// </summary>
    internal void ApplyTrade(Trade trade, CostMethod costMethod)
    {
        switch (trade.TradeType)
        {
            case TradeType.Buy:
            case TradeType.TransferIn:
                ApplyBuy(trade, costMethod);
                break;

            case TradeType.Sell:
            case TradeType.TransferOut:
                ApplySell(trade);
                break;

            case TradeType.Split:
                ApplySplit(trade);
                break;

            case TradeType.Dividend:
                // Dividend does not affect quantity or cost basis.
                break;
        }

        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the current market value and recalculates unrealized P&amp;L.
    /// Called by the infrastructure layer on each price refresh cycle.
    /// </summary>
    public void UpdateMarketValue(decimal currentPrice, string currency)
    {
        var priceCurrency = Currency.From(currency);
        CurrentValue = Money.Of(Quantity * currentPrice, priceCurrency);
        UnrealizedPnL = CurrentValue.Subtract(TotalCost);
        UnrealizedPnLPct = TotalCost.Amount == 0
            ? 0m
            : UnrealizedPnL.Amount / TotalCost.Amount * 100m;
        LastUpdated = DateTime.UtcNow;
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void ApplyBuy(Trade trade, CostMethod costMethod)
    {
        var tradeCurrency = Currency.From(trade.PriceCurrency);
        var tradeCost = Money.Of(trade.Quantity * trade.Price.Amount, tradeCurrency)
                               .Add(Money.Of(trade.Commission.Amount, tradeCurrency));

        if (costMethod == CostMethod.Average || Quantity == 0)
        {
            var newQuantity = Quantity + trade.Quantity;
            var newTotalCost = TotalCost.Add(tradeCost);
            AvgCostPrice = Money.Of(newTotalCost.Amount / newQuantity, tradeCurrency);
            TotalCost = newTotalCost;
            Quantity = newQuantity;
        }
        else
        {
            // FIFO / LIFO — cost basis is the sum of all open lots.
            // Lot tracking is handled by the trade history in ITradeRepository.
            Quantity += trade.Quantity;
            TotalCost = TotalCost.Add(tradeCost);
            AvgCostPrice = Money.Of(TotalCost.Amount / Quantity, tradeCurrency);
        }
    }

    private void ApplySell(Trade trade)
    {
        if (trade.Quantity > Quantity)
            throw new DomainException(
                $"Cannot sell {trade.Quantity} units — only {Quantity} units held.");

        Quantity -= trade.Quantity;
        TotalCost = Quantity == 0
            ? Money.Zero(TotalCost.Currency)
            : Money.Of(AvgCostPrice.Amount * Quantity, TotalCost.Currency);
    }

    private void ApplySplit(Trade trade)
    {
        // Split ratio encoded as quantity (new shares) at price (ratio denominator).
        // e.g. 2-for-1 split: quantity=2, price=1 → ratio=2.0
        if (trade.Price.Amount <= 0)
            throw new DomainException("Split ratio denominator must be positive.");

        var splitRatio = trade.Quantity / trade.Price.Amount;
        Quantity *= splitRatio;
        AvgCostPrice = Money.Of(AvgCostPrice.Amount / splitRatio, AvgCostPrice.Currency);
        // TotalCost remains the same after a split.
    }
}