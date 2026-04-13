using Quantira.Domain.Enums;

namespace Quantira.Application.Alerts.DTOs;

/// <summary>
/// Read model for an alert. Returned by alert queries and used
/// to populate the alert management screen and notification previews.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="AssetId">The monitored asset.</param>
/// <param name="AlertType">The type of condition being watched.</param>
/// <param name="ConditionJson">JSON-encoded condition parameters.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="TriggeredAt">UTC timestamp of last trigger. Null if never fired.</param>
/// <param name="ExpiresAt">UTC expiry timestamp. Null if no expiry.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
public sealed record AlertDto(
    Guid Id,
    Guid AssetId,
    AlertType AlertType,
    string ConditionJson,
    string Status,
    DateTime? TriggeredAt,
    DateTime? ExpiresAt,
    DateTime CreatedAt);