using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Application.Portfolios.DTOs;

namespace Quantira.Application.Portfolios.Queries.GetPortfolioList;

/// <summary>
/// Query to retrieve all active portfolios belonging to the authenticated user.
/// Returns a lightweight list suitable for the portfolio switcher and
/// sidebar navigation — does not include positions or trade history.
/// Results are cached per user for 60 seconds since portfolio metadata
/// (name, currency, default flag) changes infrequently.
/// </summary>
/// <param name="UserId">The authenticated user whose portfolios are requested.</param>
public sealed record GetPortfolioListQuery(Guid UserId)
    : IRequest<IReadOnlyList<PortfolioDto>>, ICacheableQuery
{
    public string CacheKey => $"quantira:portfolio:list:{UserId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);
}