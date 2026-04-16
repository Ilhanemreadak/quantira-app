using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Events;
using Quantira.Domain.Exceptions;
using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Tests.Entities;

public sealed class PortfolioTests
{
    public static IEnumerable<object?[]> ValidCreateCases()
    {
        yield return ["  Growth Portfolio  ", Currency.USD, CostMethod.Fifo, "  Tech heavy  ", true, "Growth Portfolio", "Tech heavy"];
        yield return ["  BIST Uzun Vade  ", Currency.TRY, CostMethod.Average, null, false, "BIST Uzun Vade", null];
    }

    public static IEnumerable<object?[]> InvalidNameCases()
    {
        yield return [null];
        yield return [""];
        yield return ["   "];
        yield return ["\t"];
    }

    [Theory]
    [MemberData(nameof(ValidCreateCases))]
    public void Create_WithValidParameters_ShouldReturnActivePortfolioAndRaiseCreatedEvent(
        string name,
        Currency baseCurrency,
        CostMethod costMethod,
        string? description,
        bool isDefault,
        string expectedName,
        string? expectedDescription)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var beforeCreate = DateTime.UtcNow;

        // Act
        var portfolio = Portfolio.Create(userId, name, baseCurrency, costMethod, description, isDefault);
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, portfolio.Id);
        Assert.Equal(userId, portfolio.UserId);
        Assert.Equal(expectedName, portfolio.Name);
        Assert.Equal(expectedDescription, portfolio.Description);
        Assert.Equal(baseCurrency, portfolio.BaseCurrency);
        Assert.Equal(costMethod, portfolio.CostMethod);
        Assert.Equal(isDefault, portfolio.IsDefault);
        Assert.True(portfolio.IsActive);
        Assert.Empty(portfolio.Trades);
        Assert.Empty(portfolio.Positions);
        Assert.InRange(portfolio.CreatedAt, beforeCreate, afterCreate);
        Assert.InRange(portfolio.UpdatedAt, beforeCreate, afterCreate);
        Assert.False(portfolio.IsDeleted);

        var createdEvent = Assert.IsType<PortfolioCreatedEvent>(Assert.Single(portfolio.DomainEvents));
        Assert.Equal(portfolio.Id, createdEvent.PortfolioId);
        Assert.Equal(userId, createdEvent.UserId);
        Assert.Equal(name, createdEvent.PortfolioName);
    }

    [Theory]
    [MemberData(nameof(InvalidNameCases))]
    public void Create_WithInvalidName_ShouldThrowDomainException(string? name)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var action = () => Portfolio.Create(userId, name!, Currency.USD);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Portfolio name cannot be empty.", exception.Message);
    }

    [Fact]
    public void AddTrade_WithValidBuyTrade_ShouldCreateTradePositionAndRaiseEvent()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);
        portfolio.ClearDomainEvents();
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");

        // Act
        var trade = portfolio.AddTrade(asset, TradeType.Buy, 2m, 100m, "USD", 1m, 0.5m, DateTime.UtcNow, "  first buy  ");

        // Assert
        Assert.Single(portfolio.Trades);
        Assert.Equal(trade.Id, portfolio.Trades.Single().Id);

        var position = Assert.Single(portfolio.Positions);
        Assert.Equal(asset.Id, position.AssetId);
        Assert.Equal(2m, position.Quantity);
        Assert.Equal(100.5m, position.AvgCostPrice.Amount);
        Assert.Equal(201m, position.TotalCost.Amount);

        var tradeAddedEvent = Assert.IsType<TradeAddedEvent>(Assert.Single(portfolio.DomainEvents));
        Assert.Equal(portfolio.Id, tradeAddedEvent.PortfolioId);
        Assert.Equal(asset.Id, tradeAddedEvent.AssetId);
        Assert.Equal(trade.Id, tradeAddedEvent.TradeId);
        Assert.Equal(TradeType.Buy, tradeAddedEvent.TradeType);
        Assert.Equal(2m, tradeAddedEvent.Quantity);
        Assert.Equal(100m, tradeAddedEvent.Price);
        Assert.Equal("USD", tradeAddedEvent.PriceCurrency);
    }

    [Fact]
    public void AddTrade_WithSecondTradeOnSameAsset_ShouldUpdateExistingPositionOnly()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD, CostMethod.Average);
        var asset = Asset.Create("ETH", "Ethereum", AssetType.Crypto, "USD", "BINANCE", null, "ETHUSDT");
        portfolio.AddTrade(asset, TradeType.Buy, 1m, 100m, "USD");

        // Act
        portfolio.AddTrade(asset, TradeType.Buy, 1m, 200m, "USD");

        // Assert
        Assert.Equal(2, portfolio.Trades.Count);
        var position = Assert.Single(portfolio.Positions);
        Assert.Equal(2m, position.Quantity);
        Assert.Equal(150m, position.AvgCostPrice.Amount);
        Assert.Equal(300m, position.TotalCost.Amount);
    }

    [Fact]
    public void AddTrade_WhenPortfolioIsDeleted_ShouldThrowDomainException()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");
        portfolio.Delete();

        // Act
        var action = () => portfolio.AddTrade(asset, TradeType.Buy, 1m, 10m, "USD");

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Cannot add a trade to inactive portfolio 'Main'.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddTrade_WithNonPositiveQuantity_ShouldThrowDomainException(decimal quantity)
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");

        // Act
        var action = () => portfolio.AddTrade(asset, TradeType.Buy, quantity, 10m, "USD");

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Contains("Trade quantity must be positive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddTrade_WithNegativePrice_ShouldThrowDomainException()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);
        var asset = Asset.Create("BTC", "Bitcoin", AssetType.Crypto, "USD", "BINANCE", null, "BTCUSDT");

        // Act
        var action = () => portfolio.AddTrade(asset, TradeType.Buy, 1m, -1m, "USD");

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Trade price cannot be negative. Received: -1", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidNameCases))]
    public void Rename_WithInvalidName_ShouldThrowDomainException(string? newName)
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);

        // Act
        var action = () => portfolio.Rename(newName!);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Portfolio name cannot be empty.", exception.Message);
    }

    [Fact]
    public void Rename_WithValidName_ShouldTrimAndUpdateTimestamp()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);
        var previousUpdatedAt = portfolio.UpdatedAt;

        // Act
        portfolio.Rename("  New Main  ");

        // Assert
        Assert.Equal("New Main", portfolio.Name);
        Assert.True(portfolio.UpdatedAt >= previousUpdatedAt);
    }

    [Fact]
    public void SetAsDefault_And_UnsetDefault_ShouldToggleDefaultFlag()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD, isDefault: false);

        // Act
        portfolio.SetAsDefault();
        portfolio.UnsetDefault();

        // Assert
        Assert.False(portfolio.IsDefault);
    }

    [Fact]
    public void Delete_WhenActive_ShouldMarkInactiveAndSoftDeleted()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);

        // Act
        portfolio.Delete();

        // Assert
        Assert.False(portfolio.IsActive);
        Assert.True(portfolio.IsDeleted);
        Assert.NotNull(portfolio.DeletedAt);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ShouldThrowDomainException()
    {
        // Arrange
        var portfolio = Portfolio.Create(Guid.NewGuid(), "Main", Currency.USD);
        portfolio.Delete();

        // Act
        var action = portfolio.Delete;

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Portfolio 'Main' is already deleted.", exception.Message);
    }
}
