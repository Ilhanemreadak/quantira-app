using MediatR;
using Mapster;
using Quantira.Application.Common.Models;
using Quantira.Application.Portfolios.DTOs;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Portfolios.Queries.GetTradeHistory;

/// <summary>
/// Handles <see cref="GetTradeHistoryQuery"/>.
/// Verifies portfolio ownership, fetches the filtered trade list from
/// the repository, and returns a paginated result.
/// Pagination is applied at the database level via the repository
/// to avoid loading the full trade history into memory.
/// </summary>
public sealed class GetTradeHistoryQueryHandler
    : IRequestHandler<GetTradeHistoryQuery, PagedResult<TradeDto>>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;

    public GetTradeHistoryQueryHandler(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<PagedResult<TradeDto>> Handle(
        GetTradeHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository
            .GetByIdAsync(query.PortfolioId, cancellationToken)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Portfolio), query.PortfolioId);

        if (portfolio.UserId != query.UserId)
            throw new DomainException(
                "You do not have permission to access this portfolio.");

        var trades = await _tradeRepository.GetByPortfolioAsync(
            portfolioId: query.PortfolioId,
            assetId: query.AssetId,
            tradeType: query.TradeType,
            from: query.From,
            to: query.To,
            cancellationToken: cancellationToken);

        var totalCount = trades.Count;
        var pageSize = Math.Min(query.PageSize, 100);

        var pagedTrades = trades
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => t.Adapt<TradeDto>())
            .ToList()
            .AsReadOnly();

        return new PagedResult<TradeDto>(
            items: pagedTrades,
            totalCount: totalCount,
            page: query.Page,
            pageSize: pageSize);
    }
}