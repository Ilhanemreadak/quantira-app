namespace Quantira.Domain.Enums;

/// <summary>
/// Defines the category of a financial asset tracked in Quantira.
/// Used to route market data requests to the correct provider
/// (e.g. <c>Stock</c> → Yahoo Finance, <c>Crypto</c> → Binance,
/// <c>Commodity</c> → GoldApi), apply asset-specific business rules,
/// and group positions in portfolio reports.
/// </summary>
public enum AssetType
{
    /// <summary>
    /// Exchange-listed equity (e.g. THYAO, AAPL, MSFT).
    /// Traded during exchange hours. Subject to corporate actions
    /// such as dividends and stock splits.
    /// </summary>
    Stock = 1,

    /// <summary>
    /// Cryptocurrency (e.g. BTC, ETH, SOL).
    /// Trades 24/7. Quantity precision up to 8 decimal places.
    /// </summary>
    Crypto = 2,

    /// <summary>
    /// Physical commodity (e.g. XAU/USD gold, XAG/USD silver, WTI crude oil).
    /// Priced in USD per troy ounce or barrel depending on the commodity.
    /// </summary>
    Commodity = 3,

    /// <summary>
    /// Foreign currency pair (e.g. USD/TRY, EUR/USD).
    /// Used for tracking FX positions and for cross-currency
    /// portfolio valuation.
    /// </summary>
    Currency = 4,

    /// <summary>
    /// Investment fund unit (e.g. Turkish mutual funds, ETFs).
    /// Priced once per day at NAV.
    /// </summary>
    Fund = 5
}