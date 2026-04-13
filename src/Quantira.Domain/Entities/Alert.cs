using Quantira.Domain.Common;
using Quantira.Domain.Enums;
using Quantira.Domain.Events;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Entities;

/// <summary>
/// Represents a user-defined condition that triggers a notification
/// when a market threshold is crossed. Evaluated every 30 seconds
/// by <c>AlertCheckJob</c> against live market data from Redis.
/// The triggering condition is stored as a JSON payload in
/// <see cref="ConditionJson"/> to support extensible alert types
/// without schema changes.
/// </summary>
public sealed class Alert : AggregateRoot<Guid>
{
    /// <summary>The user who created this alert.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The asset being watched.</summary>
    public Guid AssetId { get; private set; }

    /// <summary>The type of condition being monitored.</summary>
    public AlertType AlertType { get; private set; }

    /// <summary>
    /// JSON-encoded condition parameters specific to the <see cref="AlertType"/>.
    /// Examples:
    /// PriceAbove:      <c>{ "threshold": 185.0, "currency": "USD" }</c>
    /// IndicatorSignal: <c>{ "indicator": "RSI", "operator": "lt", "value": 30 }</c>
    /// PortfolioLoss:   <c>{ "lossPercentage": 3.0 }</c>
    /// </summary>
    public string ConditionJson { get; private set; } = default!;

    /// <summary>
    /// Current lifecycle status of this alert.
    /// Transitions: Active → Triggered | Expired | Paused → Active (rearm).
    /// </summary>
    public string Status { get; private set; } = default!;

    /// <summary>
    /// UTC timestamp of when this alert was last triggered.
    /// <c>null</c> if the alert has never fired.
    /// </summary>
    public DateTime? TriggeredAt { get; private set; }

    /// <summary>
    /// Optional UTC expiry timestamp. After this time the alert
    /// transitions to <c>Expired</c> regardless of market conditions.
    /// <c>null</c> means the alert never expires automatically.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>Returns <c>true</c> if this alert is currently being evaluated.</summary>
    public bool IsActive => Status == AlertStatuses.Active;

    private Alert() { }

    /// <summary>
    /// Creates a new alert for the given user and asset.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="conditionJson"/> is null or empty,
    /// or when <paramref name="expiresAt"/> is in the past.
    /// </exception>
    public static Alert Create(
        Guid userId,
        Guid assetId,
        AlertType alertType,
        string conditionJson,
        DateTime? expiresAt = null)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
            throw new DomainException("Alert condition cannot be empty.");

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new DomainException("Alert expiry date must be in the future.");

        return new Alert
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AssetId = assetId,
            AlertType = alertType,
            ConditionJson = conditionJson.Trim(),
            Status = AlertStatuses.Active,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Marks this alert as triggered. Records the trigger timestamp
    /// and raises <see cref="AlertTriggeredEvent"/> for the notification pipeline.
    /// </summary>
    /// <param name="triggerValue">
    /// The market value that caused the alert to fire
    /// (price, indicator value, or loss percentage).
    /// </param>
    /// <exception cref="DomainException">
    /// Thrown when the alert is not in <c>Active</c> status.
    /// </exception>
    public void Trigger(decimal triggerValue)
    {
        if (!IsActive)
            throw new DomainException($"Cannot trigger an alert that is not active. Current status: {Status}");

        Status = AlertStatuses.Triggered;
        TriggeredAt = DateTime.UtcNow;
        MarkUpdated();

        AddDomainEvent(new AlertTriggeredEvent(Id, UserId, AssetId, AlertType, triggerValue));
    }

    /// <summary>
    /// Re-arms a triggered alert, returning it to <c>Active</c> status
    /// so it can fire again. Used for recurring alerts.
    /// </summary>
    public void Rearm()
    {
        if (Status != AlertStatuses.Triggered)
            throw new DomainException("Only triggered alerts can be re-armed.");

        Status = AlertStatuses.Active;
        MarkUpdated();
    }

    /// <summary>Pauses evaluation of this alert without deleting it.</summary>
    public void Pause()
    {
        if (!IsActive)
            throw new DomainException("Only active alerts can be paused.");

        Status = AlertStatuses.Paused;
        MarkUpdated();
    }

    /// <summary>Resumes a paused alert.</summary>
    public void Resume()
    {
        if (Status != AlertStatuses.Paused)
            throw new DomainException("Only paused alerts can be resumed.");

        Status = AlertStatuses.Active;
        MarkUpdated();
    }

    /// <summary>
    /// Expires this alert when its <see cref="ExpiresAt"/> time has passed.
    /// Called by <c>AlertCheckJob</c> during each evaluation cycle.
    /// </summary>
    public void Expire()
    {
        Status = AlertStatuses.Expired;
        MarkDeleted();
    }

    /// <summary>Status constant strings to avoid magic string usage.</summary>
    public static class AlertStatuses
    {
        public const string Active = "Active";
        public const string Triggered = "Triggered";
        public const string Expired = "Expired";
        public const string Paused = "Paused";
    }
}