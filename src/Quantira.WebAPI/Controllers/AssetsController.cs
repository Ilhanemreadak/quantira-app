using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantira.Application.Assets.Commands.CreateAsset;
using Quantira.Application.Assets.Queries.GetAssetBySymbol;
using Quantira.Domain.Enums;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// REST API endpoints for asset catalogue management.
/// Asset reads are public (no auth required) so the trade entry form
/// can resolve symbols without authentication.
/// Asset creation requires authentication and is intended for
/// admin use during seeding or when a user searches for an unknown symbol.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AssetsController : ControllerBase
{
    private readonly ISender _sender;

    public AssetsController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// Retrieves an asset by ticker symbol.
    /// Returns 404 when the symbol is not in the Quantira catalogue.
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<IActionResult> GetBySymbol(
        string symbol,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetAssetBySymbolQuery(symbol), ct);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Registers a new asset in the Quantira catalogue.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssetRequest request,
        CancellationToken ct)
    {
        var id = await _sender.Send(
            new CreateAssetCommand(
                Symbol: request.Symbol,
                Name: request.Name,
                AssetType: request.AssetType,
                Currency: request.Currency,
                Exchange: request.Exchange,
                Sector: request.Sector,
                DataProviderKey: request.DataProviderKey), ct);

        return CreatedAtAction(
            nameof(GetBySymbol),
            new { symbol = request.Symbol },
            new { id });
    }
}

public sealed record CreateAssetRequest(
    string Symbol,
    string Name,
    AssetType AssetType,
    string Currency,
    string? Exchange = null,
    string? Sector = null,
    string? DataProviderKey = null);