using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators.Implementations;

/// <summary>
/// Moving Average Convergence Divergence (MACD) indicator.
/// Shows the relationship between two EMAs of an asset's price.
/// MACD line crossing above signal line = bullish signal.
/// MACD line crossing below signal line = bearish signal.
/// Histogram = MACD line minus signal line (momentum visualization).
/// Default parameters: fast EMA 12, slow EMA 26, signal EMA 9.
/// </summary>
public sealed class MacdIndicator : IIndicator
{
    public string Name => "MACD";
    public string DisplayName => "MACD";
    public string Description =>
        "Moving Average Convergence Divergence. " +
        "Signal line crossovers indicate trend changes.";
    public string Category => "Trend";
    public int MinimumPeriod => 35;

    public Dictionary<string, string> DefaultParameters => new()
    {
        { "fast",   "12" },
        { "slow",   "26" },
        { "signal", "9"  }
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
        var fast = GetParam(parameters, "fast", 12);
        var slow = GetParam(parameters, "slow", 26);
        var signal = GetParam(parameters, "signal", 9);

        var closes = candles.Select(c => c.Close).ToArray();
        var times = candles.Select(c => c.Time).ToArray();

        var fastEma = CalculateEma(closes, fast);
        var slowEma = CalculateEma(closes, slow);

        // MACD line = fast EMA - slow EMA
        var macdLine = fastEma
            .Zip(slowEma, (f, s) =>
                f.HasValue && s.HasValue ? f - s : (decimal?)null)
            .ToArray();

        // Signal line = EMA of MACD line
        var macdValues = macdLine
            .Select(v => v ?? 0m)
            .ToArray();

        var signalLine = CalculateEma(macdValues, signal);

        // Histogram = MACD - Signal
        var macdPoints = new List<IndicatorDataPoint>(times.Length);
        var signalPoints = new List<IndicatorDataPoint>(times.Length);
        var histPoints = new List<IndicatorDataPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            var macdVal = macdLine[i];
            var signalVal = signalLine[i];
            var histVal = macdVal.HasValue && signalVal.HasValue
                ? macdVal - signalVal
                : null;

            macdPoints.Add(new IndicatorDataPoint(
                times[i],
                macdVal.HasValue ? Math.Round(macdVal.Value, 4) : null));

            signalPoints.Add(new IndicatorDataPoint(
                times[i],
                signalVal.HasValue ? Math.Round(signalVal.Value, 4) : null));

            histPoints.Add(new IndicatorDataPoint(
                times[i],
                histVal.HasValue ? Math.Round(histVal.Value, 4) : null));
        }

        return new IndicatorResult
        {
            Values = macdPoints.AsReadOnly(),
            AdditionalSeries = new Dictionary<string, IReadOnlyList<IndicatorDataPoint>>
            {
                { "Signal",    signalPoints.AsReadOnly() },
                { "Histogram", histPoints.AsReadOnly()   }
            }
        };
    }

    private static decimal?[] CalculateEma(decimal[] values, int period)
    {
        var result = new decimal?[values.Length];
        var multiplier = 2m / (period + 1);

        // Seed with SMA for the first value.
        var firstValid = period - 1;
        if (firstValid >= values.Length) return result;

        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += values[i];

        result[firstValid] = sum / period;

        for (var i = firstValid + 1; i < values.Length; i++)
            result[i] = (values[i] - result[i - 1]!.Value)
                        * multiplier
                        + result[i - 1]!.Value;

        return result;
    }

    private static int GetParam(
        Dictionary<string, string>? parameters,
        string key,
        int defaultValue)
    {
        if (parameters?.TryGetValue(key, out var val) == true
            && int.TryParse(val, out var parsed)
            && parsed > 0)
            return parsed;

        return defaultValue;
    }
}