using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.MarketData.Providers;

/// <summary>
/// Market data provider implementation for Yahoo Finance.
/// Handles stocks listed on BIST, NYSE, NASDAQ and most global exchanges.
/// Uses the unofficial Yahoo Finance v8 JSON API which is free but
/// has rate limits and no SLA — Polly retry/circuit-breaker policies
/// are applied at the HttpClient level in DependencyInjection.cs.
/// Symbol format: domestic BIST symbols require ".IS" suffix
/// (e.g. "THYAO" → "THYAO.IS"). The DataProviderKey on the Asset
/// entity stores the provider-specific symbol to avoid runtime mapping.
/// </summary>
public sealed class YahooFinanceProvider : IMarketDataProvider
{
    public string ProviderName => "Yahoo Finance";

    private static readonly HashSet<AssetType> SupportedTypes =
    [
        AssetType.Stock,
        AssetType.Fund,
        AssetType.Currency
    ];

    private readonly HttpClient _httpClient;
    private readonly ILogger<YahooFinanceProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public YahooFinanceProvider(
        HttpClient httpClient,
        ILogger<YahooFinanceProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(AssetType assetType, string? exchange = null)
        => SupportedTypes.Contains(assetType);

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
        var symbolList = string.Join(",", symbols);
        var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={symbolList}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<YahooQuoteResponse>(
                url, JsonOpts, cancellationToken);

            return response?.QuoteResponse?.Result?
                .Select(MapToDto)
                .ToList()
                .AsReadOnly()
                ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[YahooFinance] Failed to fetch quotes for symbols: {Symbols}",
                symbolList);
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
        var fromUnix = ((DateTimeOffset)from).ToUnixTimeSeconds();
        var toUnix = ((DateTimeOffset)to).ToUnixTimeSeconds();

        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}" +
                  $"?interval={interval}&period1={fromUnix}&period2={toUnix}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<YahooChartResponse>(
                url, JsonOpts, cancellationToken);

            var result = response?.Chart?.Result?.FirstOrDefault();
            if (result is null) return [];

            var timestamps = result.Timestamps ?? [];
            var quotes = result.Indicators?.Quote?.FirstOrDefault();
            if (quotes is null) return [];

            return timestamps
                .Select((t, i) => new OhlcvDto(
                    Time: t,
                    Open: quotes.Open?[i] ?? 0m,
                    High: quotes.High?[i] ?? 0m,
                    Low: quotes.Low?[i] ?? 0m,
                    Close: quotes.Close?[i] ?? 0m,
                    Volume: (long)(quotes.Volume?[i] ?? 0)))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[YahooFinance] Failed to fetch history for {Symbol} interval={Interval}",
                symbol, interval);
            return [];
        }
    }

    public Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        // Simple rule-based check — replace with exchange calendar API
        // (e.g. Polygon.io market status) in a production deployment.
        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTime.UtcNow,
            exchange == "BIST" ? "Turkey Standard Time" : "Eastern Standard Time");

        var isWeekday = now.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        var isMarketHours = exchange == "BIST"
            ? now.TimeOfDay >= TimeSpan.FromHours(10) && now.TimeOfDay <= TimeSpan.FromHours(18)
            : now.TimeOfDay >= TimeSpan.FromHours(9.5) && now.TimeOfDay <= TimeSpan.FromHours(16);

        return Task.FromResult(isWeekday && isMarketHours);
    }

    private static PriceLatestDto MapToDto(YahooQuote q) => new(
        Symbol: q.Symbol ?? string.Empty,
        Price: (decimal)(q.RegularMarketPrice ?? 0),
        Change: (decimal)(q.RegularMarketChange ?? 0),
        ChangePct: (decimal)(q.RegularMarketChangePercent ?? 0),
        DayHigh: (decimal?)q.RegularMarketDayHigh,
        DayLow: (decimal?)q.RegularMarketDayLow,
        Volume: q.RegularMarketVolume,
        MarketStatus: q.MarketState ?? "CLOSED",
        Timestamp: DateTime.UtcNow);

    // ── Internal response models ─────────────────────────────────────
    private sealed record YahooQuoteResponse(YahooQuoteResult? QuoteResponse);
    private sealed record YahooQuoteResult(List<YahooQuote>? Result);
    private sealed record YahooQuote(
        string? Symbol,
        double? RegularMarketPrice,
        double? RegularMarketChange,
        double? RegularMarketChangePercent,
        double? RegularMarketDayHigh,
        double? RegularMarketDayLow,
        long? RegularMarketVolume,
        string? MarketState);
    private sealed record YahooChartResponse(YahooChartResult? Chart);
    private sealed record YahooChartResult(List<YahooChartItem>? Result);
    private sealed record YahooChartItem(
        List<long>? Timestamps,
        YahooChartIndicators? Indicators);
    private sealed record YahooChartIndicators(List<YahooQuoteData>? Quote);
    private sealed record YahooQuoteData(
        List<decimal?>? Open,
        List<decimal?>? High,
        List<decimal?>? Low,
        List<decimal?>? Close,
        List<double?>? Volume);
}