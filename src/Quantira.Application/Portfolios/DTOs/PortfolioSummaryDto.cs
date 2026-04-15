namespace Quantira.Application.Portfolios.DTOs;

/// <summary>
/// Full portfolio valuation snapshot returned by
/// <c>GetPortfolioSummaryQueryHandler</c>. Contains aggregated financial
/// totals and the full position list. Used as the primary data source
/// for the Quantira dashboard and as the portfolio context payload
/// sent to the AI chatbot.
/// </summary>
/// <param name="PortfolioId">The portfolio identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="BaseCurrency">ISO 4217 base currency for all monetary values.</param>
/// <param name="TotalCost">Sum of cost basis across all positions.</param>
/// <param name="TotalCurrentValue">Sum of current market values across all positions.</param>
/// <param name="TotalUnrealizedPnL">TotalCurrentValue minus TotalCost.</param>
/// <param name="TotalUnrealizedPnLPct">Unrealized P&amp;L as a percentage of TotalCost.</param>
/// <param name="Positions">All open positions in this portfolio.</param>
/// <param name="LastUpdated">UTC timestamp of when this summary was calculated.</param>
public sealed record PortfolioSummaryDto(
    Guid PortfolioId,
    string Name,
    string BaseCurrency,
    decimal TotalCost,
    decimal TotalCurrentValue,
    decimal TotalUnrealizedPnL,
    decimal TotalUnrealizedPnLPct,
    IReadOnlyList<PositionDto> Positions,
    DateTime LastUpdated);