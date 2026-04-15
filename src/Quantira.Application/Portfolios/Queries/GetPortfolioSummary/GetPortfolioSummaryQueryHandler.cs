using MediatR;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.Portfolios.DTOs;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Portfolios.Queries.GetPortfolioSummary;

/// <summary>
/// Handles <see cref="GetPortfolioSummaryQuery"/>.
/// Loads the portfolio with all positions and their associated asset metadata,
/// fetches the latest price for each position from Redis via
/// <see cref="IMarketDataService"/>, calculates aggregated totals,
/// and assembles the <see cref="PortfolioSummaryDto"/>.
/// Ownership is verified before any data is returned.
/// </summary>
public sealed class GetPortfolioSummaryQueryHandler
    : IRequestHandler<GetPortfolioSummaryQuery, PortfolioSummaryDto>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IMarketDataService _marketDataService;

    public GetPortfolioSummaryQueryHandler(
        IPortfolioRepository portfolioRepository,
        IMarketDataService marketDataService)
    {
        _portfolioRepository = portfolioRepository;
        _marketDataService = marketDataService;
    }

    public async Task<PortfolioSummaryDto> Handle(
        GetPortfolioSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository
            .GetWithPositionsAsync(query.PortfolioId, cancellationToken)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Portfolio), query.PortfolioId);

        if (portfolio.UserId != query.UserId)
            throw new DomainException(
                "You do not have permission to access this portfolio.");

        var positions = portfolio.Positions.ToList();

        var symbols = positions
            .Select(p => p.AssetId.ToString())
            .Distinct()
            .ToList();

        var latestPrices = symbols.Count > 0
            ? await _marketDataService.GetBatchLatestAsync(symbols, cancellationToken)
            : [];

        var priceMap = latestPrices.ToDictionary(p => p.Symbol);

        var positionDtos = positions.Select(position =>
        {
            priceMap.TryGetValue(position.AssetId.ToString(), out var priceData);

            return new PositionDto(
                AssetId: position.AssetId,
                Quantity: position.Quantity,
                AvgCostPrice: position.AvgCostPrice.Amount,
                TotalCost: position.TotalCost.Amount,
                Currency: position.AvgCostPrice.Currency.Code,
                CurrentPrice: priceData?.Price ?? 0m,
                CurrentValue: position.CurrentValue?.Amount ?? 0m,
                UnrealizedPnL: position.UnrealizedPnL?.Amount ?? 0m,
                UnrealizedPnLPct: position.UnrealizedPnLPct ?? 0m,
                LastUpdated: position.LastUpdated);
        }).ToList();

        var totalCost = positionDtos.Sum(p => p.TotalCost);
        var totalCurrentValue = positionDtos.Sum(p => p.CurrentValue);
        var totalUnrealizedPnL = totalCurrentValue - totalCost;
        var totalPnLPct = totalCost == 0
            ? 0m
            : totalUnrealizedPnL / totalCost * 100m;

        return new PortfolioSummaryDto(
            PortfolioId: portfolio.Id,
            Name: portfolio.Name,
            BaseCurrency: portfolio.BaseCurrency.Code,
            TotalCost: totalCost,
            TotalCurrentValue: totalCurrentValue,
            TotalUnrealizedPnL: totalUnrealizedPnL,
            TotalUnrealizedPnLPct: totalPnLPct,
            Positions: positionDtos,
            LastUpdated: DateTime.UtcNow);
    }
}