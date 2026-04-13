using MediatR;

namespace Quantira.Application.Chat.Commands.SendMessage;

/// <summary>
/// Command to send a message to the Quantira AI assistant and receive
/// a context-aware financial response. The handler builds a portfolio
/// and asset context snapshot from the current user's data and passes
/// it to <see cref="Common.Interfaces.IAIService"/> alongside the message.
/// Supports both full-response and streaming modes — streaming mode
/// returns an empty string and delivers tokens via SignalR instead.
/// </summary>
/// <param name="UserId">The authenticated user sending the message.</param>
/// <param name="Message">The user's question or instruction.</param>
/// <param name="SessionId">
/// Optional existing chat session ID for conversation continuity.
/// When null a new session is created automatically.
/// </param>
/// <param name="PortfolioId">
/// Optional portfolio to use as context. When provided the handler
/// includes the portfolio summary, positions and P&amp;L in the AI context.
/// </param>
/// <param name="AssetId">
/// Optional asset to use as focused context. When provided the handler
/// includes the latest price, technical indicators and recent news
/// for this asset in the AI context.
/// </param>
/// <param name="Streaming">
/// When <c>true</c> the response is streamed token-by-token via SignalR
/// and this command returns an empty string. The SignalR connection ID
/// of the caller is used to target the stream.
/// When <c>false</c> the full response is returned synchronously.
/// </param>
public sealed record SendMessageCommand(
    Guid UserId,
    string Message,
    Guid? SessionId = null,
    Guid? PortfolioId = null,
    Guid? AssetId = null,
    bool Streaming = false
) : IRequest<string>;