using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Assets.Providers;

public sealed class GlobalStockAssetProvider : IAssetProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GlobalStockProviderOptions _options;
    private readonly ILogger<GlobalStockAssetProvider> _logger;

    public GlobalStockAssetProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GlobalStockProviderOptions> options,
        ILogger<GlobalStockAssetProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public AssetType SupportedType => AssetType.Stock;

    public async Task<IReadOnlyList<Asset>> FetchAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "[GlobalStockAssetProvider] Provider disabled by configuration. Skipping fetch.");
            return [];
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning(
                "[GlobalStockAssetProvider] API key is missing. Configure {Section}:{Key} to enable global stock ingestion.",
                GlobalStockProviderOptions.SectionName,
                nameof(GlobalStockProviderOptions.ApiKey));
            return [];
        }

        if (_options.Exchanges.Count == 0)
        {
            _logger.LogInformation(
                "[GlobalStockAssetProvider] No exchanges configured. Returning empty result.");
            return [];
        }

        var client = _httpClientFactory.CreateClient("GlobalStocks");
        var assets = new List<Asset>();

        foreach (var exchange in _options.Exchanges)
        {
            if (string.IsNullOrWhiteSpace(exchange))
                continue;

            var exchangeCode = exchange.Trim().ToUpperInvariant();
            var requestUri = $"api/v1/stock/symbol?exchange={exchangeCode}&token={_options.ApiKey}";

            _logger.LogInformation(
                "[GlobalStockAssetProvider] Fetching stock catalogue for exchange {Exchange}.",
                exchangeCode);

            var symbols = await client.GetFromJsonAsync<List<FinnhubSymbolResponse>>(
                requestUri,
                cancellationToken);

            if (symbols is null || symbols.Count == 0)
            {
                _logger.LogInformation(
                    "[GlobalStockAssetProvider] Exchange {Exchange} returned no symbols.",
                    exchangeCode);
                continue;
            }

            var mappedAssets = symbols
                .Where(symbol =>
                    string.Equals(symbol.Type, "Common Stock", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(symbol.Symbol)
                    && !string.IsNullOrWhiteSpace(symbol.Description))
                .Select(symbol => Asset.Create(
                    symbol: symbol.Symbol,
                    name: symbol.Description,
                    assetType: AssetType.Stock,
                    currency: ResolveCurrency(symbol.Currency),
                    exchange: ResolveExchangeLabel(symbol.Mic, exchangeCode),
                    dataProviderKey: symbol.Symbol))
                .ToList();

            assets.AddRange(mappedAssets);

            _logger.LogInformation(
                "[GlobalStockAssetProvider] Exchange {Exchange} yielded {Count} stock assets.",
                exchangeCode,
                mappedAssets.Count);
        }

        var deduplicated = assets
            .GroupBy(asset => asset.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        _logger.LogInformation(
            "[GlobalStockAssetProvider] Total deduplicated stock assets fetched: {Count}.",
            deduplicated.Count);

        return deduplicated.AsReadOnly();
    }

    private string ResolveCurrency(string? providerCurrency)
        => string.IsNullOrWhiteSpace(providerCurrency)
            ? _options.DefaultCurrency
            : providerCurrency.Trim().ToUpperInvariant();

    private string ResolveExchangeLabel(string? mic, string exchangeCode)
        => string.IsNullOrWhiteSpace(mic)
            ? $"{_options.DefaultExchangeLabel}-{exchangeCode}"
            : mic.Trim().ToUpperInvariant();

    private sealed record FinnhubSymbolResponse(
        string Symbol,
        string Description,
        string? Currency,
        string? Mic,
        string? Type);
}