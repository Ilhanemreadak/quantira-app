using MediatR;

namespace Quantira.Domain.Common;

/// <summary>
/// Marker interface for all domain events in the Quantira domain.
/// Domain events represent something meaningful that happened within the domain
/// and are used to decouple side effects (e.g. cache invalidation, notifications,
/// SignalR broadcasts) from the core business logic.
/// Extends <see cref="INotification"/> so MediatR can dispatch events
/// to multiple handlers without the aggregate knowing about them.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>Unique identifier for this specific event occurrence.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp of when the event occurred.</summary>
    DateTime OccurredAt { get; }
}