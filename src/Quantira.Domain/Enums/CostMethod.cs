namespace Quantira.Domain.Enums;

/// <summary>
/// Defines the inventory cost method used to calculate the average cost
/// and realized P&amp;L when a sell trade is recorded.
/// Set at the portfolio level and applied consistently to all positions
/// within that portfolio. Changing this value after trades exist
/// triggers a full position recalculation for the affected portfolio.
/// </summary>
public enum CostMethod
{
    /// <summary>
    /// First In, First Out. The oldest purchased lots are sold first.
    /// Most common method and the default for Quantira portfolios.
    /// Generally results in higher realized gains in rising markets.
    /// </summary>
    Fifo = 1,

    /// <summary>
    /// Last In, First Out. The most recently purchased lots are sold first.
    /// Less common; may reduce short-term gains in rising markets.
    /// Note: LIFO is not permitted for tax purposes in some jurisdictions.
    /// </summary>
    Lifo = 2,

    /// <summary>
    /// Weighted Average Cost. All lots are blended into a single average
    /// cost price. Simplest to track; widely used for crypto and funds.
    /// </summary>
    Average = 3
}