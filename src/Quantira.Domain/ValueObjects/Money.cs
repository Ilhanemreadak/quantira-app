using Quantira.Domain.Common;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with an associated currency.
/// Encapsulates all currency-aware arithmetic operations and enforces
/// the invariant that two <see cref="Money"/> values can only be combined
/// if they share the same <see cref="Currency"/>. Using <c>decimal</c>
/// internally guarantees exact representation for financial calculations —
/// never use <c>float</c> or <c>double</c> for monetary values.
/// </summary>
public sealed class Money : ValueObject
{
    /// <summary>The monetary amount. Always non-negative for cost/price contexts.</summary>
    public decimal Amount { get; }

    /// <summary>The currency this amount is denominated in.</summary>
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new <see cref="Money"/> instance.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">The currency of the amount.</param>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="amount"/> is negative.
    /// </exception>
    public static Money Of(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new DomainException($"Money amount cannot be negative. Received: {amount}");

        return new Money(amount, currency);
    }

    /// <summary>Returns a zero-value <see cref="Money"/> for the given currency.</summary>
    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>
    /// Adds two monetary amounts. Both must share the same currency.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the currencies do not match.
    /// </exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtracts <paramref name="other"/> from this amount.
    /// Both must share the same currency. Result may be negative
    /// (e.g. unrealized loss scenarios).
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the currencies do not match.
    /// </exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>
    /// Multiplies this amount by a scalar factor.
    /// Used for quantity-based cost calculations (e.g. price × quantity).
    /// </summary>
    public Money Multiply(decimal factor)
        => new(Amount * factor, Currency);

    /// <summary>
    /// Converts this amount to a target currency using the provided exchange rate.
    /// The rate represents how many units of <paramref name="targetCurrency"/>
    /// equal one unit of this object's currency.
    /// </summary>
    /// <param name="targetCurrency">The currency to convert to.</param>
    /// <param name="exchangeRate">The conversion rate. Must be greater than zero.</param>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="exchangeRate"/> is zero or negative.
    /// </exception>
    public Money ConvertTo(Currency targetCurrency, decimal exchangeRate)
    {
        if (exchangeRate <= 0)
            throw new DomainException($"Exchange rate must be positive. Received: {exchangeRate}");

        return new Money(Amount * exchangeRate, targetCurrency);
    }

    public bool IsZero => Amount == 0m;

    public override string ToString() => $"{Amount:F4} {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException(
                $"Currency mismatch: cannot operate on {Currency} and {other.Currency}.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}