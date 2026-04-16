using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Tests.ValueObjects;

public sealed class CurrencyTests
{
    public static IEnumerable<object?[]> ValidCurrencyCodes()
    {
        yield return ["usd", "USD"];
        yield return ["  try  ", "TRY"];
        yield return ["eUr", "EUR"];
    }

    public static IEnumerable<object?[]> InvalidCurrencyCodes()
    {
        yield return [null];
        yield return [""];
        yield return ["  "];
        yield return ["US"];
        yield return ["USDT"];
        yield return ["U1D"];
        yield return ["$$$"];
    }

    [Theory]
    [MemberData(nameof(ValidCurrencyCodes))]
    public void From_WithValidCode_ShouldNormalizeAndCreateCurrency(string code, string expectedCode)
    {
        // Arrange

        // Act
        var currency = Currency.From(code);

        // Assert
        Assert.Equal(expectedCode, currency.Code);
        Assert.Equal(expectedCode, currency.ToString());
    }

    [Theory]
    [MemberData(nameof(InvalidCurrencyCodes))]
    public void From_WithInvalidCode_ShouldThrowArgumentException(string? code)
    {
        // Arrange

        // Act
        var action = () => Currency.From(code!);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Equality_WithSameCode_ShouldBeEqual()
    {
        // Arrange
        var left = Currency.From("usd");
        var right = Currency.From("USD");

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentCode_ShouldNotBeEqual()
    {
        // Arrange
        var left = Currency.From("USD");
        var right = Currency.From("TRY");

        // Act
        var areNotEqual = left != right;

        // Assert
        Assert.True(areNotEqual);
        Assert.NotEqual(left, right);
    }
}