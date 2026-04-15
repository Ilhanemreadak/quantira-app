using MediatR;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Alerts.Commands.DeleteAlert;

/// <summary>
/// Handles <see cref="DeleteAlertCommand"/>.
/// Verifies ownership and soft-deletes the alert via the aggregate.
/// </summary>
public sealed class DeleteAlertCommandHandler : IRequestHandler<DeleteAlertCommand>
{
    private readonly IAlertRepository _alertRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAlertCommandHandler(
        IAlertRepository alertRepository,
        IUnitOfWork unitOfWork)
    {
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        DeleteAlertCommand command,
        CancellationToken cancellationToken)
    {
        var alert = await _alertRepository
            .GetByIdAsync(command.AlertId, cancellationToken)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Alert), command.AlertId);

        if (alert.UserId != command.UserId)
            throw new DomainException(
                "You do not have permission to delete this alert.");

        alert.Expire();
        _alertRepository.Update(alert);
    }
}