using Quantira.Domain.Enums;
using Quantira.Domain.Events;

namespace Quantira.Domain.Tests.Events;

public sealed class DomainEventsTests
{
    [Fact]
    public void AssetPriceUpdatedEvent_ShouldMapPayloadAndComputeChanges()
    {
        // Arrange
        var assetId = Guid.NewGuid();

        // Act
        var domainEvent = new AssetPriceUpdatedEvent(assetId, "BTC", 120m, 100m, "USD");

        // Assert
        Assert.Equal(assetId, domainEvent.AssetId);
        Assert.Equal("BTC", domainEvent.Symbol);
        Assert.Equal(120m, domainEvent.NewPrice);
        Assert.Equal(100m, domainEvent.PreviousPrice);
        Assert.Equal("USD", domainEvent.Currency);
        Assert.Equal(20m, domainEvent.Change);
        Assert.Equal(20m, domainEvent.ChangePercentage);
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
    }

    [Fact]
    public void AssetPriceUpdatedEvent_WithZeroPreviousPrice_ShouldReturnZeroChangePercentage()
    {
        // Arrange

        // Act
        var domainEvent = new AssetPriceUpdatedEvent(Guid.NewGuid(), "BTC", 120m, 0m, "USD");

        // Assert
        Assert.Equal(0m, domainEvent.ChangePercentage);
    }

    [Fact]
    public void PortfolioCreatedEvent_ShouldMapPayload()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var domainEvent = new PortfolioCreatedEvent(portfolioId, userId, "Main");

        // Assert
        Assert.Equal(portfolioId, domainEvent.PortfolioId);
        Assert.Equal(userId, domainEvent.UserId);
        Assert.Equal("Main", domainEvent.PortfolioName);
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
    }

    [Fact]
    public void TradeAddedEvent_ShouldMapPayload()
    {
        // Arrange
        var portfolioId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var tradeId = Guid.NewGuid();

        // Act
        var domainEvent = new TradeAddedEvent(portfolioId, assetId, tradeId, TradeType.Buy, 2m, 100m, "USD");

        // Assert
        Assert.Equal(portfolioId, domainEvent.PortfolioId);
        Assert.Equal(assetId, domainEvent.AssetId);
        Assert.Equal(tradeId, domainEvent.TradeId);
        Assert.Equal(TradeType.Buy, domainEvent.TradeType);
        Assert.Equal(2m, domainEvent.Quantity);
        Assert.Equal(100m, domainEvent.Price);
        Assert.Equal("USD", domainEvent.PriceCurrency);
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
    }

    [Fact]
    public void AlertTriggeredEvent_ShouldMapPayload()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        var domainEvent = new AlertTriggeredEvent(alertId, userId, assetId, AlertType.PriceAbove, 185m);

        // Assert
        Assert.Equal(alertId, domainEvent.AlertId);
        Assert.Equal(userId, domainEvent.UserId);
        Assert.Equal(assetId, domainEvent.AssetId);
        Assert.Equal(AlertType.PriceAbove, domainEvent.AlertType);
        Assert.Equal(185m, domainEvent.TriggerValue);
        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
    }
}