using MediatR;
using Quantira.Application.Common.Models;
using Quantira.Application.Portfolios.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Application.Portfolios.Queries.GetTradeHistory;

/// <summary>
/// Query to retrieve a paginated, optionally filtered list of trades
/// for a given portfolio. Used by the trade history table in the UI
/// and by the P&amp;L report generator.
/// Not cached — trade data changes frequently and must always reflect
/// the latest recorded transactions.
/// </summary>
/// <param name="PortfolioId">The portfolio whose trades are requested.</param>
/// <param name="UserId">Verified against portfolio ownership in the handler.</param>
/// <param name="Page">1-based page number. Defaults to 1.</param>
/// <param name="PageSize">Items per page. Max 100. Defaults to 20.</param>
/// <param name="AssetId">Optional filter to a specific asset.</param>
/// <param name="TradeType">Optional filter to a specific trade type.</param>
/// <param name="From">Optional UTC start of the date range filter.</param>
/// <param name="To">Optional UTC end of the date range filter.</param>
public sealed record GetTradeHistoryQuery(
    Guid PortfolioId,
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    Guid? AssetId = null,
    TradeType? TradeType = null,
    DateTime? From = null,
    DateTime? To = null
) : IRequest<PagedResult<TradeDto>>;