using MediatR;
using Quantira.Application.Chat.DTOs;
using Quantira.Application.Chat.Services;

namespace Quantira.Application.Chat.Queries.GetChatHistory;

/// <summary>
/// Handles <see cref="GetChatHistoryQuery"/>.
/// Fetches recent messages from MongoDB via <see cref="IChatSessionService"/>
/// and projects them to <see cref="ChatMessageDto"/>.
/// </summary>
public sealed class GetChatHistoryQueryHandler
    : IRequestHandler<GetChatHistoryQuery, IReadOnlyList<ChatMessageDto>>
{
    private readonly IChatSessionService _chatSessionService;

    public GetChatHistoryQueryHandler(IChatSessionService chatSessionService)
        => _chatSessionService = chatSessionService;

    public async Task<IReadOnlyList<ChatMessageDto>> Handle(
        GetChatHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var count = Math.Min(query.Count, 200);

        var messages = await _chatSessionService.GetRecentMessagesAsync(
            sessionId: query.SessionId,
            count: count,
            cancellationToken: cancellationToken);

        return messages
            .Select(m => new ChatMessageDto(
                Role: m.Role,
                Content: m.Content,
                CreatedAt: m.CreatedAt))
            .ToList()
            .AsReadOnly();
    }
}