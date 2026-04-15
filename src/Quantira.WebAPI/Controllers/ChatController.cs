using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quantira.Application.Chat.Commands.SendMessage;
using Quantira.Application.Chat.Queries.GetChatHistory;

namespace Quantira.WebAPI.Controllers;

/// <summary>
/// REST API endpoints for the Quantira AI chatbot.
/// Supports both full-response and streaming modes.
/// Streaming mode returns HTTP 202 Accepted immediately and delivers
/// tokens via SignalR — the client must be connected to <c>PriceHub</c>
/// before sending a streaming request.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private readonly ISender _sender;

    public ChatController(ISender sender)
        => _sender = sender;

    private Guid UserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Sends a message to the AI assistant and returns the full response.
    /// Use <c>streaming=true</c> for token-by-token delivery via SignalR.
    /// </summary>
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        var response = await _sender.Send(
            new SendMessageCommand(
                UserId: UserId,
                Message: request.Message,
                SessionId: request.SessionId,
                PortfolioId: request.PortfolioId,
                AssetId: request.AssetId,
                Streaming: request.Streaming), ct);

        if (request.Streaming)
            return Accepted(new { message = "Streaming via SignalR." });

        return Ok(new { response });
    }

    /// <summary>Returns the message history for a chat session.</summary>
    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetHistory(
        Guid sessionId,
        [FromQuery] int count = 50,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetChatHistoryQuery(sessionId, UserId, count), ct);

        return Ok(result);
    }
}

public sealed record SendMessageRequest(
    string Message,
    Guid? SessionId = null,
    Guid? PortfolioId = null,
    Guid? AssetId = null,
    bool Streaming = false);