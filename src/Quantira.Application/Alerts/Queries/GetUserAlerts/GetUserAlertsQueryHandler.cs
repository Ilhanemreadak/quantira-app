using MediatR;
using Mapster;
using Quantira.Application.Alerts.DTOs;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Alerts.Queries.GetUserAlerts;

/// <summary>
/// Handles <see cref="GetUserAlertsQuery"/>.
/// Fetches and projects alerts for the given user.
/// </summary>
public sealed class GetUserAlertsQueryHandler
    : IRequestHandler<GetUserAlertsQuery, IReadOnlyList<AlertDto>>
{
    private readonly IAlertRepository _alertRepository;

    public GetUserAlertsQueryHandler(IAlertRepository alertRepository)
        => _alertRepository = alertRepository;

    public async Task<IReadOnlyList<AlertDto>> Handle(
        GetUserAlertsQuery query,
        CancellationToken cancellationToken)
    {
        var alerts = await _alertRepository.GetByUserIdAsync(
            userId: query.UserId,
            alertType: query.AlertType,
            cancellationToken: cancellationToken);

        return alerts
            .Select(a => a.Adapt<AlertDto>())
            .ToList()
            .AsReadOnly();
    }
}