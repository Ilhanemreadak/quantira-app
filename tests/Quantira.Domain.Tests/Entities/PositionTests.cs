using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Tests.Entities;

public sealed class PositionTests
{
    public static IEnumerable<object[]> CreateCases()
    {
        yield return ["USD"];
        yield return ["  try  "];
    }

    public static IEnumerable<object[]> BuyTradeCases()
    {
        yield return [CostMethod.Average, 2m, 100m, 1m, 2m, 100.5m, 201m];
        yield return [CostMethod.Fifo, 3m, 50m, 0m, 3m, 50m, 150m];
        yield return [CostMethod.Lifo, 1.5m, 80m, 0.5m, 1.5m, 80.33333333333333333333333333m, 120.5m];
    }

    [Theory]
    [MemberData(nameof(CreateCases))]
    public void Create_WithValidParameters_ShouldInitializeZeroState(string currency)
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var beforeCreate = DateTime.UtcNow;

        // Act
        var position = Position.Create(portfolioId, assetId, currency);
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, position.Id);
        Assert.Equal(portfolioId, position.PortfolioId);
        Assert.Equal(assetId, position.AssetId);
        Assert.Equal(0m, position.Quantity);
        Assert.Equal(0m, position.AvgCostPrice.Amount);
        Assert.Equal(currency.Trim().ToUpperInvariant(), position.AvgCostPrice.Currency.Code);
        Assert.Equal(0m, position.TotalCost.Amount);
        Assert.Equal(currency.Trim().ToUpperInvariant(), position.TotalCost.Currency.Code);
        Assert.Null(position.CurrentValue);
        Assert.Null(position.UnrealizedPnL);
        Assert.Null(position.UnrealizedPnLPct);
        Assert.InRange(position.LastUpdated, beforeCreate, afterCreate);
    }

    [Theory]
    [MemberData(nameof(BuyTradeCases))]
    public void ApplyTrade_WithBuyTrade_ShouldUpdateQuantityAndCostBasis(
        CostMethod costMethod,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal expectedQuantity,
        decimal expectedAvgCost,
        decimal expectedTotalCost)
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        var trade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, quantity, price, "USD", commission, 0m);
        var previousLastUpdated = position.LastUpdated;

        // Act
        position.ApplyTrade(trade, costMethod);

        // Assert
        Assert.Equal(expectedQuantity, position.Quantity);
        Assert.Equal(expectedAvgCost, position.AvgCostPrice.Amount);
        Assert.Equal(expectedTotalCost, position.TotalCost.Amount);
        Assert.True(position.LastUpdated >= previousLastUpdated);
    }

    [Fact]
    public void ApplyTrade_WithTransferIn_ShouldBehaveLikeBuy()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        var trade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.TransferIn, 4m, 25m, "USD");

        // Act
        position.ApplyTrade(trade, CostMethod.Average);

        // Assert
        Assert.Equal(4m, position.Quantity);
        Assert.Equal(25m, position.AvgCostPrice.Amount);
        Assert.Equal(100m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithSellTrade_ShouldDecreaseQuantityAndRecalculateTotalCost()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        var buyTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 5m, 20m, "USD");
        position.ApplyTrade(buyTrade, CostMethod.Average);
        var sellTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Sell, 2m, 30m, "USD");

        // Act
        position.ApplyTrade(sellTrade, CostMethod.Average);

        // Assert
        Assert.Equal(3m, position.Quantity);
        Assert.Equal(20m, position.AvgCostPrice.Amount);
        Assert.Equal(60m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithSellTradeClosingPosition_ShouldSetTotalCostToZero()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 5m, 20m, "USD"), CostMethod.Average);
        var closingSellTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Sell, 5m, 30m, "USD");

        // Act
        position.ApplyTrade(closingSellTrade, CostMethod.Average);

        // Assert
        Assert.Equal(0m, position.Quantity);
        Assert.Equal(0m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithSecondBuyAndAverageCostMethod_ShouldUseAverageBranchWithExistingQuantity()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 2m, 100m, "USD"), CostMethod.Average);
        var secondBuyTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 2m, 200m, "USD");

        // Act
        position.ApplyTrade(secondBuyTrade, CostMethod.Average);

        // Assert
        Assert.Equal(4m, position.Quantity);
        Assert.Equal(150m, position.AvgCostPrice.Amount);
        Assert.Equal(600m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithSecondBuyAndFifoMethod_ShouldUseNonAverageBranchWithExistingQuantity()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 2m, 100m, "USD"), CostMethod.Fifo);
        var secondBuyTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 3m, 200m, "USD");

        // Act
        position.ApplyTrade(secondBuyTrade, CostMethod.Fifo);

        // Assert
        Assert.Equal(5m, position.Quantity);
        Assert.Equal(160m, position.AvgCostPrice.Amount);
        Assert.Equal(800m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithTransferOut_ShouldBehaveLikeSell()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 3m, 10m, "USD"), CostMethod.Average);
        var transferOutTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.TransferOut, 1m, 10m, "USD");

        // Act
        position.ApplyTrade(transferOutTrade, CostMethod.Average);

        // Assert
        Assert.Equal(2m, position.Quantity);
        Assert.Equal(20m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithSellGreaterThanQuantity_ShouldThrowDomainException()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 1m, 10m, "USD"), CostMethod.Average);
        var sellTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Sell, 2m, 10m, "USD");

        // Act
        var action = () => position.ApplyTrade(sellTrade, CostMethod.Average);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("Cannot sell", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTrade_WithSplitTrade_ShouldAdjustQuantityAndAvgCostWithoutChangingTotalCost()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 10m, 20m, "USD"), CostMethod.Average);
        var splitTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Split, 2m, 1m, "USD");

        // Act
        position.ApplyTrade(splitTrade, CostMethod.Average);

        // Assert
        Assert.Equal(20m, position.Quantity);
        Assert.Equal(10m, position.AvgCostPrice.Amount);
        Assert.Equal(200m, position.TotalCost.Amount);
    }

    [Fact]
    public void ApplyTrade_WithInvalidSplitDenominator_ShouldThrowDomainException()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 10m, 20m, "USD"), CostMethod.Average);
        var invalidSplitTrade = Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Split, 2m, 0m, "USD");

        // Act
        var action = () => position.ApplyTrade(invalidSplitTrade, CostMethod.Average);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Split ratio denominator must be positive.", exception.Message);
    }

    [Fact]
    public void ApplyTrade_WithDividend_ShouldNotChangeQuantityOrCost()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 2m, 50m, "USD"), CostMethod.Average);
        var previousQuantity = position.Quantity;
        var previousAvgCost = position.AvgCostPrice.Amount;
        var previousTotalCost = position.TotalCost.Amount;

        // Act
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Dividend, 1m, 5m, "USD"), CostMethod.Average);

        // Assert
        Assert.Equal(previousQuantity, position.Quantity);
        Assert.Equal(previousAvgCost, position.AvgCostPrice.Amount);
        Assert.Equal(previousTotalCost, position.TotalCost.Amount);
    }

    [Fact]
    public void UpdateMarketValue_WithNonZeroTotalCost_ShouldCalculateCurrentValueAndUnrealizedPnL()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");
        position.ApplyTrade(Trade.Create(Guid.NewGuid(), Guid.NewGuid(), TradeType.Buy, 2m, 50m, "USD"), CostMethod.Average);

        // Act
        position.UpdateMarketValue(60m, "USD");

        // Assert
        Assert.NotNull(position.CurrentValue);
        Assert.NotNull(position.UnrealizedPnL);
        Assert.NotNull(position.UnrealizedPnLPct);
        Assert.Equal(120m, position.CurrentValue!.Amount);
        Assert.Equal(20m, position.UnrealizedPnL!.Amount);
        Assert.Equal(20m, position.UnrealizedPnLPct!.Value);
    }

    [Fact]
    public void UpdateMarketValue_WhenTotalCostIsZero_ShouldSetUnrealizedPnLPctToZero()
    {
        // Arrange
        var position = Position.Create(Guid.NewGuid(), Guid.NewGuid(), "USD");

        // Act
        position.UpdateMarketValue(100m, "USD");

        // Assert
        Assert.NotNull(position.CurrentValue);
        Assert.NotNull(position.UnrealizedPnL);
        Assert.NotNull(position.UnrealizedPnLPct);
        Assert.Equal(0m, position.CurrentValue!.Amount);
        Assert.Equal(0m, position.UnrealizedPnL!.Amount);
        Assert.Equal(0m, position.UnrealizedPnLPct!.Value);
    }
}