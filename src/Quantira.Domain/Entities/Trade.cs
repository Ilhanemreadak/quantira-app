using Quantira.Domain.Common;
using Quantira.Domain.Enums;
using Quantira.Domain.Exceptions;
using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Entities;

/// <summary>
/// Represents an immutable financial transaction recorded against a portfolio.
/// Trades are append-only records — once persisted they are never modified.
/// They serve as the source of truth for position reconstruction and
/// realized P&amp;L calculation. Soft-deletion is supported for compliance
/// corrections but requires supervisor-level authorization enforced in
/// the application layer.
/// </summary>
public sealed class Trade : Entity<Guid>
{
    /// <summary>The portfolio this trade belongs to.</summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>The asset that was bought, sold, or transferred.</summary>
    public Guid AssetId { get; private set; }

    /// <summary>The nature of this transaction.</summary>
    public TradeType TradeType { get; private set; }

    /// <summary>
    /// The number of units involved. Uses 8 decimal places
    /// to support fractional crypto quantities.
    /// Always positive — direction is encoded in <see cref="TradeType"/>.
    /// </summary>
    public decimal Quantity { get; private set; }

    /// <summary>Per-unit execution price at the time of the trade.</summary>
    public Money Price { get; private set; } = default!;

    /// <summary>
    /// Currency the price is denominated in.
    /// May differ from the portfolio's <c>BaseCurrency</c>.
    /// </summary>
    public string PriceCurrency { get; private set; } = default!;

    /// <summary>
    /// Brokerage or exchange commission paid for this trade.
    /// Included in cost basis calculations for buy trades.
    /// </summary>
    public Money Commission { get; private set; } = default!;

    /// <summary>Withholding tax or transaction tax applied to this trade.</summary>
    public Money TaxAmount { get; private set; } = default!;

    /// <summary>Optional free-text note for this trade.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// The actual execution time of the trade as provided by the user.
    /// May be in the past to support importing historical trades.
    /// Stored and compared in UTC.
    /// </summary>
    public DateTime TradedAt { get; private set; }

    /// <summary>UTC timestamp of when this record was created in Quantira.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gross trade value: <see cref="Price"/> × <see cref="Quantity"/>.
    /// Does not include commission or tax.
    /// </summary>
    public Money GrossValue => Price.Multiply(Quantity);

    /// <summary>
    /// Net cost or proceeds: GrossValue + Commission + TaxAmount for buys,
    /// GrossValue - Commission - TaxAmount for sells.
    /// </summary>
    public Money NetValue => TradeType is TradeType.Buy or TradeType.TransferIn
        ? GrossValue.Add(Commission).Add(TaxAmount)
        : GrossValue.Subtract(Commission).Subtract(TaxAmount);

    private Trade() { }

    /// <summary>
    /// Creates a new trade record. Called exclusively by
    /// <see cref="Portfolio.AddTrade"/> to enforce aggregate boundary rules.
    /// </summary>
    internal static Trade Create(
        Guid portfolioId,
        Guid assetId,
        TradeType tradeType,
        decimal quantity,
        decimal price,
        string priceCurrency,
        decimal commission = 0m,
        decimal taxAmount = 0m,
        DateTime? tradedAt = null,
        string? notes = null)
    {
        if (quantity <= 0)
            throw new DomainException($"Trade quantity must be positive. Received: {quantity}");

        if (price < 0)
            throw new DomainException($"Trade price cannot be negative. Received: {price}");

        if (commission < 0)
            throw new DomainException($"Commission cannot be negative. Received: {commission}");

        if (taxAmount < 0)
            throw new DomainException($"Tax amount cannot be negative. Received: {taxAmount}");

        var currency = Currency.From(priceCurrency);

        return new Trade
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            AssetId = assetId,
            TradeType = tradeType,
            Quantity = quantity,
            Price = Money.Of(price, currency),
            PriceCurrency = priceCurrency.Trim().ToUpperInvariant(),
            Commission = Money.Of(commission, currency),
            TaxAmount = Money.Of(taxAmount, currency),
            Notes = notes?.Trim(),
            TradedAt = tradedAt.HasValue
                                ? DateTime.SpecifyKind(tradedAt.Value, DateTimeKind.Utc)
                                : DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}