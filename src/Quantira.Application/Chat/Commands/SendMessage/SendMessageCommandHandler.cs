using MediatR;
using Quantira.Application.Chat.Services;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Application.Chat.Commands.SendMessage;

/// <summary>
/// Handles <see cref="SendMessageCommand"/>.
/// Orchestrates three steps: context building, AI invocation, and
/// session persistence. For streaming requests, tokens are pushed
/// to the SignalR hub by the AI service implementation and an empty
/// string is returned to the caller. For non-streaming requests
/// the full response string is returned.
/// Chat cost tracking (token count and USD cost) is persisted
/// to MongoDB via <see cref="IChatSessionService"/> for usage analytics.
/// </summary>
public sealed class SendMessageCommandHandler
    : IRequestHandler<SendMessageCommand, string>
{
    private readonly IAIService _aiService;
    private readonly IPortfolioContextBuilder _contextBuilder;
    private readonly IChatSessionService _chatSessionService;

    public SendMessageCommandHandler(
        IAIService aiService,
        IPortfolioContextBuilder contextBuilder,
        IChatSessionService chatSessionService)
    {
        _aiService = aiService;
        _contextBuilder = contextBuilder;
        _chatSessionService = chatSessionService;
    }

    public async Task<string> Handle(
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        var sessionId = command.SessionId
            ?? await _chatSessionService.CreateSessionAsync(
                command.UserId,
                command.PortfolioId,
                cancellationToken);

        var context = await _contextBuilder.BuildAsync(
            userId: command.UserId,
            portfolioId: command.PortfolioId,
            assetId: command.AssetId,
            cancellationToken: cancellationToken);

        await _chatSessionService.SaveUserMessageAsync(
            sessionId: sessionId,
            content: command.Message,
            cancellationToken: cancellationToken);

        if (command.Streaming)
        {
            _ = Task.Run(async () =>
            {
                var fullResponse = string.Empty;

                await foreach (var token in _aiService.StreamAdviceAsync(
                    context, command.Message, cancellationToken))
                {
                    fullResponse += token;
                }

                await _chatSessionService.SaveAssistantMessageAsync(
                    sessionId: sessionId,
                    content: fullResponse,
                    cancellationToken: CancellationToken.None);

            }, cancellationToken);

            return string.Empty;
        }

        var response = await _aiService.GetAdviceAsync(
            context, command.Message, cancellationToken);

        await _chatSessionService.SaveAssistantMessageAsync(
            sessionId: sessionId,
            content: response,
            cancellationToken: cancellationToken);

        return response;
    }
}