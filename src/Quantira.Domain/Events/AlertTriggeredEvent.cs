using Quantira.Domain.Common;
using Quantira.Domain.Enums;

namespace Quantira.Domain.Events;

/// <summary>
/// Raised when <c>AlertCheckJob</c> determines that an alert's condition
/// has been met. Triggers the notification pipeline in the application layer:
/// push notification, email, or SMS depending on the user's preferences.
/// Also updates the alert's status to <c>Triggered</c> and records
/// the trigger timestamp for audit and display purposes.
/// </summary>
public sealed class AlertTriggeredEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <summary>The alert that was triggered.</summary>
    public Guid AlertId { get; }

    /// <summary>The user who will receive the notification.</summary>
    public Guid UserId { get; }

    /// <summary>The asset whose price or indicator triggered the alert.</summary>
    public Guid AssetId { get; }

    /// <summary>The type of condition that was met.</summary>
    public AlertType AlertType { get; }

    /// <summary>
    /// The market value that caused the alert to fire.
    /// For price alerts this is the current price;
    /// for indicator alerts this is the indicator value.
    /// </summary>
    public decimal TriggerValue { get; }

    public AlertTriggeredEvent(
        Guid alertId,
        Guid userId,
        Guid assetId,
        AlertType alertType,
        decimal triggerValue)
    {
        AlertId = alertId;
        UserId = userId;
        AssetId = assetId;
        AlertType = alertType;
        TriggerValue = triggerValue;
    }
}