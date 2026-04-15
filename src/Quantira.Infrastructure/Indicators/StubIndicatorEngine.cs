using Quantira.Application.Common.Interfaces;
using Quantira.Application.MarketData.DTOs;

namespace Quantira.Infrastructure.Indicators;

/// <summary>
/// Stub implementation of <see cref="IIndicatorEngine"/>.
/// Returns empty results until the real indicator implementations
/// (RSI, MACD, Bollinger etc.) are wired up in a later phase.
/// Replace this registration with the real engine in
/// <c>DependencyInjection.cs</c> when indicators are implemented.
/// </summary>
public sealed class StubIndicatorEngine : IIndicatorEngine
{
    public Task<IndicatorResultDto> CalculateAsync(
        string symbol,
        string indicatorName,
        string interval,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var empty = new IndicatorResultDto(
            Symbol: symbol,
            IndicatorName: indicatorName,
            Interval: interval,
            Values: [],
            AdditionalSeries: null,
            CalculatedAt: DateTime.UtcNow);

        return Task.FromResult(empty);
    }

    public Task<IReadOnlyList<IndicatorResultDto>> CalculateBatchAsync(
        string symbol,
        IEnumerable<IndicatorRequest> requests,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IndicatorResultDto> empty = [];
        return Task.FromResult(empty);
    }

    public IReadOnlyList<IndicatorMetadata> GetAvailableIndicators()
        => [];
}