using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators.Implementations;

/// <summary>
/// Exponential Moving Average (EMA) indicator.
/// Gives more weight to recent prices than a simple moving average,
/// making it more responsive to new information.
/// Common periods: 9, 20, 50, 100, 200.
/// EMA 50 crossing above EMA 200 = Golden Cross (bullish).
/// EMA 50 crossing below EMA 200 = Death Cross (bearish).
/// </summary>
public sealed class EmaIndicator : IIndicator
{
    public string Name => "EMA";
    public string DisplayName => "Exponential Moving Average";
    public string Description =>
        "Weighted moving average giving more importance to recent prices. " +
        "50/200 EMA crossovers signal major trend changes.";
    public string Category => "Trend";
    public int MinimumPeriod => 10;

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
        var multiplier = 2m / (period + 1);

        var result = new List<IndicatorDataPoint>(times.Length);

        for (var i = 0; i < period - 1; i++)
            result.Add(new IndicatorDataPoint(times[i], null));

        // Seed with SMA.
        var seed = closes.Take(period).Average();
        result.Add(new IndicatorDataPoint(
            times[period - 1], Math.Round(seed, 4)));

        for (var i = period; i < closes.Length; i++)
        {
            var prev = result[i - 1].Value!.Value;
            var ema = (closes[i] - prev) * multiplier + prev;
            result.Add(new IndicatorDataPoint(times[i], Math.Round(ema, 4)));
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