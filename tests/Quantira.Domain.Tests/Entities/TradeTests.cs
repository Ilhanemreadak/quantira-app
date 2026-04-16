using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Tests.Entities;

public sealed class TradeTests
{
    public static IEnumerable<object?[]> ValidCreateCases()
    {
        yield return [TradeType.Buy, 2m, 100m, 1.5m, 0.5m, "  usd  ", "  first buy  ", 200m, 202m];
        yield return [TradeType.TransferIn, 3m, 10m, 0m, 0m, "  try  ", null, 30m, 30m];
        yield return [TradeType.Sell, 2m, 100m, 1.5m, 0.5m, "  usd  ", "  first sell  ", 200m, 198m];
        yield return [TradeType.TransferOut, 3m, 10m, 0m, 0m, "  try  ", null, 30m, 30m];
    }

    public static IEnumerable<object?[]> NonPositiveQuantityCases()
    {
        yield return [0m];
        yield return [-1m];
    }

    public static IEnumerable<object?[]> NegativeValueCases()
    {
        yield return [-0.01m];
        yield return [-1m];
    }

    [Theory]
    [MemberData(nameof(ValidCreateCases))]
    public void Create_WithValidParameters_ShouldCreateTradeAndCalculateValues(
        TradeType tradeType,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal taxAmount,
        string priceCurrency,
        string? notes,
        decimal expectedGross,
        decimal expectedNet)
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var tradedAt = new DateTime(2026, 1, 5, 13, 30, 0, DateTimeKind.Local);
        var beforeCreate = DateTime.UtcNow;

        // Act
        var trade = Trade.Create(portfolioId, assetId, tradeType, quantity, price, priceCurrency, commission, taxAmount, tradedAt, notes);
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, trade.Id);
        Assert.Equal(portfolioId, trade.PortfolioId);
        Assert.Equal(assetId, trade.AssetId);
        Assert.Equal(tradeType, trade.TradeType);
        Assert.Equal(quantity, trade.Quantity);
        Assert.Equal(price, trade.Price.Amount);
        Assert.Equal(commission, trade.Commission.Amount);
        Assert.Equal(taxAmount, trade.TaxAmount.Amount);
        Assert.Equal(priceCurrency.Trim().ToUpperInvariant(), trade.PriceCurrency);
        Assert.Equal(notes?.Trim(), trade.Notes);
        Assert.Equal(DateTimeKind.Utc, trade.TradedAt.Kind);
        Assert.Equal(DateTime.SpecifyKind(tradedAt, DateTimeKind.Utc), trade.TradedAt);
        Assert.InRange(trade.CreatedAt, beforeCreate, afterCreate);
        Assert.Equal(expectedGross, trade.GrossValue.Amount);
        Assert.Equal(expectedNet, trade.NetValue.Amount);
    }

    [Theory]
    [MemberData(nameof(NonPositiveQuantityCases))]
    public void Create_WithNonPositiveQuantity_ShouldThrowDomainException(decimal quantity)
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        var action = () => Trade.Create(portfolioId, assetId, TradeType.Buy, quantity, 1m, "USD");

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("Trade quantity must be positive", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(NegativeValueCases))]
    public void Create_WithNegativePrice_ShouldThrowDomainException(decimal price)
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        var action = () => Trade.Create(portfolioId, assetId, TradeType.Buy, 1m, price, "USD");

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("Trade price cannot be negative", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(NegativeValueCases))]
    public void Create_WithNegativeCommission_ShouldThrowDomainException(decimal commission)
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        var action = () => Trade.Create(portfolioId, assetId, TradeType.Buy, 1m, 10m, "USD", commission, 0m);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("Commission cannot be negative", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(NegativeValueCases))]
    public void Create_WithNegativeTaxAmount_ShouldThrowDomainException(decimal taxAmount)
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        var action = () => Trade.Create(portfolioId, assetId, TradeType.Buy, 1m, 10m, "USD", 0m, taxAmount);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("Tax amount cannot be negative", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithoutTradedAt_ShouldUseUtcNow()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var beforeCreate = DateTime.UtcNow;

        // Act
        var trade = Trade.Create(portfolioId, assetId, TradeType.Buy, 1m, 10m, "USD");
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.InRange(trade.TradedAt, beforeCreate, afterCreate);
        Assert.Equal(DateTimeKind.Utc, trade.TradedAt.Kind);
    }
}
