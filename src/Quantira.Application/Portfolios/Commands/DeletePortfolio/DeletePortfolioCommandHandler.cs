using MediatR;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Portfolios.Commands.DeletePortfolio;

/// <summary>
/// Handles <see cref="DeletePortfolioCommand"/>.
/// Verifies ownership, enforces the "cannot delete last portfolio"
/// business rule, and delegates soft-deletion to the aggregate.
/// </summary>
public sealed class DeletePortfolioCommandHandler : IRequestHandler<DeletePortfolioCommand>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePortfolioCommandHandler(
        IPortfolioRepository portfolioRepository,
        IUnitOfWork unitOfWork)
    {
        _portfolioRepository = portfolioRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        DeletePortfolioCommand command,
        CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository
            .GetByIdAsync(command.PortfolioId, cancellationToken)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Portfolio), command.PortfolioId);

        if (portfolio.UserId != command.UserId)
            throw new DomainException(
                "You do not have permission to delete this portfolio.");

        var userPortfolios = await _portfolioRepository
            .GetByUserIdAsync(command.UserId, cancellationToken);

        if (userPortfolios.Count(p => p.IsActive) <= 1)
            throw new DomainException(
                "Cannot delete your only remaining portfolio.");

        portfolio.Delete();
        _portfolioRepository.Update(portfolio);
    }
}