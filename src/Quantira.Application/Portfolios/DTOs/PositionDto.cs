namespace Quantira.Application.Portfolios.DTOs;

/// <summary>
/// Represents a single open position within a portfolio summary.
/// Returned as part of <see cref="PortfolioSummaryDto"/>.
/// All monetary values are expressed in the portfolio's base currency
/// after cross-currency conversion where applicable.
/// </summary>
/// <param name="AssetId">The held asset's identifier.</param>
/// <param name="Quantity">Current number of units held.</param>
/// <param name="AvgCostPrice">Weighted average cost per unit.</param>
/// <param name="TotalCost">Quantity × AvgCostPrice — the cost basis.</param>
/// <param name="Currency">ISO 4217 currency of all monetary values.</param>
/// <param name="CurrentPrice">Latest market price per unit from Redis.</param>
/// <param name="CurrentValue">Quantity × CurrentPrice.</param>
/// <param name="UnrealizedPnL">CurrentValue minus TotalCost.</param>
/// <param name="UnrealizedPnLPct">UnrealizedPnL as a percentage of TotalCost.</param>
/// <param name="LastUpdated">UTC timestamp of the last market value refresh.</param>
public sealed record PositionDto(
    Guid AssetId,
    decimal Quantity,
    decimal AvgCostPrice,
    decimal TotalCost,
    string Currency,
    decimal CurrentPrice,
    decimal CurrentValue,
    decimal UnrealizedPnL,
    decimal UnrealizedPnLPct,
    DateTime LastUpdated);