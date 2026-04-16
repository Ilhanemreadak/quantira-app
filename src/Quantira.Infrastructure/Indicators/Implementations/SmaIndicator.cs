using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators.Implementations;

/// <summary>
/// Simple Moving Average (SMA) indicator.
/// The arithmetic mean of prices over a defined number of periods.
/// Slower to react to price changes than EMA but less prone to
/// false signals in choppy markets.
/// Common periods: 20, 50, 100, 200.
/// </summary>
public sealed class SmaIndicator : IIndicator
{
    public string Name => "SMA";
    public string DisplayName => "Simple Moving Average";
    public string Description =>
        "Arithmetic mean of closing prices over N periods. " +
        "Slower than EMA, more reliable in ranging markets.";
    public string Category => "Trend";
    public int MinimumPeriod => 5;

    public Dictionary<string, string> DefaultParameters => new()
    {
        { "period", "20" }
    };

    public IReadOnlyList<AssetType> SupportedAssetTypes =>
    [
        AssetType.Stock,
        AssetType.Crypto,
        AssetType.Commodity,
        AssetType.Currency,
        AssetType.Fund
    ];

    public IndicatorResult Calculate(
        IReadOnlyList<OhlcvDto> candles,
        Dictionary<string, string>? parameters = null)
    {
        var period = GetPeriod(parameters);
        var closes = candles.Select(c => c.Close).ToArray();
        var times = candles.Select(c => c.Time).ToArray();

        var result = new List<IndicatorDataPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            if (i < period - 1)
            {
                result.Add(new IndicatorDataPoint(times[i], null));
                continue;
            }

            var sma = closes
                .Skip(i - period + 1)
                .Take(period)
                .Average();

            result.Add(new IndicatorDataPoint(
                times[i], Math.Round(sma, 4)));
        }

        return new IndicatorResult { Values = result.AsReadOnly() };
    }

    private int GetPeriod(Dictionary<string, string>? parameters)
    {
        if (parameters?.TryGetValue("period", out var p) == true
            && int.TryParse(p, out var period)
            && period > 0)
            return period;

        return 20;
    }
}