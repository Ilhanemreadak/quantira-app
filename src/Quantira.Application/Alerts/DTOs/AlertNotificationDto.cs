using Quantira.Domain.Enums;

namespace Quantira.Application.Alerts.DTOs;

/// <summary>
/// Carries the data needed to render an alert-triggered notification
/// across all delivery channels (email, push, SMS).
/// Built by the <c>AlertTriggeredEvent</c> handler and passed to
/// <see cref="INotificationService.SendAlertTriggeredAsync"/>.
/// </summary>
/// <param name="AlertId">The unique identifier of the triggered alert.</param>
/// <param name="AssetId">The asset whose price or indicator triggered the alert.</param>
/// <param name="AssetSymbol">Human-readable symbol for notification body text.</param>
/// <param name="AlertType">The type of condition that was satisfied.</param>
/// <param name="ConditionJson">Original condition JSON for rendering details.</param>
/// <param name="TriggerValue">
/// The market value that caused the alert to fire
/// (price, indicator value, or loss percentage).
/// </param>
/// <param name="TriggeredAt">UTC timestamp of when the alert fired.</param>
public sealed record AlertNotificationDto(
    Guid AlertId,
    Guid AssetId,
    string AssetSymbol,
    AlertType AlertType,
    string ConditionJson,
    decimal TriggerValue,
    DateTime TriggeredAt);
