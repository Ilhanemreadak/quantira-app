using MediatR;
using Quantira.Application.Common.Behaviors;
using Quantira.Domain.Enums;

namespace Quantira.Application.Alerts.Commands.CreateAlert;

/// <summary>
/// Command to create a new price, indicator or sentiment alert for an asset.
/// The alert becomes active immediately and is evaluated on the next
/// <c>AlertCheckJob</c> cycle (every 30 seconds).
/// </summary>
/// <param name="UserId">The user creating the alert.</param>
/// <param name="AssetId">The asset to monitor.</param>
/// <param name="AlertType">The type of condition to watch for.</param>
/// <param name="ConditionJson">
/// JSON-encoded condition parameters.
/// PriceAbove/Below: <c>{ "threshold": 185.0, "currency": "USD" }</c>
/// IndicatorSignal:  <c>{ "indicator": "RSI", "operator": "lt", "value": 30 }</c>
/// PortfolioLoss:    <c>{ "lossPercentage": 3.0 }</c>
/// </param>
/// <param name="ExpiresAt">
/// Optional UTC expiry. After this time the alert auto-expires.
/// Null means the alert never expires automatically.
/// </param>
public sealed record CreateAlertCommand(
    Guid UserId,
    Guid AssetId,
    AlertType AlertType,
    string ConditionJson,
    DateTime? ExpiresAt = null
) : IRequest<Guid>, ITransactionalCommand;