using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Application.Portfolios.DTOs;

namespace Quantira.Application.Portfolios.Queries.GetPortfolioSummary;

/// <summary>
/// Query to retrieve a full portfolio summary including all open positions,
/// total current value, total cost basis, unrealized P&amp;L and allocation
/// breakdown by asset type and sector.
/// This is the primary data source for the Quantira dashboard.
/// Results are cached for 30 seconds — short enough to reflect recent
/// price updates while avoiding redundant recalculations on every
/// SignalR-triggered dashboard refresh.
/// </summary>
/// <param name="PortfolioId">The portfolio to summarise.</param>
/// <param name="UserId">
/// The authenticated user. Verified against portfolio ownership
/// in the handler to prevent cross-user data access.
/// </param>
public sealed record GetPortfolioSummaryQuery(Guid PortfolioId, Guid UserId)
    : IRequest<PortfolioSummaryDto>, ICacheableQuery
{
    public string CacheKey => $"quantira:portfolio:summary:{PortfolioId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);
}