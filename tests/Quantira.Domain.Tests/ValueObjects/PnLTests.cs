using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Tests.ValueObjects;

public sealed class PnLTests
{
    [Fact]
    public void Of_WithSameCurrency_ShouldCreatePnL()
    {
        // Arrange
        var realized = Money.Of(100m, Currency.USD);
        var unrealized = Money.Of(50m, Currency.USD);

        // Act
        var pnl = PnL.Of(realized, unrealized);

        // Assert
        Assert.Equal(realized, pnl.Realized);
        Assert.Equal(unrealized, pnl.Unrealized);
        Assert.Equal(Currency.USD, pnl.Currency);
        Assert.Equal(150m, pnl.Total.Amount);
    }

    [Fact]
    public void Of_WithDifferentCurrencies_ShouldThrowArgumentException()
    {
        // Arrange
        var realized = Money.Of(100m, Currency.USD);
        var unrealized = Money.Of(50m, Currency.TRY);

        // Act
        var action = () => PnL.Of(realized, unrealized);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void Zero_ShouldReturnZeroPnLForCurrency()
    {
        // Arrange

        // Act
        var pnl = PnL.Zero(Currency.EUR);

        // Assert
        Assert.Equal(0m, pnl.Realized.Amount);
        Assert.Equal(0m, pnl.Unrealized.Amount);
        Assert.Equal(0m, pnl.Total.Amount);
        Assert.Equal(Currency.EUR, pnl.Currency);
    }

    [Fact]
    public void TotalPercentage_WithZeroCost_ShouldReturnZero()
    {
        // Arrange
        var pnl = PnL.Of(Money.Of(100m, Currency.USD), Money.Of(50m, Currency.USD));
        var totalCost = Money.Zero(Currency.USD);

        // Act
        var percentage = pnl.TotalPercentage(totalCost);

        // Assert
        Assert.Equal(0m, percentage);
    }

    [Fact]
    public void TotalPercentage_WithNonZeroCost_ShouldReturnExpectedPercentage()
    {
        // Arrange
        var pnl = PnL.Of(Money.Of(100m, Currency.USD), Money.Of(50m, Currency.USD));
        var totalCost = Money.Of(300m, Currency.USD);

        // Act
        var percentage = pnl.TotalPercentage(totalCost);

        // Assert
        Assert.Equal(50m, percentage);
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var left = PnL.Of(Money.Of(100m, Currency.USD), Money.Of(50m, Currency.USD));
        var right = PnL.Of(Money.Of(100m, Currency.USD), Money.Of(50m, Currency.USD));

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldContainRealizedUnrealizedAndTotalValues()
    {
        // Arrange
        var pnl = PnL.Of(Money.Of(10m, Currency.USD), Money.Of(5m, Currency.USD));

        // Act
        var text = pnl.ToString();

        // Assert
        Assert.Contains("Realized:", text, StringComparison.Ordinal);
        Assert.Contains("Unrealized:", text, StringComparison.Ordinal);
        Assert.Contains("Total:", text, StringComparison.Ordinal);
    }
}