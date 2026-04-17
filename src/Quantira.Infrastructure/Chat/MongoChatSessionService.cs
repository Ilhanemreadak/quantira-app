using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Chat.Services;

namespace Quantira.Infrastructure.Chat;

/// <summary>
/// MongoDB implementation of <see cref="IChatSessionService"/>.
/// Stores chat sessions and messages in MongoDB for flexible schema
/// and high-volume write performance. Sessions are automatically
/// expired after <see cref="MongoDbOptions.ChatSessionTtlDays"/> days
/// via a TTL index on the <c>LastMessageAt</c> field.
/// Collections:
///   chat_sessions  — one document per session, metadata only.
///   chat_messages  — one document per message, indexed by session ID.
/// </summary>
public sealed class MongoChatSessionService : IChatSessionService
{
    private readonly IMongoCollection<ChatSessionDocument> _sessions;
    private readonly IMongoCollection<ChatMessageDocument> _messages;
    private readonly ILogger<MongoChatSessionService> _logger;

    public MongoChatSessionService(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options,
        ILogger<MongoChatSessionService> logger)
    {
        _logger = logger;

        var db = mongoClient.GetDatabase(options.Value.DatabaseName);
        _sessions = db.GetCollection<ChatSessionDocument>("chat_sessions");
        _messages = db.GetCollection<ChatMessageDocument>("chat_messages");

        EnsureIndexesAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateSessionAsync(
        Guid userId,
        Guid? portfolioId = null,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();

        var session = new ChatSessionDocument
        {
            Id = sessionId.ToString(),
            UserId = userId.ToString(),
            PortfolioId = portfolioId?.ToString(),
            Title = $"Chat — {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            MessageCount = 0
        };

        await _sessions.InsertOneAsync(session, null, cancellationToken);

        _logger.LogDebug(
            "[MongoChatSessionService] Created session {SessionId} for user {UserId}",
            sessionId, userId);

        return sessionId;
    }

    /// <inheritdoc/>
    public async Task SaveUserMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await SaveMessageAsync(sessionId, "user", content, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveAssistantMessageAsync(
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await SaveMessageAsync(sessionId, "assistant", content, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ChatMessageRecord>> GetRecentMessagesAsync(
        Guid sessionId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<ChatMessageDocument>.Filter
            .Eq(m => m.SessionId, sessionId.ToString());

        var sort = Builders<ChatMessageDocument>.Sort
            .Descending(m => m.CreatedAt);

        var docs = await _messages
            .Find(filter)
            .Sort(sort)
            .Limit(count)
            .ToListAsync(cancellationToken);

        // Return in chronological order (oldest first).
        return docs
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageRecord(
                Role: m.Role,
                Content: m.Content,
                CreatedAt: m.CreatedAt))
            .ToList()
            .AsReadOnly();
    }

    // ── Private helpers ──────────────────────────────────────────────

    private async Task SaveMessageAsync(
        Guid sessionId,
        string role,
        string content,
        CancellationToken cancellationToken)
    {
        var message = new ChatMessageDocument
        {
            Id = ObjectId.GenerateNewId().ToString(),
            SessionId = sessionId.ToString(),
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        await _messages.InsertOneAsync(message, null, cancellationToken);

        // Update session metadata.
        var filter = Builders<ChatSessionDocument>.Filter
            .Eq(s => s.Id, sessionId.ToString());

        var update = Builders<ChatSessionDocument>.Update
            .Set(s => s.LastMessageAt, DateTime.UtcNow)
            .Inc(s => s.MessageCount, 1);

        await _sessions.UpdateOneAsync(filter, update,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureIndexesAsync()
    {
        // TTL index on sessions — auto-expire after N days.
        var sessionTtlKey = Builders<ChatSessionDocument>.IndexKeys
            .Ascending(s => s.LastMessageAt);

        var sessionTtlOptions = new CreateIndexOptions
        {
            ExpireAfter = TimeSpan.FromDays(90),
            Name = "ttl_last_message_at"
        };

        await _sessions.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatSessionDocument>(sessionTtlKey, sessionTtlOptions));

        // Index on messages by session ID for fast history queries.
        var messageSessionKey = Builders<ChatMessageDocument>.IndexKeys
            .Ascending(m => m.SessionId)
            .Descending(m => m.CreatedAt);

        var messageIndexOptions = new CreateIndexOptions
        {
            Name = "idx_session_created"
        };

        await _messages.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatMessageDocument>(messageSessionKey, messageIndexOptions));

        // Index on sessions by user ID for session list queries.
        var sessionUserKey = Builders<ChatSessionDocument>.IndexKeys
            .Ascending(s => s.UserId)
            .Descending(s => s.LastMessageAt);

        await _sessions.Indexes.CreateOneAsync(
            new CreateIndexModel<ChatSessionDocument>(
                sessionUserKey,
                new CreateIndexOptions { Name = "idx_user_last_message" }));
    }

    // ── MongoDB document models ──────────────────────────────────────

    private sealed class ChatSessionDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = default!;

        public string UserId { get; set; } = default!;
        public string? PortfolioId { get; set; }
        public string Title { get; set; } = default!;
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
    }

    private sealed class ChatMessageDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = default!;

        public string SessionId { get; set; } = default!;
        public string Role { get; set; } = default!;
        public string Content { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}

/// <summary>
/// MongoDB connection options.
/// Bound from the "MongoDB" section of appsettings.json.
/// </summary>
public sealed class MongoDbOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "quantira";

    /// <summary>Chat session TTL in days. Default: 90.</summary>
    public int ChatSessionTtlDays { get; set; } = 90;
}