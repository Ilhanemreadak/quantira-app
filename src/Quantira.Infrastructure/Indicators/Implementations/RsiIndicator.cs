using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators.Implementations;

/// <summary>
/// Relative Strength Index (RSI) indicator.
/// Measures the speed and magnitude of recent price changes to
/// evaluate overbought or oversold conditions.
/// RSI above 70 is generally considered overbought (potential sell signal).
/// RSI below 30 is generally considered oversold (potential buy signal).
/// Uses Wilder's smoothing method (exponential moving average variant)
/// which is the industry standard for RSI calculation.
/// </summary>
public sealed class RsiIndicator : IIndicator
{
    public string Name => "RSI";
    public string DisplayName => "Relative Strength Index";
    public string Description =>
        "Measures overbought/oversold conditions. " +
        "Above 70 = overbought, below 30 = oversold.";
    public string Category => "Momentum";
    public int MinimumPeriod => 15;

    public Dictionary<string, string> DefaultParameters => new()
    {
        { "period", "14" }
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

        var values = CalculateRsi(closes, times, period);

        return new IndicatorResult { Values = values };
    }

    private static IReadOnlyList<IndicatorDataPoint> CalculateRsi(
        decimal[] closes,
        long[] times,
        int period)
    {
        var result = new List<IndicatorDataPoint>(closes.Length);

        // Pad the beginning with null values.
        for (var i = 0; i < period; i++)
            result.Add(new IndicatorDataPoint(times[i], null));

        // Calculate initial average gain and loss over the first period.
        var gains = 0m;
        var losses = 0m;

        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0) gains += change;
            else losses -= change;
        }

        var avgGain = gains / period;
        var avgLoss = losses / period;

        result.Add(new IndicatorDataPoint(
            times[period],
            avgLoss == 0 ? 100m : Math.Round(100m - 100m / (1m + avgGain / avgLoss), 2)));

        // Wilder's smoothing for subsequent values.
        for (var i = period + 1; i < closes.Length; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0m;
            var loss = change < 0 ? -change : 0m;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            var rsi = avgLoss == 0
                ? 100m
                : Math.Round(100m - 100m / (1m + avgGain / avgLoss), 2);

            result.Add(new IndicatorDataPoint(times[i], rsi));
        }

        return result.AsReadOnly();
    }

    private int GetPeriod(Dictionary<string, string>? parameters)
    {
        if (parameters?.TryGetValue("period", out var p) == true
            && int.TryParse(p, out var period)
            && period > 0)
            return period;

        return 14;
    }
}