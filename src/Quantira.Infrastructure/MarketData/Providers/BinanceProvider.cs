using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.MarketData.Providers;

/// <summary>
/// Market data provider for Binance cryptocurrency exchange.
/// Uses the Binance public REST API (no API key required for market data).
/// Crypto markets trade 24/7 so <see cref="IsMarketOpenAsync"/> always
/// returns <c>true</c> for the BINANCE exchange.
/// Symbol format: Binance uses base+quote pairs without a separator
/// (e.g. "BTC" → "BTCUSDT"). The DataProviderKey on the Asset entity
/// stores the Binance-specific symbol.
/// Rate limit: 1200 requests/minute on the public API weight system.
/// </summary>
public sealed class BinanceProvider : IMarketDataProvider
{
    public string ProviderName => "Binance";

    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BinanceProvider(
        HttpClient httpClient,
        ILogger<BinanceProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(AssetType assetType, string? exchange = null)
        => assetType == AssetType.Crypto;

    public async Task<PriceLatestDto> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var results = await GetBatchLatestAsync([symbol], cancellationToken);
        return results.First();
    }

    public async Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tickers = await _httpClient.GetFromJsonAsync<List<BinanceTicker>>(
                "https://api.binance.com/api/v3/ticker/24hr",
                JsonOpts,
                cancellationToken) ?? [];

            var symbolSet = symbols
                .Select(s => s.ToUpperInvariant())
                .ToHashSet();

            return tickers
                .Where(t => symbolSet.Contains(t.Symbol ?? string.Empty))
                .Select(t => new PriceLatestDto(
                    Symbol: t.Symbol ?? string.Empty,
                    Price: decimal.Parse(t.LastPrice ?? "0"),
                    Change: decimal.Parse(t.PriceChange ?? "0"),
                    ChangePct: decimal.Parse(t.PriceChangePercent ?? "0"),
                    DayHigh: decimal.TryParse(t.HighPrice, out var h) ? h : null,
                    DayLow: decimal.TryParse(t.LowPrice, out var l) ? l : null,
                    Volume: long.TryParse(t.Volume, out var v) ? v : null,
                    MarketStatus: "OPEN",
                    Timestamp: DateTime.UtcNow))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Binance] Failed to fetch batch tickers.");
            return [];
        }
    }

    public async Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var fromMs = ((DateTimeOffset)from).ToUnixTimeMilliseconds();
        var toMs = ((DateTimeOffset)to).ToUnixTimeMilliseconds();

        var url = $"https://api.binance.com/api/v3/klines" +
                  $"?symbol={symbol}&interval={interval}&startTime={fromMs}&endTime={toMs}&limit=1000";

        try
        {
            var klines = await _httpClient.GetFromJsonAsync<List<JsonElement[]>>(
                url, cancellationToken) ?? [];

            return klines
                .Select(k => new OhlcvDto(
                    Time: k[0].GetInt64() / 1000,
                    Open: decimal.Parse(k[1].GetString() ?? "0"),
                    High: decimal.Parse(k[2].GetString() ?? "0"),
                    Low: decimal.Parse(k[3].GetString() ?? "0"),
                    Close: decimal.Parse(k[4].GetString() ?? "0"),
                    Volume: (long)double.Parse(k[5].GetString() ?? "0")))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Binance] Failed to fetch klines for {Symbol} interval={Interval}",
                symbol, interval);
            return [];
        }
    }

    /// <summary>Crypto markets are always open.</summary>
    public Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    private sealed record BinanceTicker(
        string? Symbol,
        string? LastPrice,
        string? PriceChange,
        string? PriceChangePercent,
        string? HighPrice,
        string? LowPrice,
        string? Volume);
}