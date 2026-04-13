using MediatR;
using Mapster;
using Quantira.Application.Portfolios.DTOs;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Portfolios.Queries.GetPortfolioList;

/// <summary>
/// Handles <see cref="GetPortfolioListQuery"/>.
/// Fetches all active portfolios for the user and projects them
/// to <see cref="PortfolioDto"/> via Mapster.
/// The result is cached by <c>CachingBehavior</c> — this handler
/// is only invoked on a cache miss.
/// </summary>
public sealed class GetPortfolioListQueryHandler
    : IRequestHandler<GetPortfolioListQuery, IReadOnlyList<PortfolioDto>>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public GetPortfolioListQueryHandler(IPortfolioRepository portfolioRepository)
        => _portfolioRepository = portfolioRepository;

    public async Task<IReadOnlyList<PortfolioDto>> Handle(
        GetPortfolioListQuery query,
        CancellationToken cancellationToken)
    {
        var portfolios = await _portfolioRepository
            .GetByUserIdAsync(query.UserId, cancellationToken);

        return portfolios
            .Select(p => p.Adapt<PortfolioDto>())
            .ToList()
            .AsReadOnly();
    }
}