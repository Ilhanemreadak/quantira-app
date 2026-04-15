using Quantira.Domain.Common;

namespace Quantira.Domain.ValueObjects;

/// <summary>
/// Represents an ISO 4217 currency code (e.g. "USD", "TRY", "EUR").
/// Encapsulates validation so that invalid currency codes can never
/// exist in the domain. Stored as an uppercase, trimmed three-letter string.
/// </summary>
public sealed class Currency : ValueObject
{
    /// <summary>The ISO 4217 three-letter currency code in uppercase.</summary>
    public string Code { get; private set; } = default!;

    public static readonly Currency TRY = new("TRY");
    public static readonly Currency USD = new("USD");
    public static readonly Currency EUR = new("EUR");
    public static readonly Currency GBP = new("GBP");
    public static readonly Currency BTC = new("BTC");
    public static readonly Currency ETH = new("ETH");

    // Parameterless constructor required by EF Core for owned entity materialization.
    private Currency() { }

    private Currency(string code) => Code = code;

    /// <summary>
    /// Creates a <see cref="Currency"/> instance from the given code.
    /// </summary>
    /// <param name="code">ISO 4217 currency code. Must be exactly 3 letters.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="code"/> is null, empty, or not exactly 3 letters.
    /// </exception>
    public static Currency From(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant()
            ?? throw new ArgumentException(
                "Currency code cannot be null.", nameof(code));

        if (normalized.Length != 3 || !normalized.All(char.IsLetter))
            throw new ArgumentException(
                $"'{code}' is not a valid ISO 4217 currency code. Expected exactly 3 letters.",
                nameof(code));

        return new Currency(normalized);
    }

    public override string ToString() => Code;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}