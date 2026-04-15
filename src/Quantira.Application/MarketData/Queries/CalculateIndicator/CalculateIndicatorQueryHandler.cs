using MediatR;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.MarketData.DTOs;

namespace Quantira.Application.MarketData.Queries.CalculateIndicator;

/// <summary>
/// Handles <see cref="CalculateIndicatorQuery"/>.
/// Delegates to <see cref="IIndicatorEngine"/> which routes to the
/// correct <c>IIndicator</c> implementation and caches the result.
/// </summary>
public sealed class CalculateIndicatorQueryHandler
    : IRequestHandler<CalculateIndicatorQuery, IndicatorResultDto>
{
    private readonly IIndicatorEngine _indicatorEngine;

    public CalculateIndicatorQueryHandler(IIndicatorEngine indicatorEngine)
        => _indicatorEngine = indicatorEngine;

    public async Task<IndicatorResultDto> Handle(
        CalculateIndicatorQuery query,
        CancellationToken cancellationToken)
    {
        return await _indicatorEngine.CalculateAsync(
            symbol: query.Symbol,
            indicatorName: query.IndicatorName,
            interval: query.Interval,
            parameters: query.Parameters,
            cancellationToken: cancellationToken);
    }
}