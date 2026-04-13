namespace Quantira.Domain.Enums;

/// <summary>
/// Defines the type of a trade recorded against a portfolio position.
/// Each type affects position quantity and cost basis differently.
/// The handler layer uses this enum to determine which cost calculation
/// method (<c>FIFO</c>, <c>LIFO</c>, or <c>AVG</c>) to apply when
/// updating the corresponding <c>Position</c>.
/// </summary>
public enum TradeType
{
    /// <summary>
    /// Standard purchase. Increases position quantity and adds to cost basis.
    /// </summary>
    Buy = 1,

    /// <summary>
    /// Standard sale. Decreases position quantity and realizes P&amp;L
    /// based on the portfolio's configured <c>CostMethod</c>.
    /// </summary>
    Sell = 2,

    /// <summary>
    /// Cash or stock dividend received. Does not change position quantity
    /// but is recorded as realized income for P&amp;L reporting.
    /// </summary>
    Dividend = 3,

    /// <summary>
    /// Stock split or reverse split. Adjusts position quantity and
    /// recalculates average cost price without affecting total cost basis.
    /// </summary>
    Split = 4,

    /// <summary>
    /// Asset transferred in from an external account.
    /// Treated as a buy at the specified transfer price for cost basis purposes.
    /// </summary>
    TransferIn = 5,

    /// <summary>
    /// Asset transferred out to an external account.
    /// Treated as a sell at the specified transfer price for P&amp;L purposes.
    /// </summary>
    TransferOut = 6
}