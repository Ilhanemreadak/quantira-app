using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Domain.Enums;

namespace Quantira.Application.Portfolios.Commands.AddTrade;

/// <summary>
/// Command to record a new trade against an existing portfolio position.
/// This is the most critical write operation in Quantira — it updates
/// the position quantity, recalculates the cost basis, raises
/// <c>TradeAddedEvent</c>, and invalidates the portfolio value cache.
/// Wrapped in a database transaction by <c>TransactionBehavior</c>.
/// </summary>
/// <param name="PortfolioId">The portfolio to record the trade against.</param>
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
public sealed record AddTradeCommand(
    Guid PortfolioId,
    Guid AssetId,
    TradeType TradeType,
    decimal Quantity,
    decimal Price,
    string PriceCurrency,
    decimal Commission = 0m,
    decimal TaxAmount = 0m,
    DateTime? TradedAt = null,
    string? Notes = null
) : IRequest<Guid>, ITransactionalCommand;