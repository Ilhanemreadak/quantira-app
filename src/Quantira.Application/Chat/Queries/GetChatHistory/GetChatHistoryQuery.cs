using MediatR;
using Quantira.Application.Chat.DTOs;

namespace Quantira.Application.Chat.Queries.GetChatHistory;

/// <summary>
/// Query to retrieve the message history for a specific chat session.
/// Used to restore conversation state when the user reopens a previous session.
/// Not cached — chat history must always reflect the latest persisted state.
/// </summary>
/// <param name="SessionId">The chat session to retrieve history for.</param>
/// <param name="UserId">Verified against session ownership in the handler.</param>
/// <param name="Count">
/// Maximum number of messages to return, ordered newest first.
/// Defaults to 50. Capped at 200 to prevent oversized responses.
/// </param>
public sealed record GetChatHistoryQuery(
    Guid SessionId,
    Guid UserId,
    int Count = 50
) : IRequest<IReadOnlyList<ChatMessageDto>>;