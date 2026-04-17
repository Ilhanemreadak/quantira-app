using MediatR;
using Quantira.Domain.Entities;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;
using Quantira.Domain.ValueObjects;

namespace Quantira.Application.Portfolios.Commands.CreatePortfolio;

/// <summary>
/// Handles <see cref="CreatePortfolioCommand"/>.
/// Orchestrates portfolio creation through the domain aggregate,
/// enforces the single-default-per-user invariant, and delegates
/// persistence to the repository. Does not call SaveChanges directly —
/// <c>TransactionBehavior</c> commits the unit of work after this
/// handler returns successfully.
/// </summary>
public sealed class CreatePortfolioCommandHandler
    : IRequestHandler<CreatePortfolioCommand, Guid>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public CreatePortfolioCommandHandler(
        IPortfolioRepository portfolioRepository)
    {
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Guid> Handle(
        CreatePortfolioCommand command,
        CancellationToken cancellationToken)
    {
        var baseCurrency = Currency.From(command.BaseCurrency);

        if (command.IsDefault)
            await UnsetCurrentDefaultAsync(command.UserId, cancellationToken);

        var portfolio = Portfolio.Create(
            userId: command.UserId,
            name: command.Name,
            baseCurrency: baseCurrency,
            costMethod: command.CostMethod,
            description: command.Description,
            isDefault: command.IsDefault);

        await _portfolioRepository.AddAsync(portfolio, cancellationToken);

        return portfolio.Id;
    }

    private async Task UnsetCurrentDefaultAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var portfolios = await _portfolioRepository
            .GetByUserIdAsync(userId, cancellationToken);

        var currentDefault = portfolios.FirstOrDefault(p => p.IsDefault);
        if (currentDefault is null) return;

        currentDefault.UnsetDefault();
    }
}