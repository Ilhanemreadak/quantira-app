using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Domain.Enums;

namespace Quantira.Application.Assets.Commands.CreateAsset;

/// <summary>
/// Command to register a new tradeable asset in the Quantira asset catalogue.
/// Assets are shared reference data — once created they are available to all
/// users for trade entry and watchlist tracking.
/// Typically called during system seeding or when a user searches for a symbol
/// that does not yet exist in the catalogue.
/// </summary>
/// <param name="Symbol">
/// Ticker symbol in any case — normalized to uppercase by the handler.
/// Must be unique across all asset types.
/// </param>
/// <param name="Name">Full display name of the asset.</param>
/// <param name="AssetType">Category determining the market data provider route.</param>
/// <param name="Currency">ISO 4217 currency the asset is priced in.</param>
/// <param name="Exchange">
/// Exchange code (e.g. "BIST", "NYSE"). Optional for commodities and FX pairs.
/// </param>
/// <param name="Sector">GICS sector. Populated for stocks, null for others.</param>
/// <param name="DataProviderKey">
/// Provider-specific symbol (e.g. "THYAO.IS" for Yahoo Finance).
/// Can be set later via a separate command once the mapping is confirmed.
/// </param>
public sealed record CreateAssetCommand(
    string Symbol,
    string Name,
    AssetType AssetType,
    string Currency,
    string? Exchange = null,
    string? Sector = null,
    string? DataProviderKey = null
) : IRequest<Guid>, ITransactionalCommand;