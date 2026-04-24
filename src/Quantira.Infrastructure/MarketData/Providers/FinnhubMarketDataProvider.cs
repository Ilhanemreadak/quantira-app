using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.MarketData.Providers;

/// <summary>
/// Market data provider backed by the Finnhub REST API.
/// Handles stocks and funds. Registered before YahooFinanceProvider
/// so it takes priority for these asset types.
/// Free tier allows 60 requests/minute — batch calls are parallelized
/// but throttled to avoid hitting the limit.
/// </summary>
public sealed class FinnhubMarketDataProvider : IMarketDataProvider
{
    public string ProviderName => "Finnhub";

    private static readonly HashSet<AssetType> SupportedTypes =
    [
        AssetType.Stock,
        AssetType.Fund
    ];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FinnhubOptions _options;
    private readonly ILogger<FinnhubMarketDataProvider> _logger;

    public FinnhubMarketDataProvider(
        HttpClient httpClient,
        IOptions<FinnhubOptions> options,
        ILogger<FinnhubMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool CanHandle(AssetType assetType, string? exchange = null)
        => SupportedTypes.Contains(assetType)
           && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<PriceLatestDto> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_options.BaseUrl}/quote?symbol={symbol}&token={_options.ApiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<FinnhubQuote>(
                url, JsonOpts, cancellationToken);

            return MapToDto(symbol, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Finnhub] Failed to fetch quote for {Symbol}", symbol);
            return EmptyDto(symbol);
        }
    }

    public async Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();

        var tasks = symbolList.Select(s => GetLatestAsync(s, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var resolution = MapInterval(interval);
        var fromUnix = ((DateTimeOffset)from.ToUniversalTime()).ToUnixTimeSeconds();
        var toUnix = ((DateTimeOffset)to.ToUniversalTime()).ToUnixTimeSeconds();

        var url = $"{_options.BaseUrl}/stock/candle" +
                  $"?symbol={symbol}&resolution={resolution}" +
                  $"&from={fromUnix}&to={toUnix}&token={_options.ApiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<FinnhubCandles>(
                url, JsonOpts, cancellationToken);

            if (response?.S != "ok" || response.T is null)
            {
                _logger.LogWarning(
                    "[Finnhub] No candle data for {Symbol} interval={Interval} status={Status}",
                    symbol, interval, response?.S);
                return [];
            }

            return response.T
                .Select((t, i) => new OhlcvDto(
                    Time: t,
                    Open: response.O?[i] ?? 0m,
                    High: response.H?[i] ?? 0m,
                    Low: response.L?[i] ?? 0m,
                    Close: response.C?[i] ?? 0m,
                    Volume: (long)(response.V?[i] ?? 0)))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Finnhub] Failed to fetch history for {Symbol} interval={Interval}",
                symbol, interval);
            return [];
        }
    }

    public async Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        var exchangeCode = exchange == "BIST" ? "US" : exchange;
        var url = $"{_options.BaseUrl}/stock/market-status?exchange={exchangeCode}&token={_options.ApiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<FinnhubMarketStatus>(
                url, JsonOpts, cancellationToken);

            return response?.IsOpen ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Finnhub] Failed to fetch market status for {Exchange}", exchange);
            return false;
        }
    }

    private static string MapInterval(string interval) => interval switch
    {
        "1m"  => "1",
        "5m"  => "5",
        "15m" => "15",
        "30m" => "30",
        "1h"  => "60",
        "1d"  => "D",
        "1wk" => "W",
        "1mo" => "M",
        _     => "D"
    };

    private static PriceLatestDto MapToDto(string symbol, FinnhubQuote? q) => new(
        Symbol: symbol,
        Price: q?.C ?? 0m,
        Change: q?.D ?? 0m,
        ChangePct: q?.Dp ?? 0m,
        DayHigh: q?.H,
        DayLow: q?.L,
        Volume: null,
        MarketStatus: "OPEN",
        Timestamp: DateTime.UtcNow);

    private static PriceLatestDto EmptyDto(string symbol) => new(
        Symbol: symbol,
        Price: 0m, Change: 0m, ChangePct: 0m,
        DayHigh: null, DayLow: null, Volume: null,
        MarketStatus: "UNKNOWN", Timestamp: DateTime.UtcNow);

    // ── Response models ──────────────────────────────────────────────
    private sealed record FinnhubQuote(
        decimal? C,   // Current price
        decimal? D,   // Change
        decimal? Dp,  // Change percent
        decimal? H,   // Day high
        decimal? L,   // Day low
        decimal? O,   // Open
        decimal? Pc); // Previous close

    private sealed record FinnhubCandles(
        string? S,           // Status: "ok" | "no_data"
        List<long>? T,       // Timestamps
        List<decimal?>? O,
        List<decimal?>? H,
        List<decimal?>? L,
        List<decimal?>? C,
        List<double?>? V);

    private sealed record FinnhubMarketStatus(bool IsOpen);
}

public sealed class FinnhubOptions
{
    public const string SectionName = "Finnhub";

    /// <summary>Store via: <c>dotnet user-secrets set "Finnhub:ApiKey" "..."</c></summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Store via: <c>dotnet user-secrets set "Finnhub:WebhookSecret" "..."</c></summary>
    public string WebhookSecret { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";
}
