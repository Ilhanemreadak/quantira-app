namespace Quantira.Application.Chat.Services;

/// <summary>
/// Manages chat session lifecycle and message persistence in MongoDB.
/// Sessions are automatically expired after 90 days via a TTL index
/// on the <c>chat_messages</c> collection.
/// Implemented in <c>Quantira.Infrastructure</c> using the MongoDB driver.
/// </summary>
public interface IChatSessionService
{
    /// <summary>
    /// Creates a new chat session and returns its identifier.
    /// The session title defaults to the current UTC date and time
    /// until the user renames it or the AI generates a summary title.
    /// </summary>
    Task<Guid> CreateSessionAsync(
        Guid userId,
        Guid? portfolioId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a user message to the session's message history.
    /// </summary>
    Task SaveUserMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an assistant (AI) message to the session's message history.
    /// Also updates the session's <c>LastMessageAt</c> timestamp.
    /// </summary>
    Task SaveAssistantMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last <paramref name="count"/> messages from the given
    /// session in chronological order. Used to build the conversation history
    /// for multi-turn AI requests.
    /// </summary>
    Task<IReadOnlyList<ChatMessageRecord>> GetRecentMessagesAsync(
        Guid sessionId,
        int count = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single persisted message within a chat session.
/// </summary>
/// <param name="Role">Either "user" or "assistant".</param>
/// <param name="Content">The full text content of the message.</param>
/// <param name="CreatedAt">UTC timestamp of when the message was saved.</param>
public sealed record ChatMessageRecord(
    string Role,
    string Content,
    DateTime CreatedAt);