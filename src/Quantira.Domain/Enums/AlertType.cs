namespace Quantira.Domain.Enums;

/// <summary>
/// Defines the condition type that triggers a <c>Alert</c>.
/// Determines how <c>AlertCheckJob</c> evaluates the alert's
/// <c>ConditionJson</c> payload against live market data.
/// </summary>
public enum AlertType
{
    /// <summary>
    /// Triggered when the asset price rises above a defined threshold.
    /// ConditionJson: <c>{ "threshold": 185.0, "currency": "USD" }</c>
    /// </summary>
    PriceAbove = 1,

    /// <summary>
    /// Triggered when the asset price falls below a defined threshold.
    /// ConditionJson: <c>{ "threshold": 150.0, "currency": "USD" }</c>
    /// </summary>
    PriceBelow = 2,

    /// <summary>
    /// Triggered when a technical indicator crosses a defined level
    /// (e.g. RSI drops below 30 or MACD golden cross occurs).
    /// ConditionJson: <c>{ "indicator": "RSI", "operator": "lt", "value": 30 }</c>
    /// </summary>
    IndicatorSignal = 3,

    /// <summary>
    /// Triggered when the AI sentiment analysis of news related to
    /// the asset falls below a negative score threshold.
    /// ConditionJson: <c>{ "sentimentScore": -0.6 }</c>
    /// </summary>
    NewsSentiment = 4,

    /// <summary>
    /// Triggered when the portfolio's total daily loss exceeds
    /// a defined percentage of its total value.
    /// ConditionJson: <c>{ "lossPercentage": 3.0 }</c>
    /// </summary>
    PortfolioLoss = 5
}