using MediatR;
using Microsoft.AspNetCore.Mvc;
using Quantira.Application.MarketData.Queries.CalculateIndicator;
using Quantira.Application.MarketData.Queries.GetPriceHistory;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// REST API endpoints for market data: price history and technical indicators.
/// These endpoints are intentionally unauthenticated so the frontend can
/// display charts and indicators on the asset discovery screen before login.
/// Rate limiting is applied at the infrastructure level.
/// </summary>
[ApiController]
[Route("api/market")]
public sealed class MarketDataController : ControllerBase
{
    private readonly ISender _sender;

    public MarketDataController(ISender sender)
        => _sender = sender;

    /// <summary>
    /// Returns OHLCV candlestick data for the given symbol and interval.
    /// </summary>
    [HttpGet("{symbol}/history")]
    public async Task<IActionResult> GetHistory(
        string symbol,
        [FromQuery] string interval = "1d",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetPriceHistoryQuery(
                Symbol: symbol,
                Interval: interval,
                From: from ?? DateTime.UtcNow.AddDays(-90),
                To: to ?? DateTime.UtcNow), ct);

        return Ok(result);
    }

    /// <summary>
    /// Calculates a technical indicator for the given symbol.
    /// </summary>
    [HttpGet("{symbol}/indicators/{indicatorName}")]
    public async Task<IActionResult> GetIndicator(
        string symbol,
        string indicatorName,
        [FromQuery] string interval = "1d",
        [FromQuery] Dictionary<string, string>? parameters = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new CalculateIndicatorQuery(
                Symbol: symbol,
                IndicatorName: indicatorName,
                Interval: interval,
                Parameters: parameters), ct);

        return Ok(result);
    }
}