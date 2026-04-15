using Quantira.Domain.Enums;
using Quantira.Domain.Exceptions;

namespace Quantira.Infrastructure.MarketData;

/// <summary>
/// Selects the correct <see cref="IMarketDataProvider"/> for a given
/// asset type and exchange at runtime. All registered providers are
/// injected via DI and evaluated in order via <see cref="IMarketDataProvider.CanHandle"/>.
/// Provider priority is determined by registration order in
/// <c>DependencyInjection.cs</c> — register more specific providers first.
/// </summary>
public sealed class MarketDataProviderFactory
{
    private readonly IEnumerable<IMarketDataProvider> _providers;

    public MarketDataProviderFactory(IEnumerable<IMarketDataProvider> providers)
        => _providers = providers;

    /// <summary>
    /// Returns the first provider that can handle the given asset type
    /// and exchange combination.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when no registered provider supports the requested combination.
    /// </exception>
    public IMarketDataProvider GetProvider(
        AssetType assetType,
        string? exchange = null)
    {
        var provider = _providers.FirstOrDefault(p =>
            p.CanHandle(assetType, exchange));

        if (provider is null)
            throw new DomainException(
                $"No market data provider found for AssetType={assetType}, Exchange={exchange}. " +
                $"Registered providers: {string.Join(", ", _providers.Select(p => p.ProviderName))}");

        return provider;
    }
}