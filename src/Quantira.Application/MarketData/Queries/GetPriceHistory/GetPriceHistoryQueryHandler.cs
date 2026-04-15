using MediatR;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.MarketData.DTOs;

namespace Quantira.Application.MarketData.Queries.GetPriceHistory;

/// <summary>
/// Handles <see cref="GetPriceHistoryQuery"/>.
/// Delegates to <see cref="IMarketDataService"/> which checks the
/// <c>PriceHistory</c> table first and calls the external provider
/// only for data gaps.
/// </summary>
public sealed class GetPriceHistoryQueryHandler
    : IRequestHandler<GetPriceHistoryQuery, IReadOnlyList<OhlcvDto>>
{
    private readonly IMarketDataService _marketDataService;

    public GetPriceHistoryQueryHandler(IMarketDataService marketDataService)
        => _marketDataService = marketDataService;

    public async Task<IReadOnlyList<OhlcvDto>> Handle(
        GetPriceHistoryQuery query,
        CancellationToken cancellationToken)
    {
        return await _marketDataService.GetHistoryAsync(
            symbol: query.Symbol,
            interval: query.Interval,
            from: query.From,
            to: query.To,
            cancellationToken: cancellationToken);
    }
}