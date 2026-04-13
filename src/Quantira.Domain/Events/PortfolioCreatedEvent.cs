using Quantira.Domain.Common;

namespace Quantira.Domain.Events;

/// <summary>
/// Raised when a new portfolio is successfully created.
/// Handled by the application layer to perform post-creation side effects
/// such as creating a default watchlist, sending a welcome notification,
/// or logging the event for audit purposes.
/// </summary>
public sealed class PortfolioCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <summary>The unique identifier of the newly created portfolio.</summary>
    public Guid PortfolioId { get; }

    /// <summary>The identifier of the user who owns this portfolio.</summary>
    public Guid UserId { get; }

    /// <summary>The display name given to the portfolio at creation.</summary>
    public string PortfolioName { get; }

    public PortfolioCreatedEvent(Guid portfolioId, Guid userId, string portfolioName)
    {
        PortfolioId = portfolioId;
        UserId = userId;
        PortfolioName = portfolioName;
    }
}