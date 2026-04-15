using MediatR;
using Mapster;
using Quantira.Application.Assets.DTOs;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Assets.Queries.GetAssetBySymbol;

/// <summary>
/// Handles <see cref="GetAssetBySymbolQuery"/>.
/// Returns <c>null</c> when the symbol is not found rather than throwing,
/// allowing the caller to gracefully handle the "new symbol" flow
/// (prompt the user to add it to the catalogue).
/// </summary>
public sealed class GetAssetBySymbolQueryHandler
    : IRequestHandler<GetAssetBySymbolQuery, AssetDto?>
{
    private readonly IAssetRepository _assetRepository;

    public GetAssetBySymbolQueryHandler(IAssetRepository assetRepository)
        => _assetRepository = assetRepository;

    public async Task<AssetDto?> Handle(
        GetAssetBySymbolQuery query,
        CancellationToken cancellationToken)
    {
        var asset = await _assetRepository
            .GetBySymbolAsync(query.Symbol, cancellationToken);

        return asset?.Adapt<AssetDto>();
    }
}