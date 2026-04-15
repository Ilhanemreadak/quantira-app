namespace Quantira.Application.Portfolios.DTOs;

/// <summary>
/// Represents a single trade record returned by
/// <c>GetTradeHistoryQueryHandler</c>. Used to populate the trade
/// history table in the Quantira UI and as line items in P&amp;L reports.
/// </summary>
/// <param name="Id">Unique identifier of the trade record.</param>
/// <param name="PortfolioId">The portfolio this trade belongs to.</param>
/// <param name="AssetId">The asset that was traded.</param>
/// <param name="TradeType">The type of transaction (Buy, Sell, Dividend, etc.).</param>
/// <param name="Quantity">Number of units traded.</param>
/// <param name="Price">Per-unit execution price.</param>
/// <param name="PriceCurrency">ISO 4217 currency of the execution price.</param>
/// <param name="Commission">Brokerage commission paid.</param>
/// <param name="TaxAmount">Tax applied to this trade.</param>
/// <param name="GrossValue">Price × Quantity before fees.</param>
/// <param name="NetValue">GrossValue adjusted for commission and tax.</param>
/// <param name="Notes">Optional user note for this trade.</param>
/// <param name="TradedAt">UTC timestamp of the actual execution.</param>
/// <param name="CreatedAt">UTC timestamp of when the record was created in Quantira.</param>
public sealed record TradeDto(
    Guid Id,
    Guid PortfolioId,
    Guid AssetId,
    string TradeType,
    decimal Quantity,
    decimal Price,
    string PriceCurrency,
    decimal Commission,
    decimal TaxAmount,
    decimal GrossValue,
    decimal NetValue,
    string? Notes,
    DateTime TradedAt,
    DateTime CreatedAt);