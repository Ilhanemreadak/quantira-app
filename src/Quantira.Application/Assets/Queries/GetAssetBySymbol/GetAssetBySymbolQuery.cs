using MediatR;
using Quantira.Application.Assets.DTOs;
using Quantira.Application.Common.Behaviors;

namespace Quantira.Application.Assets.Queries.GetAssetBySymbol;

/// <summary>
/// Query to retrieve a single asset by its ticker symbol.
/// Used during trade entry when the user types a symbol to look up
/// asset details before confirming the trade.
/// Results are cached for 5 minutes — asset metadata is stable
/// and does not need frequent invalidation.
/// </summary>
/// <param name="Symbol">
/// The ticker symbol to look up (case-insensitive).
/// Examples: "THYAO", "btc", "XAU/USD".
/// </param>
public sealed record GetAssetBySymbolQuery(string Symbol)
    : IRequest<AssetDto?>, ICacheableQuery
{
    public string CacheKey =>
        $"quantira:asset:symbol:{Symbol.ToUpperInvariant()}";

    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(5);
}