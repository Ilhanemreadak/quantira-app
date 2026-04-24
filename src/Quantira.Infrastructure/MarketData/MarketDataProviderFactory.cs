using Microsoft.Extensions.Logging;
using Quantira.Domain.Enums;
using Quantira.Domain.Exceptions;

namespace Quantira.Infrastructure.MarketData;

public sealed class MarketDataProviderFactory
{
    private readonly IEnumerable<IMarketDataProvider> _providers;
    private readonly ILogger<MarketDataProviderFactory> _logger;

    public MarketDataProviderFactory(
        IEnumerable<IMarketDataProvider> providers,
        ILogger<MarketDataProviderFactory> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public IMarketDataProvider GetProvider(AssetType assetType, string? exchange = null)
    {
        foreach (var p in _providers)
        {
            var can = p.CanHandle(assetType, exchange);
            _logger.LogDebug(
                "[ProviderFactory] {Provider}.CanHandle(type={AssetType}, exchange={Exchange}) = {Result}",
                p.ProviderName, assetType, exchange, can);

            if (can) return p;
        }

        throw new DomainException(
            $"No market data provider found for AssetType={assetType}, Exchange={exchange}. " +
            $"Registered providers: {string.Join(", ", _providers.Select(p => p.ProviderName))}");
    }
}
