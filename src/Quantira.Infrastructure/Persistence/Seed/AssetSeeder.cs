using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds the initial asset catalogue on first startup.
/// Only inserts assets that do not already exist — safe to run
/// on every application start via <c>Program.cs</c>.
/// Covers the most commonly traded BIST stocks, global crypto pairs,
/// precious metals, oil benchmarks and major FX pairs.
/// </summary>
public static class AssetSeeder
{
    public static async Task SeedAsync(
        QuantiraDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.Assets
            .Select(a => a.Symbol)
            .ToHashSetAsync(cancellationToken);

        var assets = GetSeedAssets()
            .Where(a => !existing.Contains(a.Symbol))
            .ToList();

        if (assets.Count == 0)
        {
            logger.LogDebug("[AssetSeeder] All seed assets already exist, skipping.");
            return;
        }

        await context.Assets.AddRangeAsync(assets, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "[AssetSeeder] Seeded {Count} assets.", assets.Count);
    }

    private static List<Asset> GetSeedAssets()
    {
        var assets = new List<Asset>();

        // ── BIST Stocks ───────────────────────────────────────────────
        var bistStocks = new[]
        {
            ("THYAO", "Turk Hava Yollari",               "Industrials"),
            ("GARAN", "Garanti BBVA",                    "Financials"),
            ("AKBNK", "Akbank",                          "Financials"),
            ("EREGL", "Eregli Demir Celik",              "Materials"),
            ("ASELS", "Aselsan",                         "Industrials"),
            ("SISE",  "Turkiye Sise ve Cam",             "Materials"),
            ("KCHOL", "Koc Holding",                     "Industrials"),
            ("TUPRS", "Tupras",                          "Energy"),
            ("BIMAS", "BIM Birlesik Magazalar",          "Consumer Staples"),
            ("SAHOL", "Sabanci Holding",                 "Industrials"),
            ("YKBNK", "Yapi ve Kredi Bankasi",           "Financials"),
            ("TOASO", "Tofas Turk Otomobil Fabrikasi",  "Consumer Discretionary"),
            ("FROTO", "Ford Otomotiv Sanayi",            "Consumer Discretionary"),
            ("PGSUS", "Pegasus Hava Tasimaciligi",       "Industrials"),
            ("TAVHL", "TAV Havalimanlari Holding",       "Industrials"),
            ("TKFEN", "Tekfen Holding",                  "Industrials"),
            ("ARCLK", "Arcelik",                         "Consumer Discretionary"),
            ("VESTL", "Vestel Elektronik",               "Consumer Discretionary"),
            ("DOHOL", "Dogan Sirketler Grubu Holding",   "Communication Services"),
            ("EKGYO", "Emlak Konut GYO",                 "Real Estate"),
        };

        foreach (var (symbol, name, sector) in bistStocks)
        {
            assets.Add(Asset.Create(
                symbol: symbol,
                name: name,
                assetType: AssetType.Stock,
                currency: "TRY",
                exchange: "BIST",
                sector: sector,
                dataProviderKey: $"{symbol}.IS"));
        }

        // ── Crypto ────────────────────────────────────────────────────
        var cryptos = new[]
        {
            ("BTC",  "Bitcoin",        "BTCUSDT"),
            ("ETH",  "Ethereum",       "ETHUSDT"),
            ("BNB",  "BNB",            "BNBUSDT"),
            ("SOL",  "Solana",         "SOLUSDT"),
            ("XRP",  "XRP",            "XRPUSDT"),
            ("USDT", "Tether",         "USDTUSDC"),
            ("ADA",  "Cardano",        "ADAUSDT"),
            ("AVAX", "Avalanche",      "AVAXUSDT"),
            ("DOGE", "Dogecoin",       "DOGEUSDT"),
            ("DOT",  "Polkadot",       "DOTUSDT"),
        };

        foreach (var (symbol, name, providerKey) in cryptos)
        {
            assets.Add(Asset.Create(
                symbol: symbol,
                name: name,
                assetType: AssetType.Crypto,
                currency: "USD",
                exchange: "BINANCE",
                sector: null,
                dataProviderKey: providerKey));
        }

        // ── Commodities ───────────────────────────────────────────────
        var commodities = new[]
        {
            ("XAU", "Gold",            "USD", "XAU"),
            ("XAG", "Silver",          "USD", "XAG"),
            ("XPT", "Platinum",        "USD", "XPT"),
            ("WTI", "Crude Oil (WTI)", "USD", "WTI"),
            ("BRT", "Crude Oil (Brent)","USD","BRENT"),
        };

        foreach (var (symbol, name, currency, providerKey) in commodities)
        {
            assets.Add(Asset.Create(
                symbol: symbol,
                name: name,
                assetType: AssetType.Commodity,
                currency: currency,
                exchange: null,
                sector: null,
                dataProviderKey: providerKey));
        }

        // ── FX Pairs ──────────────────────────────────────────────────
        var fxPairs = new[]
        {
            ("USDTRY", "US Dollar / Turkish Lira",    "USD", "USDTRY=X"),
            ("EURTRY", "Euro / Turkish Lira",         "EUR", "EURTRY=X"),
            ("EURUSD", "Euro / US Dollar",            "EUR", "EURUSD=X"),
            ("GBPTRY", "British Pound / Turkish Lira","GBP", "GBPTRY=X"),
            ("GBPUSD", "British Pound / US Dollar",   "GBP", "GBPUSD=X"),
        };

        foreach (var (symbol, name, currency, providerKey) in fxPairs)
        {
            assets.Add(Asset.Create(
                symbol: symbol,
                name: name,
                assetType: AssetType.Currency,
                currency: currency,
                exchange: null,
                sector: null,
                dataProviderKey: providerKey));
        }

        return assets;
    }
}