using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Assets.Providers;

public sealed class BinanceAssetProvider : IAssetProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BinanceAssetProvider> _logger;

    public BinanceAssetProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<BinanceAssetProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public AssetType SupportedType => AssetType.Crypto;

    public async Task<IReadOnlyList<Asset>> FetchAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Binance");

        var response = await client.GetFromJsonAsync<BinanceExchangeInfo>(
            "api/v3/exchangeInfo",
            cancellationToken);

        if (response?.Symbols is null)
        {
            _logger.LogWarning("[BinanceAssetProvider] Exchange info response is empty.");
            return [];
        }

        var assets = response.Symbols
            .Where(symbol =>
                symbol.QuoteAsset == "USDT"
                && symbol.Status == "TRADING"
                && !string.IsNullOrWhiteSpace(symbol.Symbol)
                && !string.IsNullOrWhiteSpace(symbol.BaseAsset))
            .GroupBy(symbol => symbol.BaseAsset)
            .Select(group => group.First())
            .Select(symbol => Asset.Create(
                symbol: symbol.BaseAsset,
                name: string.IsNullOrWhiteSpace(symbol.BaseAssetName)
                    ? $"{symbol.BaseAsset} (Binance)"
                    : symbol.BaseAssetName,
                assetType: AssetType.Crypto,
                currency: "USD",
                exchange: "BINANCE",
                dataProviderKey: symbol.Symbol))
            .ToList();

        return assets.AsReadOnly();
    }

    private sealed record BinanceExchangeInfo(List<BinanceSymbol>? Symbols);

    private sealed record BinanceSymbol(
        string Symbol,
        string BaseAsset,
        string? BaseAssetName,
        string QuoteAsset,
        string Status);
}
