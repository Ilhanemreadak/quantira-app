using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantira.Application.Portfolios.Commands.AddTrade;
using Quantira.Application.Portfolios.Commands.CreatePortfolio;
using Quantira.Application.Portfolios.Commands.DeletePortfolio;
using Quantira.Application.Portfolios.Queries.GetPortfolioList;
using Quantira.Application.Portfolios.Queries.GetPortfolioSummary;
using Quantira.Application.Portfolios.Queries.GetTradeHistory;
using Quantira.Domain.Enums;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// REST API endpoints for portfolio management.
/// All endpoints require authentication — the authenticated user's ID
/// is extracted from the JWT claims and injected into every command/query
/// to enforce ownership isolation at the handler level.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class PortfoliosController : ControllerBase
{
    private readonly ISender _sender;

    public PortfoliosController(ISender sender)
        => _sender = sender;

    private Guid UserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Returns all active portfolios for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetPortfolioListQuery(UserId), ct);

        return Ok(result);
    }

    /// <summary>Returns a full portfolio summary including positions and P&amp;L.</summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(
            new GetPortfolioSummaryQuery(id, UserId), ct);

        return Ok(result);
    }

    /// <summary>Returns a paginated, filterable trade history for a portfolio.</summary>
    [HttpGet("{id:guid}/trades")]
    public async Task<IActionResult> GetTrades(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? assetId = null,
        [FromQuery] TradeType? tradeType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetTradeHistoryQuery(
                PortfolioId: id,
                UserId: UserId,
                Page: page,
                PageSize: pageSize,
                AssetId: assetId,
                TradeType: tradeType,
                From: from,
                To: to), ct);

        return Ok(result);
    }

    /// <summary>Creates a new portfolio for the authenticated user.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePortfolioRequest request,
        CancellationToken ct)
    {
        var id = await _sender.Send(
            new CreatePortfolioCommand(
                UserId: UserId,
                Name: request.Name,
                BaseCurrency: request.BaseCurrency,
                CostMethod: request.CostMethod,
                Description: request.Description,
                IsDefault: request.IsDefault), ct);

        return CreatedAtAction(nameof(GetSummary), new { id }, new { id });
    }

    /// <summary>Records a new trade against an existing portfolio.</summary>
    [HttpPost("{id:guid}/trades")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddTrade(
        Guid id,
        [FromBody] AddTradeRequest request,
        CancellationToken ct)
    {
        var tradeId = await _sender.Send(
            new AddTradeCommand(
                PortfolioId: id,
                AssetId: request.AssetId,
                TradeType: request.TradeType,
                Quantity: request.Quantity,
                Price: request.Price,
                PriceCurrency: request.PriceCurrency,
                Commission: request.Commission,
                TaxAmount: request.TaxAmount,
                TradedAt: request.TradedAt,
                Notes: request.Notes), ct);

        return CreatedAtAction(nameof(GetTrades), new { id },
            new { tradeId });
    }

    /// <summary>Soft-deletes a portfolio.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(
            new DeletePortfolioCommand(id, UserId), ct);

        return NoContent();
    }
}

public sealed record CreatePortfolioRequest(
    string Name,
    string BaseCurrency,
    CostMethod CostMethod = CostMethod.Fifo,
    string? Description = null,
    bool IsDefault = false);

public sealed record AddTradeRequest(
    Guid AssetId,
    TradeType TradeType,
    decimal Quantity,
    decimal Price,
    string PriceCurrency,
    decimal Commission = 0m,
    decimal TaxAmount = 0m,
    DateTime? TradedAt = null,
    string? Notes = null);