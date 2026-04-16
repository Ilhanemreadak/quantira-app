using Quantira.Domain.Exceptions;
using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Tests.ValueObjects;

public sealed class MoneyTests
{
    public static IEnumerable<object[]> AddSubtractCases()
    {
        yield return [10m, 5m, 15m, 5m];
        yield return [100.25m, 20.10m, 120.35m, 80.15m];
    }

    [Fact]
    public void Of_WithValidAmount_ShouldCreateMoney()
    {
        // Arrange
        var currency = Currency.USD;

        // Act
        var money = Money.Of(10.5m, currency);

        // Assert
        Assert.Equal(10.5m, money.Amount);
        Assert.Equal(currency, money.Currency);
    }

    [Fact]
    public void Of_WithNegativeAmount_ShouldThrowDomainException()
    {
        // Arrange
        var currency = Currency.USD;

        // Act
        var action = () => Money.Of(-1m, currency);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Zero_ShouldReturnZeroMoneyForCurrency()
    {
        // Arrange

        // Act
        var money = Money.Zero(Currency.TRY);

        // Assert
        Assert.Equal(0m, money.Amount);
        Assert.Equal(Currency.TRY, money.Currency);
        Assert.True(money.IsZero);
    }

    [Theory]
    [MemberData(nameof(AddSubtractCases))]
    public void Add_And_Subtract_WithSameCurrency_ShouldReturnExpectedResults(decimal leftAmount, decimal rightAmount, decimal expectedAdd, decimal expectedSubtract)
    {
        // Arrange
        var left = Money.Of(leftAmount, Currency.USD);
        var right = Money.Of(rightAmount, Currency.USD);

        // Act
        var added = left.Add(right);
        var subtracted = left.Subtract(right);

        // Assert
        Assert.Equal(expectedAdd, added.Amount);
        Assert.Equal(expectedSubtract, subtracted.Amount);
        Assert.Equal(Currency.USD, added.Currency);
        Assert.Equal(Currency.USD, subtracted.Currency);
    }

    [Fact]
    public void Add_WithDifferentCurrencies_ShouldThrowDomainException()
    {
        // Arrange
        var usd = Money.Of(10m, Currency.USD);
        var tryMoney = Money.Of(10m, Currency.TRY);

        // Act
        var action = () => usd.Add(tryMoney);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Subtract_WithDifferentCurrencies_ShouldThrowDomainException()
    {
        // Arrange
        var usd = Money.Of(10m, Currency.USD);
        var tryMoney = Money.Of(10m, Currency.TRY);

        // Act
        var action = () => usd.Subtract(tryMoney);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Multiply_ShouldScaleAmount()
    {
        // Arrange
        var money = Money.Of(10m, Currency.USD);

        // Act
        var multiplied = money.Multiply(2.5m);

        // Assert
        Assert.Equal(25m, multiplied.Amount);
        Assert.Equal(Currency.USD, multiplied.Currency);
    }

    [Fact]
    public void ConvertTo_WithValidExchangeRate_ShouldConvertAmountAndCurrency()
    {
        // Arrange
        var usd = Money.Of(100m, Currency.USD);

        // Act
        var converted = usd.ConvertTo(Currency.TRY, 32m);

        // Assert
        Assert.Equal(3200m, converted.Amount);
        Assert.Equal(Currency.TRY, converted.Currency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConvertTo_WithNonPositiveExchangeRate_ShouldThrowDomainException(decimal exchangeRate)
    {
        // Arrange
        var usd = Money.Of(100m, Currency.USD);

        // Act
        var action = () => usd.ConvertTo(Currency.TRY, exchangeRate);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Equality_WithSameAmountAndCurrency_ShouldBeEqual()
    {
        // Arrange
        var left = Money.Of(10m, Currency.USD);
        var right = Money.Of(10m, Currency.USD);

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldUseExpectedFormat()
    {
        // Arrange
        var money = Money.Of(10.5m, Currency.USD);
        var expectedAmountText = 10.5m.ToString("F4");

        // Act
        var text = money.ToString();

        // Assert
        Assert.Equal($"{expectedAmountText} USD", text);
    }
}