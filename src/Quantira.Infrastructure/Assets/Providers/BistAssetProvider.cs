using Microsoft.Extensions.Logging;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Assets.Providers;

public sealed class BistAssetProvider : IAssetProvider
{
    private readonly ILogger<BistAssetProvider> _logger;

    public BistAssetProvider(ILogger<BistAssetProvider> logger)
        => _logger = logger;

    public AssetType SupportedType => AssetType.Stock;

    public Task<IReadOnlyList<Asset>> FetchAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[BistAssetProvider] BIST feed is not connected yet. " +
            "Returning empty catalogue snapshot.");

        IReadOnlyList<Asset> assets = [];
        return Task.FromResult(assets);
    }
}
