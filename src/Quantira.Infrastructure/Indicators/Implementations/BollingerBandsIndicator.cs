using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Indicators.Implementations;

/// <summary>
/// Bollinger Bands indicator.
/// Consists of a middle band (SMA) and two outer bands placed at
/// a standard deviation distance above and below the middle band.
/// Price touching the upper band may indicate overbought conditions.
/// Price touching the lower band may indicate oversold conditions.
/// Bandwidth narrows during low volatility (squeeze) and widens
/// during high volatility periods.
/// Default: 20-period SMA with 2 standard deviations.
/// </summary>
public sealed class BollingerBandsIndicator : IIndicator
{
    public string Name => "BOLLINGER";
    public string DisplayName => "Bollinger Bands";
    public string Description =>
        "Volatility bands placed above and below a moving average. " +
        "Squeeze indicates low volatility before a breakout.";
    public string Category => "Volatility";
    public int MinimumPeriod => 20;

    public Dictionary<string, string> DefaultParameters => new()
    {
        { "period", "20" },
        { "stddev", "2"  }
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
        var period = GetParam(parameters, "period", 20);
        var stdDev = GetParamDecimal(parameters, "stddev", 2m);

        var closes = candles.Select(c => c.Close).ToArray();
        var times = candles.Select(c => c.Time).ToArray();

        var middleBand = new List<IndicatorDataPoint>(times.Length);
        var upperBand = new List<IndicatorDataPoint>(times.Length);
        var lowerBand = new List<IndicatorDataPoint>(times.Length);

        for (var i = 0; i < times.Length; i++)
        {
            if (i < period - 1)
            {
                middleBand.Add(new IndicatorDataPoint(times[i], null));
                upperBand.Add(new IndicatorDataPoint(times[i], null));
                lowerBand.Add(new IndicatorDataPoint(times[i], null));
                continue;
            }

            var window = closes.Skip(i - period + 1).Take(period).ToArray();
            var sma = window.Average();
            var std = CalculateStdDev(window, sma);

            middleBand.Add(new IndicatorDataPoint(
                times[i], Math.Round(sma, 4)));

            upperBand.Add(new IndicatorDataPoint(
                times[i], Math.Round(sma + stdDev * std, 4)));

            lowerBand.Add(new IndicatorDataPoint(
                times[i], Math.Round(sma - stdDev * std, 4)));
        }

        return new IndicatorResult
        {
            Values = middleBand.AsReadOnly(),
            AdditionalSeries = new Dictionary<string, IReadOnlyList<IndicatorDataPoint>>
            {
                { "Upper", upperBand.AsReadOnly() },
                { "Lower", lowerBand.AsReadOnly() }
            }
        };
    }

    private static decimal CalculateStdDev(decimal[] values, decimal mean)
    {
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return (decimal)Math.Sqrt((double)(sumSquares / values.Length));
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

    private static decimal GetParamDecimal(
        Dictionary<string, string>? parameters,
        string key,
        decimal defaultValue)
    {
        if (parameters?.TryGetValue(key, out var val) == true
            && decimal.TryParse(val, out var parsed)
            && parsed > 0)
            return parsed;

        return defaultValue;
    }
}