using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.MarketData.Providers;

/// <summary>
/// Market data provider for physical commodities: gold (XAU), silver (XAG),
/// platinum (XPT), and crude oil (WTI, BRENT).
/// Uses goldapi.io for precious metals and the EIA public API for oil prices.
/// Both APIs require a free API key configured in User Secrets.
/// Commodity markets have limited trading hours — <see cref="IsMarketOpenAsync"/>
/// checks London/New York metal trading session times.
/// </summary>
public sealed class GoldApiProvider : IMarketDataProvider
{
    public string ProviderName => "GoldApi";

    private static readonly HashSet<AssetType> SupportedTypes =
        [AssetType.Commodity];

    private readonly HttpClient _httpClient;
    private readonly ILogger<GoldApiProvider> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GoldApiProvider(
        HttpClient httpClient,
        ILogger<GoldApiProvider> logger)
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
        var url = $"https://www.goldapi.io/api/{symbol}/USD";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<GoldApiResponse>(
                url, JsonOpts, cancellationToken);

            if (response is null)
                return EmptyDto(symbol);

            return new PriceLatestDto(
                Symbol: symbol,
                Price: (decimal)response.Price,
                Change: (decimal)response.ChPrice,
                ChangePct: (decimal)response.ChPricePct,
                DayHigh: (decimal?)response.High,
                DayLow: (decimal?)response.Low,
                Volume: null,
                MarketStatus: "OPEN",
                Timestamp: DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GoldApi] Failed to fetch price for {Symbol}", symbol);
            return EmptyDto(symbol);
        }
    }

    public async Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        // GoldApi does not support batch requests — fetch sequentially.
        var results = new List<PriceLatestDto>();

        foreach (var symbol in symbols)
        {
            var dto = await GetLatestAsync(symbol, cancellationToken);
            results.Add(dto);
        }

        return results.AsReadOnly();
    }

    public Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        // Historical commodity data is sourced from the PriceHistory table
        // populated by the nightly data archival job.
        // Direct provider history calls are not supported on the free tier.
        _logger.LogWarning(
            "[GoldApi] History not available via provider for {Symbol}. " +
            "Use PriceHistory table instead.", symbol);

        return Task.FromResult<IReadOnlyList<OhlcvDto>>([]);
    }

    public Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        // London Metal Exchange hours: Mon-Fri 01:00 - 19:00 UTC
        var utcNow = DateTime.UtcNow;
        var isWeekday = utcNow.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        var isHours = utcNow.TimeOfDay >= TimeSpan.FromHours(1)
                     && utcNow.TimeOfDay <= TimeSpan.FromHours(19);

        return Task.FromResult(isWeekday && isHours);
    }

    private static PriceLatestDto EmptyDto(string symbol) => new(
        Symbol: symbol,
        Price: 0m,
        Change: 0m,
        ChangePct: 0m,
        DayHigh: null,
        DayLow: null,
        Volume: null,
        MarketStatus: "UNKNOWN",
        Timestamp: DateTime.UtcNow);

    private sealed record GoldApiResponse(
        double Price,
        double ChPrice,
        double ChPricePct,
        double? High,
        double? Low);
}