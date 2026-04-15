using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantira.Application.Alerts.Commands.CreateAlert;
using Quantira.Application.Alerts.Commands.DeleteAlert;
using Quantira.Application.Alerts.Queries.GetUserAlerts;
using Quantira.Domain.Enums;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// REST API endpoints for alert management.
/// All endpoints require authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AlertsController : ControllerBase
{
    private readonly ISender _sender;

    public AlertsController(ISender sender)
        => _sender = sender;

    private Guid UserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Returns all alerts for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AlertType? alertType = null,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetUserAlertsQuery(UserId, alertType), ct);

        return Ok(result);
    }

    /// <summary>Creates a new price or indicator alert.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAlertRequest request,
        CancellationToken ct)
    {
        var id = await _sender.Send(
            new CreateAlertCommand(
                UserId: UserId,
                AssetId: request.AssetId,
                AlertType: request.AlertType,
                ConditionJson: request.ConditionJson,
                ExpiresAt: request.ExpiresAt), ct);

        return CreatedAtAction(nameof(GetAll), new { id });
    }

    /// <summary>Deletes (expires) an alert.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteAlertCommand(id, UserId), ct);
        return NoContent();
    }
}

public sealed record CreateAlertRequest(
    Guid AssetId,
    AlertType AlertType,
    string ConditionJson,
    DateTime? ExpiresAt = null);