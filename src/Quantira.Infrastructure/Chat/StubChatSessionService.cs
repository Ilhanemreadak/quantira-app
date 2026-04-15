using Quantira.Application.Chat.Services;

namespace Quantira.Infrastructure.Chat;

/// <summary>
/// Stub implementation of <see cref="IChatSessionService"/>.
/// Returns empty results until the MongoDB implementation is written.
/// Replace this registration in <c>DependencyInjection.cs</c>
/// when <c>MongoChatSessionService</c> is implemented.
/// </summary>
public sealed class StubChatSessionService : IChatSessionService
{
    public Task<Guid> CreateSessionAsync(
        Guid userId,
        Guid? portfolioId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Guid.NewGuid());

    public Task SaveUserMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveAssistantMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<ChatMessageRecord>> GetRecentMessagesAsync(
        Guid sessionId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ChatMessageRecord> empty = [];
        return Task.FromResult(empty);
    }
}