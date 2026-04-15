using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Domain.Enums;

namespace Quantira.Application.Portfolios.Commands.CreatePortfolio;

/// <summary>
/// Command to create a new portfolio for the authenticated user.
/// Dispatched by <c>PortfoliosController</c> via MediatR.
/// Validated by <see cref="CreatePortfolioCommandValidator"/> before
/// the handler is invoked. Wrapped in a database transaction by
/// <c>TransactionBehavior</c> since it writes both the portfolio record
/// and raises a <c>PortfolioCreatedEvent</c> that may trigger additional writes.
/// </summary>
/// <param name="UserId">The authenticated user creating the portfolio.</param>
/// <param name="Name">Display name. Must be unique per user. Max 100 characters.</param>
/// <param name="BaseCurrency">
/// ISO 4217 currency code for P&amp;L and valuation reporting (e.g. "TRY", "USD").
/// </param>
/// <param name="CostMethod">
/// Inventory cost method applied to all sell trades in this portfolio.
/// Defaults to <see cref="CostMethod.Fifo"/>.
/// </param>
/// <param name="Description">Optional free-text description. Max 500 characters.</param>
/// <param name="IsDefault">
/// When <c>true</c>, this portfolio becomes the user's default view.
/// The handler automatically unsets the previous default.
/// </param>
public sealed record CreatePortfolioCommand(
    Guid UserId,
    string Name,
    string BaseCurrency,
    CostMethod CostMethod = CostMethod.Fifo,
    string? Description = null,
    bool IsDefault = false
) : IRequest<Guid>, ITransactionalCommand;