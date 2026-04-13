using MediatR;
using Quantira.Application.Common.Behaviors;

namespace Quantira.Application.Alerts.Commands.DeleteAlert;

/// <summary>
/// Command to soft-delete an alert. Once deleted the alert is excluded
/// from <c>AlertCheckJob</c> evaluation and no longer appears in the
/// user's alert list.
/// </summary>
/// <param name="AlertId">The alert to delete.</param>
/// <param name="UserId">Verified against alert ownership in the handler.</param>
public sealed record DeleteAlertCommand(
    Guid AlertId,
    Guid UserId
) : IRequest, ITransactionalCommand;