using MediatR;
using Quantira.Application.Alerts.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Application.Alerts.Queries.GetUserAlerts;

/// <summary>
/// Query to retrieve all alerts belonging to the authenticated user,
/// optionally filtered by type. Used to populate the alerts management
/// screen in the Quantira UI.
/// </summary>
/// <param name="UserId">The user whose alerts are requested.</param>
/// <param name="AlertType">Optional type filter.</param>
public sealed record GetUserAlertsQuery(
    Guid UserId,
    AlertType? AlertType = null
) : IRequest<IReadOnlyList<AlertDto>>;