using MediatR;
using Quantira.Domain.Entities;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Assets.Commands.CreateAsset;

/// <summary>
/// Handles <see cref="CreateAssetCommand"/>.
/// Creates the asset aggregate via the factory method and delegates
/// persistence to the repository.
/// </summary>
public sealed class CreateAssetCommandHandler
    : IRequestHandler<CreateAssetCommand, Guid>
{
    private readonly IAssetRepository _assetRepository;

    public CreateAssetCommandHandler(
        IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public async Task<Guid> Handle(
        CreateAssetCommand command,
        CancellationToken cancellationToken)
    {
        var asset = Asset.Create(
            symbol: command.Symbol,
            name: command.Name,
            assetType: command.AssetType,
            currency: command.Currency,
            exchange: command.Exchange,
            sector: command.Sector,
            dataProviderKey: command.DataProviderKey);

        await _assetRepository.AddAsync(asset, cancellationToken);

        return asset.Id;
    }
}