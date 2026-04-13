using MediatR;
using Quantira.Application.Common.Behaviors;

namespace Quantira.Application.Portfolios.Commands.DeletePortfolio;

/// <summary>
/// Command to soft-delete a portfolio. The portfolio record and all its
/// associated positions and trade history are retained in the database
/// for audit and compliance purposes but are excluded from all standard
/// queries via the global soft-delete filter.
/// </summary>
/// <param name="PortfolioId">The portfolio to delete.</param>
/// <param name="UserId">
/// The authenticated user making the request. Used to verify ownership
/// — a user cannot delete another user's portfolio.
/// </param>
public sealed record DeletePortfolioCommand(
    Guid PortfolioId,
    Guid UserId
) : IRequest, ITransactionalCommand;