using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantira.Application.Portfolios.Commands.AddTrade;
using Quantira.Application.Portfolios.Queries.GetTradeHistory;
using Quantira.Domain.Enums;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// REST API endpoints for trade operations.
/// Trades are the core financial records in Quantira — every position
/// update, P&amp;L calculation and portfolio valuation flows from trade data.
/// Trades are immutable once recorded: no PUT or PATCH endpoints exist.
/// Corrections are handled by recording an offsetting trade.
/// All endpoints require authentication and enforce portfolio ownership
/// through the command/query handler layer.
/// </summary>
[ApiController]
[Authorize]
[Route("api/portfolios/{portfolioId:guid}/trades")]
public sealed class TradesController : ControllerBase
{
    private readonly ISender _sender;

    public TradesController(ISender sender)
        => _sender = sender;

    private Guid UserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns a paginated, filterable list of trades for the given portfolio.
    /// Supports filtering by asset, trade type and date range.
    /// Ordered by <c>TradedAt</c> descending — most recent trades first.
    /// </summary>
    /// <param name="portfolioId">The portfolio to retrieve trades for.</param>
    /// <param name="page">1-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Items per page. Max 100. Defaults to 20.</param>
    /// <param name="assetId">Optional filter to a specific asset.</param>
    /// <param name="tradeType">Optional filter to a specific trade type.</param>
    /// <param name="from">Optional UTC start of the date range.</param>
    /// <param name="to">Optional UTC end of the date range.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(
        Guid portfolioId,
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
                PortfolioId: portfolioId,
                UserId: UserId,
                Page: page,
                PageSize: pageSize,
                AssetId: assetId,
                TradeType: tradeType,
                From: from,
                To: to), ct);

        return Ok(result);
    }

    /// <summary>
    /// Records a new trade against the given portfolio.
    /// Updates the corresponding position quantity and cost basis immediately.
    /// Raises <c>TradeAddedEvent</c> which triggers cache invalidation
    /// and a SignalR dashboard refresh.
    /// </summary>
    /// <param name="portfolioId">The portfolio to record the trade against.</param>
    /// <param name="request">Trade details.</param>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        Guid portfolioId,
        [FromBody] TradeRequest request,
        CancellationToken ct)
    {
        var tradeId = await _sender.Send(
            new AddTradeCommand(
                PortfolioId: portfolioId,
                AssetId: request.AssetId,
                TradeType: request.TradeType,
                Quantity: request.Quantity,
                Price: request.Price,
                PriceCurrency: request.PriceCurrency,
                Commission: request.Commission,
                TaxAmount: request.TaxAmount,
                TradedAt: request.TradedAt,
                Notes: request.Notes), ct);

        return CreatedAtAction(
            actionName: nameof(GetAll),
            routeValues: new { portfolioId },
            value: new { tradeId });
    }
}

/// <summary>
/// Request body for recording a new trade.
/// </summary>
/// <param name="AssetId">The asset being bought, sold or transferred.</param>
/// <param name="TradeType">The nature of the transaction.</param>
/// <param name="Quantity">
/// Number of units. Must be positive.
/// Supports up to 8 decimal places for crypto assets.
/// </param>
/// <param name="Price">Per-unit execution price. Must be non-negative.</param>
/// <param name="PriceCurrency">ISO 4217 currency of the execution price.</param>
/// <param name="Commission">Brokerage commission paid. Defaults to zero.</param>
/// <param name="TaxAmount">Withholding or transaction tax. Defaults to zero.</param>
/// <param name="TradedAt">
/// Actual execution time in UTC. Defaults to now.
/// May be in the past to support historical trade imports.
/// </param>
/// <param name="Notes">Optional free-text note for this trade.</param>
public sealed record TradeRequest(
    Guid AssetId,
    TradeType TradeType,
    decimal Quantity,
    decimal Price,
    string PriceCurrency,
    decimal Commission = 0m,
    decimal TaxAmount = 0m,
    DateTime? TradedAt = null,
    string? Notes = null);