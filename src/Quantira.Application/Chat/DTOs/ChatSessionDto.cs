namespace Quantira.Application.Chat.DTOs;

/// <summary>
/// Read model for a chat session summary returned by the session list query.
/// Used to populate the conversation history sidebar in the Quantira UI
/// so users can navigate between previous sessions.
/// </summary>
/// <param name="SessionId">Unique identifier of the session.</param>
/// <param name="Title">
/// Auto-generated or user-defined session title.
/// Defaults to the session creation date until a title is set.
/// </param>
/// <param name="PortfolioId">
/// The portfolio that was active when this session was created.
/// Null if no portfolio context was set.
/// </param>
/// <param name="CreatedAt">UTC timestamp of session creation.</param>
/// <param name="LastMessageAt">UTC timestamp of the most recent message.</param>
/// <param name="MessageCount">Total number of messages in this session.</param>
public sealed record ChatSessionDto(
    Guid SessionId,
    string Title,
    Guid? PortfolioId,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount);