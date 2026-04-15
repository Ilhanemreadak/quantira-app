using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Assets;

public interface IAssetProvider
{
    AssetType SupportedType { get; }

    Task<IReadOnlyList<Asset>> FetchAssetsAsync(
        CancellationToken cancellationToken = default);
}
