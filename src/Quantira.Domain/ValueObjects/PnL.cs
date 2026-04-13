using Quantira.Domain.Common;

namespace Quantira.Domain.ValueObjects;

/// <summary>
/// Represents the profit and loss breakdown of a portfolio position.
/// Separates realized P&amp;L (from closed trades) and unrealized P&amp;L
/// (from open positions marked to current market price) because they
/// have different tax and reporting implications.
/// All amounts are expressed in the same currency to allow aggregation.
/// </summary>
public sealed class PnL : ValueObject
{
    /// <summary>
    /// Profit or loss from trades that have been fully closed.
    /// Realized P&amp;L is locked in and has tax implications.
    /// </summary>
    public Money Realized { get; }

    /// <summary>
    /// Profit or loss from currently open positions based on the
    /// latest market price. Fluctuates with price movements and
    /// has no tax implication until the position is closed.
    /// </summary>
    public Money Unrealized { get; }

    /// <summary>The currency all P&amp;L figures are denominated in.</summary>
    public Currency Currency => Realized.Currency;

    private PnL(Money realized, Money unrealized)
    {
        Realized = realized;
        Unrealized = unrealized;
    }

    /// <summary>
    /// Creates a new <see cref="PnL"/> instance.
    /// </summary>
    /// <param name="realized">Realized P&amp;L amount.</param>
    /// <param name="unrealized">Unrealized P&amp;L amount.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="realized"/> and <paramref name="unrealized"/>
    /// are denominated in different currencies.
    /// </exception>
    public static PnL Of(Money realized, Money unrealized)
    {
        if (realized.Currency != unrealized.Currency)
            throw new ArgumentException(
                "Realized and unrealized P&L must be in the same currency.");

        return new PnL(realized, unrealized);
    }

    /// <summary>Returns a zero P&amp;L for the given currency.</summary>
    public static PnL Zero(Currency currency)
        => new(Money.Zero(currency), Money.Zero(currency));

    /// <summary>The combined total of realized and unrealized P&amp;L.</summary>
    public Money Total => Realized.Add(Unrealized);

    /// <summary>
    /// Calculates the total P&amp;L as a percentage of the given cost basis.
    /// Returns zero when <paramref name="totalCost"/> is zero to avoid division by zero.
    /// </summary>
    /// <param name="totalCost">The original cost basis of the position.</param>
    public decimal TotalPercentage(Money totalCost)
        => totalCost.Amount == 0m
            ? 0m
            : Total.Amount / totalCost.Amount * 100m;

    public override string ToString()
        => $"Realized: {Realized} | Unrealized: {Unrealized} | Total: {Total}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Realized;
        yield return Unrealized;
    }
}