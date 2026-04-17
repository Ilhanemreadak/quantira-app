using MediatR;
using Quantira.Domain.Entities;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Alerts.Commands.CreateAlert;

/// <summary>
/// Handles <see cref="CreateAlertCommand"/>.
/// Verifies the target asset exists, creates the alert aggregate,
/// and persists it. The alert is immediately available to
/// <c>AlertCheckJob</c> on its next evaluation cycle.
/// </summary>
public sealed class CreateAlertCommandHandler
    : IRequestHandler<CreateAlertCommand, Guid>
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAssetRepository _assetRepository;

    public CreateAlertCommandHandler(
        IAlertRepository alertRepository,
        IAssetRepository assetRepository)
    {
        _alertRepository = alertRepository;
        _assetRepository = assetRepository;
    }

    public async Task<Guid> Handle(
        CreateAlertCommand command,
        CancellationToken cancellationToken)
    {
        var assetExists = await _assetRepository
            .GetByIdAsync(command.AssetId, cancellationToken);

        if (assetExists is null)
            throw new NotFoundException(nameof(Asset), command.AssetId);

        var alert = Alert.Create(
            userId: command.UserId,
            assetId: command.AssetId,
            alertType: command.AlertType,
            conditionJson: command.ConditionJson,
            expiresAt: command.ExpiresAt);

        await _alertRepository.AddAsync(alert, cancellationToken);

        return alert.Id;
    }
}