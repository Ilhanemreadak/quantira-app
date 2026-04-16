using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators.Implementations;

/// <summary>
/// Average True Range (ATR) indicator.
/// Measures market volatility by decomposing the entire range of
/// an asset price for that period. Does not indicate price direction —
/// only the degree of price movement (volatility).
/// Widely used for setting stop-loss levels: a stop at 2×ATR below
/// entry price gives the trade room to breathe without excessive risk.
/// </summary>
public sealed class AtrIndicator : IIndicator
{
    public string Name => "ATR";
    public string DisplayName => "Average True Range";
    public string Description =>
        "Measures market volatility. " +
        "Commonly used for stop-loss placement (e.g. 2x ATR).";
    public string Category => "Volatility";
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
        var times = candles.Select(c => c.Time).ToArray();

        var trueRanges = new decimal[candles.Count];

        // First TR has no previous close.
        trueRanges[0] = candles[0].High - candles[0].Low;

        for (var i = 1; i < candles.Count; i++)
        {
            var high = candles[i].High;
            var low = candles[i].Low;
            var prevClose = candles[i - 1].Close;

            trueRanges[i] = Math.Max(
                high - low,
                Math.Max(
                    Math.Abs(high - prevClose),
                    Math.Abs(low - prevClose)));
        }

        var result = new List<IndicatorDataPoint>(times.Length);

        for (var i = 0; i < period - 1; i++)
            result.Add(new IndicatorDataPoint(times[i], null));

        // Seed ATR with simple average of first period.
        var seedAtr = trueRanges.Take(period).Average();
        result.Add(new IndicatorDataPoint(
            times[period - 1], Math.Round(seedAtr, 4)));

        // Wilder's smoothing.
        for (var i = period; i < candles.Count; i++)
        {
            var prev = result[i - 1].Value!.Value;
            var atr = (prev * (period - 1) + trueRanges[i]) / period;
            result.Add(new IndicatorDataPoint(times[i], Math.Round(atr, 4)));
        }

        return new IndicatorResult { Values = result.AsReadOnly() };
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