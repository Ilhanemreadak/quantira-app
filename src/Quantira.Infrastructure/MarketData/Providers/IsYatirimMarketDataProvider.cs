using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.MarketData.Providers;

/// <summary>
/// Market data provider backed by IsYatirim (isyatirim.com.tr).
/// Handles BIST-listed stocks. Free, no authentication required.
/// Only provides end-of-day daily data — intraday intervals fall back to daily.
/// </summary>
public sealed class IsYatirimMarketDataProvider : IMarketDataProvider
{
    public string ProviderName => "IsYatirim";

    private const string BaseUrl =
        "https://www.isyatirim.com.tr/_layouts/15/Isyatirim.Website/Common/Data.aspx/HisseTekil";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeZoneInfo BistTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

    private readonly HttpClient _httpClient;
    private readonly ILogger<IsYatirimMarketDataProvider> _logger;

    public IsYatirimMarketDataProvider(
        HttpClient httpClient,
        ILogger<IsYatirimMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanHandle(AssetType assetType, string? exchange = null)
        => assetType == AssetType.Stock
           && string.Equals(exchange, "BIST", StringComparison.OrdinalIgnoreCase);

    public async Task<PriceLatestDto> GetLatestAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var bistNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BistTimeZone);

        // Try today, then walk back up to 5 days to skip weekends / holidays.
        for (var offset = 0; offset <= 5; offset++)
        {
            var date = bistNow.Date.AddDays(-offset);
            var rows = await FetchRowsAsync(symbol, date, date, cancellationToken);

            if (rows is { Count: > 0 })
            {
                var row = rows[^1];
                return MapToLatestDto(symbol, row, bistNow);
            }
        }

        _logger.LogWarning("[IsYatirim] No recent data for {Symbol}", symbol);
        return EmptyLatestDto(symbol);
    }

    public async Task<IReadOnlyList<PriceLatestDto>> GetBatchLatestAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var symbolList = symbols.ToList();
        var results    = new PriceLatestDto[symbolList.Count];
        using var sem  = new SemaphoreSlim(3, 3); // max 3 concurrent requests

        var tasks = symbolList.Select(async (symbol, index) =>
        {
            await sem.WaitAsync(cancellationToken);
            try
            {
                results[index] = await GetLatestAsync(symbol, cancellationToken);
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    public async Task<IReadOnlyList<OhlcvDto>> GetHistoryAsync(
        string symbol,
        string interval,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var rows = await FetchRowsAsync(symbol, from.Date, to.Date, cancellationToken);

        if (rows is null or { Count: 0 })
            return [];

        return rows
            .Select(MapToOhlcvDto)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList()
            .AsReadOnly();
    }

    public Task<bool> IsMarketOpenAsync(
        string exchange,
        CancellationToken cancellationToken = default)
    {
        var bistNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BistTimeZone);

        // BIST: Mon–Fri, 10:00–18:00 Turkey time.
        var isWeekday = bistNow.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
        var isWithinHours = bistNow.TimeOfDay >= new TimeSpan(10, 0, 0)
                         && bistNow.TimeOfDay <= new TimeSpan(18, 0, 0);

        return Task.FromResult(isWeekday && isWithinHours);
    }

    // ── Private helpers ──────────────────────────────────────────────

    private async Task<List<IsYatirimRow>?> FetchRowsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        // IsYatirim uses bare BIST codes — strip Yahoo-style exchange suffixes (.IS, .E etc.)
        var bareSymbol = symbol.Contains('.') ? symbol[..symbol.IndexOf('.')] : symbol;

        var start = startDate.ToString("dd-MM-yyyy");
        var end   = endDate.ToString("dd-MM-yyyy");
        var url   = $"{BaseUrl}?hisse={bareSymbol}&startdate={start}&enddate={end}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<IsYatirimResponse>(
                url, JsonOpts, cancellationToken);

            return response?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[IsYatirim] Failed to fetch data for {Symbol} (bare={BareSymbol}, {Start} → {End})",
                symbol, bareSymbol, start, end);
            return null;
        }
    }

    private static PriceLatestDto MapToLatestDto(
        string symbol,
        IsYatirimRow row,
        DateTime bistNow)
    {
        var prevClose = row.HgdgKapanis;
        var change    = 0m;
        var changePct = 0m;

        return new PriceLatestDto(
            Symbol: symbol,
            Price: row.HgdgKapanis,
            Change: change,
            ChangePct: changePct,
            DayHigh: row.HgdgMax,
            DayLow: row.HgdgMin,
            Volume: (long?)row.HgdgHacim,
            MarketStatus: "CLOSED",
            Timestamp: bistNow.ToUniversalTime());
    }

    private static OhlcvDto? MapToOhlcvDto(IsYatirimRow row)
    {
        if (!DateTime.TryParseExact(
                row.HgdgTarih,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return null;

        var unixTime = ((DateTimeOffset)date.ToUniversalTime()).ToUnixTimeSeconds();

        return new OhlcvDto(
            Time: unixTime,
            Open: row.HgdgAof,
            High: row.HgdgMax,
            Low: row.HgdgMin,
            Close: row.HgdgKapanis,
            Volume: (long)row.HgdgHacim);
    }

    private static PriceLatestDto EmptyLatestDto(string symbol) => new(
        Symbol: symbol,
        Price: 0m, Change: 0m, ChangePct: 0m,
        DayHigh: null, DayLow: null, Volume: null,
        MarketStatus: "UNKNOWN", Timestamp: DateTime.UtcNow);

    // ── Response models ──────────────────────────────────────────────

    private sealed class IsYatirimResponse
    {
        public bool Ok { get; set; }
        public List<IsYatirimRow>? Value { get; set; }
    }

    private sealed class IsYatirimRow
    {
        [JsonPropertyName("HGDG_HS_KODU")]  public string HgdgHsKodu   { get; set; } = string.Empty;
        [JsonPropertyName("HGDG_TARIH")]    public string HgdgTarih    { get; set; } = string.Empty;
        [JsonPropertyName("HGDG_KAPANIS")]  public decimal HgdgKapanis { get; set; }
        [JsonPropertyName("HGDG_AOF")]      public decimal HgdgAof     { get; set; }
        [JsonPropertyName("HGDG_MIN")]      public decimal HgdgMin     { get; set; }
        [JsonPropertyName("HGDG_MAX")]      public decimal HgdgMax     { get; set; }
        [JsonPropertyName("HGDG_HACIM")]    public decimal HgdgHacim   { get; set; }
    }
}
