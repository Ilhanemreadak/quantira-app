using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Events;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Tests.Entities;

public sealed class AlertTests
{
    public static IEnumerable<object?[]> ValidCreateCases()
    {
        yield return [AlertType.PriceAbove, "  { \"threshold\": 185.0, \"currency\": \"USD\" }  ", null, "{ \"threshold\": 185.0, \"currency\": \"USD\" }"];
        yield return [AlertType.IndicatorSignal, " { \"indicator\": \"RSI\", \"operator\": \"lt\", \"value\": 30 } ", DateTime.UtcNow.AddDays(1), "{ \"indicator\": \"RSI\", \"operator\": \"lt\", \"value\": 30 }"];
    }

    public static IEnumerable<object?[]> InvalidConditionCases()
    {
        yield return [null];
        yield return [""];
        yield return ["   "];
        yield return ["\t"];
    }

    [Theory]
    [MemberData(nameof(ValidCreateCases))]
    public void Create_WithValidParameters_ShouldReturnActiveAlert(
        AlertType alertType,
        string conditionJson,
        DateTime? expiresAt,
        string expectedConditionJson)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var beforeCreate = DateTime.UtcNow;

        // Act
        var alert = Alert.Create(userId, assetId, alertType, conditionJson, expiresAt);
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotEqual(Guid.Empty, alert.Id);
        Assert.Equal(userId, alert.UserId);
        Assert.Equal(assetId, alert.AssetId);
        Assert.Equal(alertType, alert.AlertType);
        Assert.Equal(expectedConditionJson, alert.ConditionJson);
        Assert.Equal(Alert.AlertStatuses.Active, alert.Status);
        Assert.True(alert.IsActive);
        Assert.Null(alert.TriggeredAt);
        Assert.Equal(expiresAt, alert.ExpiresAt);
        Assert.InRange(alert.CreatedAt, beforeCreate, afterCreate);
        Assert.InRange(alert.UpdatedAt, beforeCreate, afterCreate);
        Assert.Empty(alert.DomainEvents);
    }

    [Theory]
    [MemberData(nameof(InvalidConditionCases))]
    public void Create_WithInvalidConditionJson_ShouldThrowDomainException(string? conditionJson)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        // Act
        var action = () => Alert.Create(userId, assetId, AlertType.PriceAbove, conditionJson!);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Alert condition cannot be empty.", exception.Message);
    }

    [Fact]
    public void Create_WithPastExpiry_ShouldThrowDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddMinutes(-1);

        // Act
        var action = () => Alert.Create(userId, assetId, AlertType.PriceAbove, "{\"threshold\":100}", pastDate);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Alert expiry date must be in the future.", exception.Message);
    }

    [Fact]
    public void Trigger_WhenActive_ShouldSetTriggeredStatusAndRaiseDomainEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var alert = Alert.Create(userId, assetId, AlertType.PriceAbove, "{\"threshold\":185}");
        var previousUpdatedAt = alert.UpdatedAt;

        // Act
        alert.Trigger(187.45m);

        // Assert
        Assert.Equal(Alert.AlertStatuses.Triggered, alert.Status);
        Assert.False(alert.IsActive);
        Assert.NotNull(alert.TriggeredAt);
        Assert.True(alert.UpdatedAt >= previousUpdatedAt);

        var triggeredEvent = Assert.IsType<AlertTriggeredEvent>(Assert.Single(alert.DomainEvents));
        Assert.Equal(alert.Id, triggeredEvent.AlertId);
        Assert.Equal(userId, triggeredEvent.UserId);
        Assert.Equal(assetId, triggeredEvent.AssetId);
        Assert.Equal(AlertType.PriceAbove, triggeredEvent.AlertType);
        Assert.Equal(187.45m, triggeredEvent.TriggerValue);
    }

    [Fact]
    public void Trigger_WhenNotActive_ShouldThrowDomainException()
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.PriceAbove, "{\"threshold\":185}");
        alert.Pause();

        // Act
        var action = () => alert.Trigger(180m);

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Cannot trigger an alert that is not active. Current status: Paused", exception.Message);
    }

    [Fact]
    public void Rearm_WhenTriggered_ShouldSetStatusToActive()
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.PriceBelow, "{\"threshold\":100}");
        alert.Trigger(95m);
        var previousUpdatedAt = alert.UpdatedAt;

        // Act
        alert.Rearm();

        // Assert
        Assert.Equal(Alert.AlertStatuses.Active, alert.Status);
        Assert.True(alert.IsActive);
        Assert.True(alert.UpdatedAt >= previousUpdatedAt);
    }

    [Theory]
    [InlineData(Alert.AlertStatuses.Active)]
    [InlineData(Alert.AlertStatuses.Paused)]
    [InlineData(Alert.AlertStatuses.Expired)]
    public void Rearm_WhenStatusIsNotTriggered_ShouldThrowDomainException(string initialStatus)
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.PriceBelow, "{\"threshold\":100}");

        if (initialStatus == Alert.AlertStatuses.Paused)
            alert.Pause();

        if (initialStatus == Alert.AlertStatuses.Expired)
            alert.Expire();

        // Act
        var action = alert.Rearm;

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Only triggered alerts can be re-armed.", exception.Message);
    }

    [Fact]
    public void Pause_And_Resume_ShouldTransitionBetweenActiveAndPaused()
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.NewsSentiment, "{\"sentimentScore\":-0.6}");

        // Act
        alert.Pause();
        alert.Resume();

        // Assert
        Assert.Equal(Alert.AlertStatuses.Active, alert.Status);
        Assert.True(alert.IsActive);
    }

    [Fact]
    public void Pause_WhenNotActive_ShouldThrowDomainException()
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.NewsSentiment, "{\"sentimentScore\":-0.6}");
        alert.Trigger(-0.8m);

        // Act
        var action = alert.Pause;

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Only active alerts can be paused.", exception.Message);
    }

    [Fact]
    public void Resume_WhenNotPaused_ShouldThrowDomainException()
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.PriceAbove, "{\"threshold\":185}");

        // Act
        var action = alert.Resume;

        // Assert
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal("Only paused alerts can be resumed.", exception.Message);
    }

    [Fact]
    public void Expire_ShouldSetExpiredStatusAndSoftDelete()
    {
        // Arrange
        var alert = Alert.Create(Guid.NewGuid(), Guid.NewGuid(), AlertType.PortfolioLoss, "{\"lossPercentage\":3}");

        // Act
        alert.Expire();

        // Assert
        Assert.Equal(Alert.AlertStatuses.Expired, alert.Status);
        Assert.False(alert.IsActive);
        Assert.True(alert.IsDeleted);
        Assert.NotNull(alert.DeletedAt);
    }
}